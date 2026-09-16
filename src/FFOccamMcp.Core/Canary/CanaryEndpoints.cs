using System.Buffers;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace OccamMcp.Core.Canary;

/// <summary>
/// HTTP surface for the canary: one endpoint that serves a probe document, one that adjudicates a
/// claim. Both are rate limited and both refuse to be cached.
/// </summary>
/// <remarks>
/// JSON is written with <see cref="Utf8JsonWriter"/> rather than a serializer so the endpoints stay
/// reflection-free under Native AOT, matching the rest of the host.
/// </remarks>
public static class CanaryEndpoints
{
    /// <summary>Route prefix for the probe document.</summary>
    public const string ProbeRoute = "/probe/canary/{sessionId}";

    /// <summary>Route for the verification oracle.</summary>
    public const string VerifyRoute = "/probe/canary/{sessionId}/verify";

    /// <summary>
    /// Maps both canary endpoints onto <paramref name="routes"/>.
    /// </summary>
    /// <param name="routes">Endpoint route builder to extend.</param>
    /// <param name="canary">Service backing the endpoints.</param>
    /// <returns><paramref name="routes"/>, for chaining.</returns>
    public static IEndpointRouteBuilder MapOccamCanary(this IEndpointRouteBuilder routes, CanaryService canary)
    {
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(canary);

        routes.MapGet(ProbeRoute, (HttpContext context, string sessionId) =>
        {
            ApplyNoStore(context.Response);

            if (!CanarySentinel.IsValidSessionId(sessionId, canary.Options.MaxSessionIdLength))
            {
                return WriteError(context, StatusCodes.Status400BadRequest, "invalid_session_id");
            }

            var decision = canary.TryAcquire(sessionId, ClientIdentifier(context));
            if (!decision.Allowed)
            {
                return WriteRateLimited(context, decision);
            }

            var issue = canary.Issue(
                sessionId,
                ClientIdentifier(context),
                context.Request.Headers.UserAgent.ToString());

            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = CanaryPage.ContentType;
            return context.Response.WriteAsync(CanaryPage.Render(issue));
        });

        routes.MapGet(VerifyRoute, (HttpContext context, string sessionId, string? sentinel) =>
        {
            ApplyNoStore(context.Response);

            var decision = canary.TryAcquire(
                CanarySentinel.IsValidSessionId(sessionId, canary.Options.MaxSessionIdLength) ? sessionId : "invalid",
                ClientIdentifier(context));
            if (!decision.Allowed)
            {
                return WriteRateLimited(context, decision);
            }

            var verification = canary.Verify(sessionId, sentinel);
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json; charset=utf-8";
            return context.Response.WriteAsync(WriteVerification(verification));
        });

        return routes;
    }

    /// <summary>
    /// Serialises a verdict to the wire JSON shape. Deliberately omits the sentinel: echoing it back
    /// would turn the verifier into a sentinel dispenser.
    /// </summary>
    /// <param name="verification">Verdict to serialise.</param>
    /// <returns>Compact JSON.</returns>
    public static string WriteVerification(CanaryVerification verification)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteBoolean("ok", verification.IsReadEvidence);
            writer.WriteString("verdict", verification.VerdictWire);
            writer.WriteString("sessionId", verification.SessionId);
            writer.WriteNumber("currentBucket", verification.CurrentBucket);
            if (verification.MatchedBucket is { } matched)
            {
                writer.WriteNumber("matchedBucket", matched);
            }
            else
            {
                writer.WriteNull("matchedBucket");
            }

            if (verification.BucketDistance is { } distance)
            {
                writer.WriteNumber("bucketDistance", distance);
            }
            else
            {
                writer.WriteNull("bucketDistance");
            }

            writer.WriteString("detail", verification.Detail);
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>
    /// Applies the no-store header set. A cached probe document would hand out a stale sentinel and
    /// silently convert a genuine read into <see cref="CanaryVerdict.ReadStale"/>, so the headers are
    /// part of the protocol rather than a nicety (PROBE_PROTOCOL.md §4.2).
    /// </summary>
    /// <param name="response">Response to annotate.</param>
    public static void ApplyNoStore(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        response.Headers.Pragma = "no-cache";
        response.Headers.Expires = "0";
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers["Referrer-Policy"] = "no-referrer";
    }

    private static string? ClientIdentifier(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString();

    private static Task WriteRateLimited(HttpContext context, CanaryRateDecision decision)
    {
        var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(decision.RetryAfter.TotalSeconds));
        context.Response.Headers.RetryAfter = retryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return WriteError(context, StatusCodes.Status429TooManyRequests, "rate_limited");
    }

    private static Task WriteError(HttpContext context, int statusCode, string error)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        return context.Response.WriteAsync($"{{\"ok\":false,\"error\":\"{error}\"}}");
    }
}
