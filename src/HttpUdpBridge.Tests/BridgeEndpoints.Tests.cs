using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;

using Abstractions;

using Configuration;

using FluentAssertions;

using HttpUdpBridge.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Models;

using Transport;

namespace HttpUdpBridge.Tests;

public sealed class BridgeEndpointsTests
{
    [Fact(DisplayName = "MapBridgeEndpoints throws when app is null")]
    [Trait("Category", "Unit")]
    public void MapBridgeEndpointsWhenAppIsNullThrowsArgumentNullException()
    {
        // Arrange
        WebApplication app = null!;

        // Act
        Action act = () => BridgeEndpoints.MapBridgeEndpoints(app);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "POST /bridge returns bad request when payload is empty")]
    [Trait("Category", "Integration")]
    public async Task PostBridgeReturnsBadRequestWhenPayloadIsEmptyAsync()
    {
        // Arrange
        var coordinator = new DelegateUdpRequestCoordinator((_, _, _) =>
            throw new InvalidOperationException("Coordinator should not be invoked."));
        await using var app = await CreateApplicationAsync(
            coordinator,
            new InMemoryResponseCache());

        // Act
        var response = await app.Client.PostAsJsonAsync(
            "/bridge",
            new BridgeHttpRequest(string.Empty));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorValue = await ReadJsonPropertyAsync(response, "error");
        errorValue.Should().Be("Payload must not be empty.");
    }

    [Fact(DisplayName = "POST /bridge returns payload too large when payload exceeds maximum length")]
    [Trait("Category", "Integration")]
    public async Task PostBridgeReturnsPayloadTooLargeWhenPayloadExceedsMaximumLengthAsync()
    {
        // Arrange
        var coordinator = new DelegateUdpRequestCoordinator((_, _, _) =>
            throw new InvalidOperationException("Coordinator should not be invoked."));
        await using var app = await CreateApplicationAsync(
            coordinator,
            new InMemoryResponseCache());
        var payload = new string('x', 8193);

        // Act
        var response = await app.Client.PostAsJsonAsync(
            "/bridge",
            new BridgeHttpRequest(payload));

        // Assert
        response.StatusCode.Should().Be((HttpStatusCode)StatusCodes.Status413PayloadTooLarge);
        var errorValue = await ReadJsonPropertyAsync(response, "error");
        errorValue.Should().Be("Payload exceeds 8192 characters.");
    }

    [Fact(DisplayName = "POST /bridge returns bad request when request id header format is invalid")]
    [Trait("Category", "Integration")]
    public async Task PostBridgeReturnsBadRequestWhenRequestIdHeaderFormatIsInvalidAsync()
    {
        // Arrange
        var coordinator = new DelegateUdpRequestCoordinator((_, _, _) =>
            throw new InvalidOperationException("Coordinator should not be invoked."));
        await using var app = await CreateApplicationAsync(
            coordinator,
            new InMemoryResponseCache());
        using var request = new HttpRequestMessage(HttpMethod.Post, "/bridge")
        {
            Content = JsonContent.Create(new BridgeHttpRequest("payload"))
        };
        request.Headers.Add("X-Request-Id", "invalid id!");

        // Act
        var response = await app.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorValue = await ReadJsonPropertyAsync(response, "error");
        errorValue.Should().Be(
            "Request id format is invalid. Allowed: letters, digits, '-', '_', '.', ':'.");
    }

    [Fact(DisplayName = "POST /bridge returns payload too large when request id header exceeds maximum length")]
    [Trait("Category", "Integration")]
    public async Task PostBridgeReturnsPayloadTooLargeWhenRequestIdHeaderExceedsMaximumLengthAsync()
    {
        // Arrange
        var coordinator = new DelegateUdpRequestCoordinator((_, _, _) =>
            throw new InvalidOperationException("Coordinator should not be invoked."));
        await using var app = await CreateApplicationAsync(
            coordinator,
            new InMemoryResponseCache());
        using var request = new HttpRequestMessage(HttpMethod.Post, "/bridge")
        {
            Content = JsonContent.Create(new BridgeHttpRequest("payload"))
        };
        request.Headers.Add("X-Request-Id", new string('a', 129));

        // Act
        var response = await app.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be((HttpStatusCode)StatusCodes.Status413PayloadTooLarge);
        var errorValue = await ReadJsonPropertyAsync(response, "error");
        errorValue.Should().Be("Request id exceeds 128 characters.");
    }

    [Fact(DisplayName = "POST /bridge honors request id header and returns coordinator response")]
    [Trait("Category", "Integration")]
    public async Task PostBridgeHonorsRequestIdHeaderAndReturnsCoordinatorResponseAsync()
    {
        // Arrange
        const string payload = "ping";
        const string requestId = "request-from-header";
        const string requestIdHeaderName = "X-Test-Request-Id";
        var responseTimestamp = new DateTimeOffset(
            2026,
            2,
            19,
            0,
            0,
            0,
            TimeSpan.Zero);
        var coordinator = new DelegateUdpRequestCoordinator((request, timeout, _) =>
        {
            request.RequestId.Should().Be(requestId);
            request.Payload.Should().Be(payload);
            timeout.Should().Be(TimeSpan.FromMilliseconds(900));
            return Task.FromResult(BridgeDispatchResult.FromLive(
                new CachedUdpResponse(request.RequestId, "udp:ping", responseTimestamp)));
        });
        await using var app = await CreateApplicationAsync(
            coordinator,
            new InMemoryResponseCache(),
            new HttpBridgeOptions
            {
                RequestTimeoutMilliseconds = 900,
                RequestIdHeaderName = requestIdHeaderName
            });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/bridge")
        {
            Content = JsonContent.Create(new BridgeHttpRequest(payload))
        };
        request.Headers.Add(requestIdHeaderName, requestId);

        // Act
        var response = await app.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payloadResponse = await response.Content.ReadFromJsonAsync<BridgeHttpResponse>();
        payloadResponse.Should().NotBeNull();
        payloadResponse!.RequestId.Should().Be(requestId);
        payloadResponse.Payload.Should().Be("udp:ping");
        payloadResponse.FromCache.Should().BeFalse();
        payloadResponse.ReceivedAtUtc.Should().Be(responseTimestamp);
    }

    [Fact(DisplayName = "POST /bridge uses deterministic request id when header is missing")]
    [Trait("Category", "Integration")]
    public async Task PostBridgeUsesDeterministicRequestIdWhenHeaderIsMissingAsync()
    {
        // Arrange
        const string payload = "hello";
        var expectedRequestId = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        string? coordinatorRequestId = null;
        var coordinator = new DelegateUdpRequestCoordinator((request, _, _) =>
        {
            coordinatorRequestId = request.RequestId;
            return Task.FromResult(BridgeDispatchResult.Timeout(request.RequestId));
        });
        await using var app = await CreateApplicationAsync(
            coordinator,
            new InMemoryResponseCache());

        // Act
        var response = await app.Client.PostAsJsonAsync(
            "/bridge",
            new BridgeHttpRequest(payload));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.GatewayTimeout);
        coordinatorRequestId.Should().Be(expectedRequestId);
        var responseRequestId = await ReadJsonPropertyAsync(response, "requestId");
        responseRequestId.Should().Be(expectedRequestId);
    }

    [Fact(DisplayName = "POST /bridge returns timeout payload when coordinator times out")]
    [Trait("Category", "Integration")]
    public async Task PostBridgeReturnsTimeoutPayloadWhenCoordinatorTimesOutAsync()
    {
        // Arrange
        const string requestId = "timeout-request";
        var coordinator = new DelegateUdpRequestCoordinator((request, _, _) =>
        {
            request.RequestId.Should().Be(requestId);
            return Task.FromResult(BridgeDispatchResult.Timeout(request.RequestId));
        });
        await using var app = await CreateApplicationAsync(
            coordinator,
            new InMemoryResponseCache());
        using var request = new HttpRequestMessage(HttpMethod.Post, "/bridge")
        {
            Content = JsonContent.Create(new BridgeHttpRequest("payload"))
        };
        request.Headers.Add("X-Request-Id", requestId);

        // Act
        var response = await app.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.GatewayTimeout);
        var responseRequestId = await ReadJsonPropertyAsync(response, "requestId");
        responseRequestId.Should().Be(requestId);
        var error = await ReadJsonPropertyAsync(response, "error");
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "GET /bridge/{requestId} returns cached response")]
    [Trait("Category", "Integration")]
    public async Task GetBridgeReturnsCachedResponseAsync()
    {
        // Arrange
        const string requestId = "cached-request";
        var cachedResponse = new CachedUdpResponse(
            requestId,
            "cached-value",
            new DateTimeOffset(2026, 2, 19, 1, 0, 0, TimeSpan.Zero));
        var cache = new InMemoryResponseCache();
        cache.Store(cachedResponse);
        var coordinator = new DelegateUdpRequestCoordinator((_, _, _) =>
            throw new InvalidOperationException("Coordinator should not be invoked."));
        await using var app = await CreateApplicationAsync(coordinator, cache);

        // Act
        var response = await app.Client.GetAsync(CreateRelativeUri($"/bridge/{requestId}"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payloadResponse = await response.Content.ReadFromJsonAsync<BridgeHttpResponse>();
        payloadResponse.Should().NotBeNull();
        payloadResponse!.RequestId.Should().Be(requestId);
        payloadResponse.Payload.Should().Be("cached-value");
        payloadResponse.FromCache.Should().BeTrue();
        payloadResponse.ReceivedAtUtc.Should().Be(cachedResponse.ReceivedAtUtc);
    }

    [Fact(DisplayName = "GET /bridge/{requestId} returns not found when cache misses")]
    [Trait("Category", "Integration")]
    public async Task GetBridgeReturnsNotFoundWhenCacheMissesAsync()
    {
        // Arrange
        var coordinator = new DelegateUdpRequestCoordinator((_, _, _) =>
            throw new InvalidOperationException("Coordinator should not be invoked."));
        await using var app = await CreateApplicationAsync(
            coordinator,
            new InMemoryResponseCache());

        // Act
        var response = await app.Client.GetAsync(CreateRelativeUri("/bridge/missing"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var errorValue = await ReadJsonPropertyAsync(response, "error");
        errorValue.Should().Be("Response not found.");
    }

    [Fact(DisplayName = "GET /bridge/{requestId} returns bad request when request id format is invalid")]
    [Trait("Category", "Integration")]
    public async Task GetBridgeReturnsBadRequestWhenRequestIdFormatIsInvalidAsync()
    {
        // Arrange
        var coordinator = new DelegateUdpRequestCoordinator((_, _, _) =>
            throw new InvalidOperationException("Coordinator should not be invoked."));
        await using var app = await CreateApplicationAsync(
            coordinator,
            new InMemoryResponseCache());

        // Act
        var response = await app.Client.GetAsync(CreateRelativeUri("/bridge/invalid%20id"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorValue = await ReadJsonPropertyAsync(response, "error");
        errorValue.Should().Be(
            "Request id format is invalid. Allowed: letters, digits, '-', '_', '.', ':'.");
    }

    [Fact(DisplayName = "GET /bridge/{requestId} returns payload too large when request id exceeds maximum length")]
    [Trait("Category", "Integration")]
    public async Task GetBridgeReturnsPayloadTooLargeWhenRequestIdExceedsMaximumLengthAsync()
    {
        // Arrange
        var coordinator = new DelegateUdpRequestCoordinator((_, _, _) =>
            throw new InvalidOperationException("Coordinator should not be invoked."));
        await using var app = await CreateApplicationAsync(
            coordinator,
            new InMemoryResponseCache());

        // Act
        var response = await app.Client.GetAsync(CreateRelativeUri($"/bridge/{new string('a', 129)}"));

        // Assert
        response.StatusCode.Should().Be((HttpStatusCode)StatusCodes.Status413PayloadTooLarge);
        var errorValue = await ReadJsonPropertyAsync(response, "error");
        errorValue.Should().Be("Request id exceeds 128 characters.");
    }

    [Fact(DisplayName = "GET /hc returns healthy status")]
    [Trait("Category", "Integration")]
    public async Task GetHealthEndpointReturnsHealthyStatusAsync()
    {
        // Arrange
        var coordinator = new DelegateUdpRequestCoordinator((_, _, _) =>
            throw new InvalidOperationException("Coordinator should not be invoked."));
        await using var app = await CreateApplicationAsync(
            coordinator,
            new InMemoryResponseCache());

        // Act
        var response = await app.Client.GetAsync(CreateRelativeUri("/hc"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await ReadJsonPropertyAsync(response, "status");
        status.Should().Be("healthy");
    }

    private static async Task<TestBridgeApplication> CreateApplicationAsync(
        IUdpRequestCoordinator coordinator,
        IResponseCache responseCache,
        HttpBridgeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(responseCache);

        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        _ = builder.Services.AddSingleton(coordinator);
        _ = builder.Services.AddSingleton(responseCache);
        _ = builder.Services.AddSingleton(
            Options.Create(options ?? CreateDefaultOptions()));

        var app = builder.Build();
        app.MapBridgeEndpoints();
        _ = app.MapGet("/hc", () => Results.Ok(new { status = "healthy" }));
        await app.StartAsync();

        return new TestBridgeApplication(app, app.GetTestClient());
    }

    private static HttpBridgeOptions CreateDefaultOptions() =>
        new()
        {
            RequestTimeoutMilliseconds = 1000,
            RequestIdHeaderName = "X-Request-Id"
        };

    private static async Task<string?> ReadJsonPropertyAsync(
        HttpResponseMessage response,
        string propertyName)
    {
        using var jsonDocument = JsonDocument.Parse(
            await response.Content.ReadAsByteArrayAsync());
        return jsonDocument.RootElement.GetProperty(propertyName).GetString();
    }

    private static Uri CreateRelativeUri(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new Uri(value, UriKind.Relative);
    }

    private sealed class DelegateUdpRequestCoordinator : IUdpRequestCoordinator
    {
        private readonly Func<BridgeRequest, TimeSpan, CancellationToken, Task<BridgeDispatchResult>>
            _handler;

        public DelegateUdpRequestCoordinator(
            Func<BridgeRequest, TimeSpan, CancellationToken, Task<BridgeDispatchResult>> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            _handler = handler;
        }

        public Task<BridgeDispatchResult> DispatchAsync(
            BridgeRequest request,
            TimeSpan httpTimeout,
            CancellationToken cancellationToken) => _handler(request, httpTimeout, cancellationToken);
    }

    private sealed class InMemoryResponseCache : IResponseCache
    {
        private readonly Dictionary<string, CachedUdpResponse> _entries =
#pragma warning disable IDE0028 // Simplify collection initialization
            new(StringComparer.Ordinal);
#pragma warning restore IDE0028 // Simplify collection initialization

        public bool TryGet(
            string requestId,
            [NotNullWhen(true)] out CachedUdpResponse? response)
        {
            if (_entries.TryGetValue(requestId, out var entry))
            {
                response = entry;
                return true;
            }

            response = null;
            return false;
        }

        public void Store(CachedUdpResponse response)
        {
            ArgumentNullException.ThrowIfNull(response);
            _entries[response.RequestId] = response;
        }

        public int RemoveExpired(DateTimeOffset utcNow) => 0;
    }

    private sealed class TestBridgeApplication : IAsyncDisposable
    {
        private readonly WebApplication _app;

        public TestBridgeApplication(WebApplication app, HttpClient client)
        {
            ArgumentNullException.ThrowIfNull(app);
            ArgumentNullException.ThrowIfNull(client);

            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
