using OccamMcp.Core.Playbooks;
using OccamMcp.Core.Routing;

namespace OccamMcp.Core.Cascade;

/// <summary>
/// Seams the cascade orchestrator needs from the host. Production wires
/// <see cref="LiveCascadeExtractBackend"/>; tests inject a fake so step logic is proven without workers.
/// </summary>
public interface ICascadeExtractBackend
{
    /// <summary>Resolve a playbook for <paramref name="url"/> (no network required for local seeds).</summary>
    PlaybookSeedResolveResult ResolvePlaybook(string url);

    /// <summary>Run one backend extract under <paramref name="cancellationToken"/>.</summary>
    ValueTask<CascadeExtractAttempt> ExtractAsync(
        string url,
        OccamBackendPolicy policy,
        OccamTranscodeOptions options,
        CancellationToken cancellationToken);

    /// <summary>Whether receipt signing is configured for this host.</summary>
    bool ReceiptsEnabled { get; }

    /// <summary>Optional content-hash stamp for a successful markdown body.</summary>
    string? ComputeContentHash(string markdown);
}
