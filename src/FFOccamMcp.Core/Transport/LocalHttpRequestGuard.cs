using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace OccamMcp.Core.Transport;

/// <summary>
/// Host/Origin checks for loopback-only MCP HTTP transports (Streamable HTTP, local WS).
/// Complements bind-address enforcement; mitigates DNS-rebinding / confused-deputy browser hits.
/// </summary>
public static class LocalHttpRequestGuard
{
    /// <summary>ASP.NET middleware: reject non-loopback Host or non-loopback Origin.</summary>
    public static void UseLoopbackHostOriginGuard(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (!IsAllowed(context.Request))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    "{\"ok\":false,\"error\":\"host_or_origin_not_loopback\"}").ConfigureAwait(false);
                return;
            }

            await next().ConfigureAwait(false);
        });

    /// <summary>True when Host is loopback (port ignored) and Origin is absent or loopback.</summary>
    public static bool IsAllowed(HttpRequest request)
    {
        if (!IsLoopbackHostHeader(request.Headers.Host.ToString()))
        {
            return false;
        }

        var origin = request.Headers.Origin.ToString();
        if (string.IsNullOrWhiteSpace(origin))
        {
            return true;
        }

        return IsLoopbackOrigin(origin);
    }

    public static bool IsLoopbackHostHeader(string? hostHeader)
    {
        if (string.IsNullOrWhiteSpace(hostHeader))
        {
            return false;
        }

        var host = hostHeader.Trim();
        var colon = host.LastIndexOf(':');
        if (colon > 0 && host[0] != '[')
        {
            // hostname:port
            host = host[..colon];
        }
        else if (host.StartsWith("[", StringComparison.Ordinal))
        {
            var end = host.IndexOf(']');
            if (end > 1)
            {
                host = host[1..end];
            }
        }

        return IsLoopbackHostName(host);
    }

    public static bool IsLoopbackOrigin(string origin)
    {
        if (!Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        return IsLoopbackHostName(uri.Host);
    }

    public static bool IsLoopbackHostName(string host) =>
        host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.Equals("::1", StringComparison.OrdinalIgnoreCase)
        || host.Equals("[::1]", StringComparison.OrdinalIgnoreCase);
}
