using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Abstractions;

public interface IExtractBackend
{
    string Name { get; }
    bool IsReady { get; }
    // C2: async end-to-end — a blocking Extract() pinned a thread for the whole worker run,
    // and the digest fanned that out under Parallel.For.
    ValueTask<ExtractRunResult> ExtractAsync(string url, CancellationToken cancellationToken);
}
