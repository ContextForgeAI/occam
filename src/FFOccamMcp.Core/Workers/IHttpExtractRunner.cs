using OccamMcp.Core.Abstractions;

namespace OccamMcp.Core.Workers;

/// <summary>Runs HTTP extract operations (daemon + one-shot).</summary>
public interface IHttpExtractRunner
{
    // C2: genuinely async now. This returned a value synchronously while being named RunAsync — the misnomer
    // hid that the http path blocked a thread (in NodeWorkerOutputCapture) for the whole child-process life.
    ValueTask<ExtractRunResult> RunAsync(
        string scriptPath,
        ExtractOptions options,
        int timeoutMs,
        CancellationToken cancellationToken);
}