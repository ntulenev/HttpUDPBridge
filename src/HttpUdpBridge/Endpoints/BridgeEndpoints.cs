using System.Security.Cryptography;
using System.Text;

using Abstractions;

using Configuration;

using Microsoft.Extensions.Options;

using Models;

using Transport;

namespace HttpUdpBridge.Endpoints;

/// <summary>
/// Defines HTTP endpoints for the UDP bridge workflow.
/// </summary>
internal static class BridgeEndpoints
{
    /// <summary>
    /// Maps all bridge-related endpoints.
    /// </summary>
    /// <param name="app">The target web application.</param>
    public static void MapBridgeEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        _ = app.MapPost(
            "/bridge",
            async (
                BridgeHttpRequest request,
                HttpContext context,
                IUdpRequestCoordinator coordinator,
                IOptions<HttpBridgeOptions> options,
                CancellationToken token) =>
            {
                if (string.IsNullOrWhiteSpace(request.Payload))
                {
                    return Results.BadRequest(new { error = "Payload must not be empty." });
                }

                var requestId = ResolveRequestId(
                    context,
                    request.Payload,
                    options.Value.RequestIdHeaderName);

                var bridgeRequest = new BridgeRequest(requestId, request.Payload);
                var timeout = TimeSpan.FromMilliseconds(
                    options.Value.RequestTimeoutMilliseconds);

                var result = await coordinator.DispatchAsync(bridgeRequest, timeout, token)
                    .ConfigureAwait(false);

                if (result.IsTimeout)
                {
                    var timeoutPayload = new BridgeTimeoutResponse(
                        requestId,
                        "UDP response timeout. Retry with the same payload or request id.");

                    return Results.Json(
                        timeoutPayload,
                        statusCode: StatusCodes.Status504GatewayTimeout);
                }

                var responsePayload = new BridgeHttpResponse(
                    result.RequestId,
                    result.Payload!,
                    result.ServedFromCache,
                    result.ReceivedAtUtc!.Value);

                return Results.Ok(responsePayload);
            });

        _ = app.MapGet(
            "/bridge/{requestId}",
            (string requestId, IResponseCache responseCache) =>
            {
                if (string.IsNullOrWhiteSpace(requestId))
                {
                    return Results.BadRequest(new { error = "Request id must not be empty." });
                }

                if (!responseCache.TryGet(requestId, out var cachedResponse))
                {
                    return Results.NotFound(new { error = "Response not found." });
                }

                var responsePayload = new BridgeHttpResponse(
                    cachedResponse.RequestId,
                    cachedResponse.Payload,
                    true,
                    cachedResponse.ReceivedAtUtc);

                return Results.Ok(responsePayload);
            });
    }

    private static string ResolveRequestId(
        HttpContext context,
        string payload,
        string requestIdHeaderName)
    {
        if (context.Request.Headers.TryGetValue(requestIdHeaderName, out var values))
        {
            var value = values.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return CreateDeterministicRequestId(payload);
    }

    private static string CreateDeterministicRequestId(string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
