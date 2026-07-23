using System.Runtime.CompilerServices;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OccamMcp.Core.Transport;

/// <summary>
/// Local WebSocket MCP listener skeleton — 127.0.0.1, single client, text frames.
/// </summary>
public sealed class WebSocketMcpTransport : IMcpTransport
{
    private readonly OccamMcpCli _cli;
    private readonly Channel<string> _inbound = Channel.CreateUnbounded<string>();
    private WebApplication? _app;

    public WebSocketMcpTransport(OccamMcpCli cli) => _cli = cli;

    public string ListenUrl => $"http://{_cli.BindAddress}:{_cli.Port}/";

    public async IAsyncEnumerable<string> ReadRequestsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await _inbound.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (_inbound.Reader.TryRead(out var frame))
            {
                yield return frame;
            }
        }
    }

    public Task SendResponseAsync(string jsonResponse, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls(ListenUrl);
        builder.Logging.SetMinimumLevel(LogLevel.None);
        builder.Services.AddOccamMcpServer();

        var app = builder.Build();
        app.UseWebSockets();

        app.Map("/", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
            await RunSingleSessionAsync(socket, context.RequestAborted).ConfigureAwait(false);
        });

        _app = app;
        await app.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _inbound.Writer.TryComplete();
        if (_app is not null)
        {
            await _app.StopAsync(cancellationToken).ConfigureAwait(false);
            await _app.DisposeAsync().ConfigureAwait(false);
            _app = null;
        }
    }

    private static async Task RunSingleSessionAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        using var input = new WebSocketMcpInputStream(socket);
        using var output = new WebSocketMcpOutputStream(socket);
        var hostBuilder = Host.CreateApplicationBuilder();
        hostBuilder.Logging.SetMinimumLevel(LogLevel.None);
        hostBuilder.Services.AddOccamMcpServer().WithStreamServerTransport(input, output);
        using var host = hostBuilder.Build();
        await host.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    public static bool IsListeningOnLocalhost(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return false;
        }
        catch (SocketException)
        {
            return true;
        }
    }
}
