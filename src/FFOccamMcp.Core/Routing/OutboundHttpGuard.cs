using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace OccamMcp.Core.Routing;

/// <summary>
/// SSRF guard for the C# HttpClients that fetch user-influenced URLs directly (probe, robots.txt,
/// well-known genome) — i.e. the fetch paths that do NOT go through the Node workers. It resolves the
/// target host, rejects any private address across <b>both</b> IPv4 and IPv6, and pins the connection
/// to exactly the validated addresses so a re-resolve can't rebind to an internal target. This mirrors
/// the worker-side guard in <c>workers/shared/lib/private-ip.mjs</c> and closes the hostname-bypass /
/// redirect-SSRF gap left by the literal-IP-only preflight in <see cref="PrivacyClassifier"/>.
/// Wired in as <see cref="SocketsHttpHandler.ConnectCallback"/>; honors OCCAM_ALLOW_PRIVATE_URLS.
/// </summary>
public static class OutboundHttpGuard
{
    /// <summary>
    /// Resolves <paramref name="host"/> (literal IP or DNS, both families) and rejects any private
    /// answer unless private URLs are explicitly allowed. Returns the validated addresses to connect to.
    /// </summary>
    /// <exception cref="OutboundUrlBlockedException">Host resolves to a private address, or cannot be resolved.</exception>
    public static async ValueTask<IPAddress[]> ResolveAndValidateAsync(string host, CancellationToken cancellationToken)
    {
        IPAddress[] addresses;
        if (IPAddress.TryParse(host, out var literal))
        {
            addresses = [literal];
        }
        else
        {
            try
            {
                addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException)
            {
                throw new OutboundUrlBlockedException(host, "dns_error");
            }
        }

        if (addresses.Length == 0)
        {
            throw new OutboundUrlBlockedException(host, "dns_error");
        }

        if (PrivacyClassifier.IsPrivateUrlBlocked())
        {
            foreach (var address in addresses)
            {
                if (PrivacyClassifier.IsPrivateIp(address))
                {
                    throw new OutboundUrlBlockedException(host, "private_url_blocked");
                }
            }
        }

        return addresses;
    }

    /// <summary>SocketsHttpHandler.ConnectCallback: validate the host, then connect pinned to the checked IPs.</summary>
    public static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var addresses = await ResolveAndValidateAsync(context.DnsEndPoint.Host, cancellationToken).ConfigureAwait(false);

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken).ConfigureAwait(false);
            // Return the raw transport stream — SocketsHttpHandler layers TLS on top for https.
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}

/// <summary>Thrown by <see cref="OutboundHttpGuard"/> when an outbound fetch target is private or unresolvable.</summary>
public sealed class OutboundUrlBlockedException(string host, string failureCode)
    : Exception($"Outbound fetch blocked ({failureCode}): {host}.")
{
    public string FailureCode { get; } = failureCode;
}
