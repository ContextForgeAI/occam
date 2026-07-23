using System.Diagnostics;
using OccamMcp.Core.Abstractions;

namespace OccamMcp.Core.Workers;

/// <summary>Low-level process spawn and output capture.</summary>
public interface IWorkerProcessSpawner
{
    ExtractRunResult SpawnAsync(
        ProcessStartInfo psi,
        int timeoutMs,
        CancellationToken cancellationToken,
        NodeWorkerLifecycle lifecycle);
}