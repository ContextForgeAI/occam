using OccamMcp.Core.Abstractions;

namespace OccamMcp.Core.Workers;

/// <summary>Runs browser extract operations (pool + one-shot).</summary>
public interface IBrowserExtractRunner
{
   ValueTask<ExtractRunResult> RunAsync(
       string scriptPath,
       ExtractOptions options,
       int timeoutMs,
       CancellationToken cancellationToken);
}