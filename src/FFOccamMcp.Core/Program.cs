using System.Text;
using OccamMcp.Core.Batch;
using OccamMcp.Core.Canary;
using OccamMcp.Core.Cascade;
using OccamMcp.Core.Cli;
using OccamMcp.Core.Exam;
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

// Proof-of-read canary verbs (`canary selftest|vectors|smoke|serve`) — no transport, no worker
// spawn, so the protocol can be proven on a bare machine. See PROBE_PROTOCOL.md.
if (CanaryCliVerbs.TryRun(args, out var canaryExit))
{
    return canaryExit;
}

// Capability exam verbs (`exam selftest|tasks`) — grading, tiering and the tier → tool surface
// mapping. See docs/adr/0016-capability-exam.md.
if (ExamCliVerbs.TryRun(args, out var examExit))
{
    return examExit;
}

// Cascade facade verbs (`cascade selftest|run`) — progressive page reader without requiring MCP.
// See docs/adr/0017-cascade-facade.md.
if (CascadeCliVerbs.TryRun(args, out var cascadeExit))
{
    return cascadeExit;
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
    OccamMcpTransportMode.StreamableHttp => new StreamableHttpMcpTransport(cli),
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
