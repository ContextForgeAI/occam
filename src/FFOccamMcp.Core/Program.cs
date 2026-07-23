using System.Text;
using OccamMcp.Core.Batch;
using OccamMcp.Core.Cli;
using OccamMcp.Core.Transport;

AppContext.SetSwitch("System.Console.AllowVirtualTerminalOnWindows", true);
Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

// Public offline verifier verbs (`keys export`, `verify`) — self-contained, no transport / worker
// spawn. A third party pins the host's key and checks receipts without running the MCP host.
if (OccamCliVerbs.TryRun(args, out var verbExit))
{
    return verbExit;
}

var cli = OccamMcpCli.Parse(args);
if (cli.ShowHelp)
{
    OccamMcpCli.WriteUsage(Console.Error);
    return 0;
}

if (!cli.IsValid)
{
    Console.Error.WriteLine($"invalid_arguments: {cli.FailureKind}");
    return 1;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

if (cli.Mode == OccamMcpTransportMode.BatchServer)
{
    try
    {
        await BatchServerHost.RunAsync(cli, cts.Token).ConfigureAwait(false);
        return 0;
    }
    catch (OperationCanceledException)
    {
        return 0;
    }
}

IMcpTransport transport = cli.Mode switch
{
    OccamMcpTransportMode.WebSocket => new WebSocketMcpTransport(cli),
    OccamMcpTransportMode.Remote => new RemoteMcpTransport(cli),
    _ => new StdioMcpTransport(),
};

try
{
    await transport.StartAsync(cts.Token).ConfigureAwait(false);
    return 0;
}
catch (OperationCanceledException)
{
    return 0;
}
finally
{
    await transport.StopAsync(CancellationToken.None).ConfigureAwait(false);
}
