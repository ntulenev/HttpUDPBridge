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
    private const int MAX_PAYLOAD_LENGTH = 8_192;
    private const int MAX_REQUEST_ID_LENGTH = 128;

    private const string REQUEST_ID_FORMAT_ERROR =
        "Request id format is invalid. Allowed: letters, digits, '-', '_', '.', ':'.";

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

                if (request.Payload.Length > MAX_PAYLOAD_LENGTH)
                {
                    return Results.Json(
                        new { error = $"Payload exceeds {MAX_PAYLOAD_LENGTH} characters." },
                        statusCode: StatusCodes.Status413PayloadTooLarge);
                }

                if (!TryResolveRequestId(
                    context,
                    request.Payload,
                    options.Value.RequestIdHeaderName,
                    out var requestId,
                    out var validationResult))
                {
                    return validationResult!;
                }

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

                var requestIdValidation = ValidateRequestId(requestId);
                if (requestIdValidation is not null)
                {
                    return requestIdValidation;
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

    private static bool TryResolveRequestId(
        HttpContext context,
        string payload,
        string requestIdHeaderName,
        out string requestId,
        out IResult? validationResult)
    {
        if (context.Request.Headers.TryGetValue(requestIdHeaderName, out var values))
        {
            var value = values.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                var requestIdValidation = ValidateRequestId(value);
                if (requestIdValidation is not null)
                {
                    requestId = string.Empty;
                    validationResult = requestIdValidation;
                    return false;
                }

                requestId = value;
                validationResult = null;
                return true;
            }
        }

        requestId = CreateDeterministicRequestId(payload);
        validationResult = null;
        return true;
    }

    private static IResult? ValidateRequestId(string requestId)
    {
        if (requestId.Length > MAX_REQUEST_ID_LENGTH)
        {
            return Results.Json(
                new { error = $"Request id exceeds {MAX_REQUEST_ID_LENGTH} characters." },
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        if (!IsValidRequestId(requestId))
        {
            return Results.BadRequest(new { error = REQUEST_ID_FORMAT_ERROR });
        }

        return null;
    }

    private static bool IsValidRequestId(string requestId)
    {
        foreach (var symbol in requestId)
        {
            if (char.IsLetterOrDigit(symbol) || symbol is '-' or '_' or '.' or ':')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static string CreateDeterministicRequestId(string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
