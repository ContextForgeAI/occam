namespace OccamMcp.Core.Transport;

/// <summary>
/// Strategy-shaped MCP transport seam — JSON-RPC framing only; tool logic stays in MCP SDK handlers.
/// </summary>
public interface IMcpTransport
{
    IAsyncEnumerable<string> ReadRequestsAsync(CancellationToken cancellationToken);

    Task SendResponseAsync(string jsonResponse, CancellationToken cancellationToken);

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}
