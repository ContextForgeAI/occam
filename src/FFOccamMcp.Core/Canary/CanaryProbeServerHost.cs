using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OccamMcp.Core.Transport;

namespace OccamMcp.Core.Canary;

/// <summary>
/// Minimal Kestrel host exposing only the canary endpoints.
/// </summary>
/// <remarks>
/// Kept separate from the MCP transports so the proof-of-read protocol can be exercised — and
/// smoke-tested on a machine with no browser and no worker tree — without starting the extraction
/// stack. Binds loopback by default and refuses non-loopback Host/Origin headers unless the operator
/// explicitly opts out, because the probe endpoint mints proof material on request.
/// </remarks>
public sealed class CanaryProbeServerHost : IAsyncDisposable
{
    private readonly CanaryService _canary;
    private readonly string _bindAddress;
    private readonly int _port;
    private readonly bool _enforceLoopbackHeaders;
    private WebApplication? _app;

    /// <summary>Creates a host for <paramref name="canary"/>.</summary>
    /// <param name="canary">Service backing the endpoints.</param>
    /// <param name="port">TCP port to bind; 0 lets the OS choose.</param>
    /// <param name="bindAddress">Interface to bind; defaults to loopback.</param>
    /// <param name="enforceLoopbackHeaders">
    /// When <c>true</c> (default) non-loopback Host/Origin headers are rejected, mitigating
    /// DNS rebinding against a locally bound probe.
    /// </param>
    public CanaryProbeServerHost(
        CanaryService canary,
        int port = 0,
        string bindAddress = "127.0.0.1",
        bool enforceLoopbackHeaders = true)
    {
        _canary = canary ?? throw new ArgumentNullException(nameof(canary));
        ArgumentException.ThrowIfNullOrWhiteSpace(bindAddress);
        ArgumentOutOfRangeException.ThrowIfNegative(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65_535);
        _port = port;
        _bindAddress = bindAddress;
        _enforceLoopbackHeaders = enforceLoopbackHeaders;
    }

    /// <summary>Base URL the server listens on; only meaningful after <see cref="StartAsync"/>.</summary>
    public string ListenUrl { get; private set; } = string.Empty;

    /// <summary>Starts Kestrel and returns once the server is accepting connections.</summary>
    /// <param name="cancellationToken">Cancels startup.</param>
    /// <returns>A task that completes when the server is listening.</returns>
    public async Task<string> StartAsync(CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://{FormatHost(_bindAddress)}:{_port}");
        builder.Logging.SetMinimumLevel(LogLevel.None);

        var app = builder.Build();
        if (_enforceLoopbackHeaders)
        {
            app.UseLoopbackHostOriginGuard();
        }

        app.MapOccamCanary(_canary);
        app.MapGet("/health", (HttpContext context) =>
        {
            CanaryEndpoints.ApplyNoStore(context.Response);
            context.Response.ContentType = "application/json; charset=utf-8";
            return context.Response.WriteAsync("{\"ok\":true,\"mode\":\"canary-probe\"}");
        });

        await app.StartAsync(cancellationToken).ConfigureAwait(false);
        _app = app;

        var bound = app.Urls.FirstOrDefault() ?? $"http://{FormatHost(_bindAddress)}:{_port}";
        ListenUrl = bound.TrimEnd('/');
        return ListenUrl;
    }

    /// <summary>Blocks until the host is shut down (Ctrl+C or <paramref name="cancellationToken"/>).</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task completing on shutdown.</returns>
    public Task WaitForShutdownAsync(CancellationToken cancellationToken = default) =>
        _app is null
            ? Task.CompletedTask
            : ((IHost)_app).WaitForShutdownAsync(cancellationToken);

    /// <summary>URL of the probe document for a session.</summary>
    /// <param name="sessionId">Session id to embed in the path.</param>
    /// <returns>Absolute probe URL.</returns>
    public string ProbeUrl(string sessionId) => $"{ListenUrl}/probe/canary/{Uri.EscapeDataString(sessionId)}";

    /// <summary>URL of the verification endpoint for a session and claimed sentinel.</summary>
    /// <param name="sessionId">Session id to embed in the path.</param>
    /// <param name="sentinel">Claimed sentinel.</param>
    /// <returns>Absolute verify URL.</returns>
    public string VerifyUrl(string sessionId, string sentinel) =>
        $"{ListenUrl}/probe/canary/{Uri.EscapeDataString(sessionId)}/verify?sentinel={Uri.EscapeDataString(sentinel)}";

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_app is null)
        {
            return;
        }

        await _app.StopAsync().ConfigureAwait(false);
        await _app.DisposeAsync().ConfigureAwait(false);
        _app = null;
    }

    private static string FormatHost(string bindAddress) =>
        bindAddress.Contains(':', StringComparison.Ordinal) ? $"[{bindAddress}]" : bindAddress;
}
