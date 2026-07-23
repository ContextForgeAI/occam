using OccamMcp.Core.Abstractions;

namespace OccamMcp.Core.Backends;

/// <summary>
/// Managed third-party extraction backend (Package 3). Off by default; enabled per
/// <c>OCCAM_MANAGED_PROVIDER</c> with per-domain opt-in. Used by the router only as a last-resort
/// escalation after http and browser both fail on an opted-in host.
/// </summary>
public interface IManagedExtractBackend : IExtractBackend
{
    /// <summary>True when the managed backend is enabled AND <paramref name="url"/>'s host is opted in.</summary>
    bool ShouldAttempt(string url);
}
