using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OccamMcp.Core.Transport;

/// <summary>
/// Thin adapter over MCP SDK stdio transport — behavior parity with pre-P2-4 <c>Program.cs</c>.
/// </summary>
public sealed class StdioMcpTransport : IMcpTransport
{
    // P1-5: Bounded channel to prevent unbounded memory growth under backpressure
    private readonly Channel<string> _outbound = Channel.CreateBounded<string>(
        new BoundedChannelOptions(32)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
    private IHost? _host;

    public async IAsyncEnumerable<string> ReadRequestsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await _outbound.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (_outbound.Reader.TryRead(out var line))
            {
                yield return line;
            }
        }
    }

    public Task SendResponseAsync(string jsonResponse, CancellationToken cancellationToken) =>
        _outbound.Writer.WriteAsync(jsonResponse, cancellationToken).AsTask();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.None);
        builder.Services.AddOccamMcpServer().WithStdioServerTransport();
        _host = builder.Build();
        await _host.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _outbound.Writer.TryComplete();
        if (_host is not null)
        {
            await _host.StopAsync(cancellationToken).ConfigureAwait(false);
            _host.Dispose();
            _host = null;
        }
    }
}
