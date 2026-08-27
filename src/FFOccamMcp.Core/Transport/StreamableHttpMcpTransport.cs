using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OccamMcp.Core.Transport;

/// <summary>
/// Standard MCP Streamable HTTP transport at <c>/mcp</c> (SDK 2.2 / protocol 2026-07-28).
/// Local-first: defaults to loopback. Custom WSS (<c>--remote</c>) remains a separate advanced path.
/// </summary>
public sealed class StreamableHttpMcpTransport : IMcpTransport
{
    private readonly OccamMcpCli _cli;
    private WebApplication? _app;

    public StreamableHttpMcpTransport(OccamMcpCli cli) => _cli = cli;

    public string ListenUrl => $"http://{FormatListenHost(_cli.BindAddress)}:{_cli.Port}/";

    public string McpEndpoint => $"{ListenUrl.TrimEnd('/')}/mcp";

    public async IAsyncEnumerable<string> ReadRequestsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // SDK owns the request loop via MapMcp.
        await Task.CompletedTask.ConfigureAwait(false);
        yield break;
    }

    public Task SendResponseAsync(string jsonResponse, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls(ListenUrl);
        builder.Logging.SetMinimumLevel(LogLevel.None);

        builder.Services
            .AddOccamMcpServer()
            .WithHttpTransport(options =>
            {
                // Stateless is the 2.2 default and matches 2026-07-28 Streamable HTTP.
                options.Stateless = true;
            });

        var app = builder.Build();
        app.MapMcp("/mcp");
        app.MapGet("/health", () => Results.Text("{\"ok\":true,\"mode\":\"streamable-http\",\"endpoint\":\"/mcp\"}", "application/json"));

        _app = app;
        Console.Error.WriteLine($"streamable_http_mcp_listening: {McpEndpoint}");
        await ((IHost)app).RunAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_app is not null)
        {
            await _app.StopAsync(cancellationToken).ConfigureAwait(false);
            await _app.DisposeAsync().ConfigureAwait(false);
            _app = null;
        }
    }

    private static string FormatListenHost(string bindAddress) =>
        bindAddress.Contains(':', StringComparison.Ordinal) ? $"[{bindAddress}]" : bindAddress;
}
