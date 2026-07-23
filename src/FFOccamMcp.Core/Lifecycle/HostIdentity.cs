using System.Diagnostics;
using System.Reflection;

namespace OccamMcp.Core.Lifecycle;

/// <summary>Stable per-process runtime identity (INV-10). Never a process-name key.</summary>
public readonly record struct RuntimeId(string Value)
{
    public override string ToString() => Value;

    public static RuntimeId CreateNew() =>
        new($"rt-{Guid.NewGuid():N}");

    public static RuntimeId FromEnvironmentOrNew()
    {
        var env = Environment.GetEnvironmentVariable("OCCAM_RUNTIME_ID")?.Trim();
        return string.IsNullOrEmpty(env) ? CreateNew() : new RuntimeId(env);
    }
}

/// <summary>Client/session correlation id for multi-profile coexistence.</summary>
public readonly record struct SessionId(string Value)
{
    public override string ToString() => Value;

    public static SessionId FromEnvironmentOrNew()
    {
        var env = Environment.GetEnvironmentVariable("OCCAM_SESSION_ID")?.Trim();
        return string.IsNullOrEmpty(env)
            ? new SessionId($"sess-{Guid.NewGuid():N}")
            : new SessionId(env);
    }
}

/// <summary>UTC start time of this host identity.</summary>
public readonly record struct StartTimestamp(DateTimeOffset Utc)
{
    public static StartTimestamp Now() => new(DateTimeOffset.UtcNow);
}

/// <summary>Parent process identity (launcher / external client). Diagnostic only.</summary>
public sealed record ParentHostIdentity(
    int Pid,
    string? Label = null);

/// <summary>Who owns desired state for this instance. Host-agnostic — no vendor coupling.</summary>
public enum OwnershipKind
{
    /// <summary>Occam launcher/Core owns the process tree.</summary>
    SelfManaged = 0,
    /// <summary>An external client owns desired state; Occam owns exact descendants only.</summary>
    ExternalClient = 1,
    Unknown = 2,
}

public sealed record Ownership(
    OwnershipKind Kind,
    string? OwnerLabel = null);

/// <summary>
/// Exact host identity. Two profiles with different roots/sessions may coexist.
/// Duplicate detection keys on overlapping identity fields, never process name alone.
/// </summary>
public sealed record HostIdentity(
    RuntimeId RuntimeId,
    int Pid,
    ParentHostIdentity Parent,
    SessionId SessionId,
    StartTimestamp StartedAt,
    Ownership Ownership,
    string OccamHome,
    string BinaryPath,
    string? Version = null,
    string Transport = "stdio");

/// <summary>
/// Read-only doctor/diagnostics projection of <see cref="HostIdentity"/>.
/// Named for the PR-A production gap check (<c>HostIdentityDescriptor</c>).
/// </summary>
public sealed record HostIdentityDescriptor(
    string RuntimeId,
    int Pid,
    int ParentPid,
    string? ParentLabel,
    string SessionId,
    string StartedAtUtc,
    string OwnershipKind,
    string? OwnerLabel,
    string OccamHome,
    string BinaryPath,
    string? Version,
    string Transport)
{
    public static HostIdentityDescriptor From(HostIdentity identity) =>
        new(
            identity.RuntimeId.Value,
            identity.Pid,
            identity.Parent.Pid,
            identity.Parent.Label,
            identity.SessionId.Value,
            identity.StartedAt.Utc.ToString("O"),
            identity.Ownership.Kind.ToString(),
            identity.Ownership.OwnerLabel,
            identity.OccamHome,
            identity.BinaryPath,
            identity.Version,
            identity.Transport);
}

/// <summary>Exact shutdown selector. Bulk/process-name kills are rejected by the adapter.</summary>
public sealed record ShutdownTarget(
    RuntimeId RuntimeId,
    int Pid,
    int? ParentPid = null);

/// <summary>Read-only multi-instance diagnosis. Overlaps warn; they never auto-kill.</summary>
public sealed record LifecycleDiagnostics(
    HostIdentityDescriptor Self,
    IReadOnlyList<HostIdentityDescriptor> ObservedPeers,
    IReadOnlyList<string> OverlapWarnings);

/// <summary>
/// Host-agnostic lifecycle boundary. External clients (including Hermes, if/when a stable API
/// exists) adapt here — Occam does not invent vendor callbacks.
/// </summary>
public interface ILifecycleAdapter
{
    HostIdentity DescribeSelf();

    HostIdentityDescriptor DescribeSelfDescriptor();

    LifecycleDiagnostics Diagnose(IReadOnlyList<HostIdentity> observedPeers);

    /// <summary>
    /// Plans a targeted shutdown. Returns false when the target does not match an exact identity
    /// (never falls back to process-name-wide termination).
    /// </summary>
    bool TryPlanShutdown(
        ShutdownTarget target,
        IReadOnlyList<HostIdentity> live,
        out HostIdentity? matched,
        out string? rejectionReason);
}

/// <summary>In-process identity registry used by diagnostics and PR-G tests (INV-10).</summary>
public sealed class HostIdentityRegistry
{
    private readonly List<HostIdentity> _live = [];

    public IReadOnlyList<HostIdentity> Live => _live;

    public void Add(HostIdentity identity) => _live.Add(identity);

    public bool TryStop(ShutdownTarget target, out HostIdentity? removed, out string? rejectionReason)
    {
        removed = null;
        rejectionReason = null;
        var matches = _live
            .Where(item =>
                item.RuntimeId.Value == target.RuntimeId.Value
                && item.Pid == target.Pid
                && (target.ParentPid is null || item.Parent.Pid == target.ParentPid))
            .ToList();
        if (matches.Count == 0)
        {
            rejectionReason = "shutdown target does not match any live identity";
            return false;
        }

        if (matches.Count > 1)
        {
            rejectionReason = "shutdown target is ambiguous across live identities";
            return false;
        }

        removed = matches[0];
        _live.Remove(removed);
        return true;
    }

    /// <summary>Owner-label convenience used only when RuntimeId is unavailable in legacy spikes.</summary>
    public bool TryStopByRuntimeId(RuntimeId runtimeId, out HostIdentity? removed, out string? rejectionReason)
    {
        removed = null;
        rejectionReason = null;
        var matches = _live.Where(item => item.RuntimeId.Value == runtimeId.Value).ToList();
        if (matches.Count != 1)
        {
            rejectionReason = matches.Count == 0
                ? "runtime id not found"
                : "runtime id matches multiple live identities";
            return false;
        }

        return TryStop(
            new ShutdownTarget(matches[0].RuntimeId, matches[0].Pid, matches[0].Parent.Pid),
            out removed,
            out rejectionReason);
    }
}

public sealed class LocalLifecycleAdapter : ILifecycleAdapter
{
    private readonly HostIdentity _self;

    public LocalLifecycleAdapter(HostIdentity? self = null) =>
        _self = self ?? CaptureSelf();

    public HostIdentity DescribeSelf() => _self;

    public HostIdentityDescriptor DescribeSelfDescriptor() => HostIdentityDescriptor.From(_self);

    public LifecycleDiagnostics Diagnose(IReadOnlyList<HostIdentity> observedPeers)
    {
        var peers = observedPeers
            .Where(peer => peer.RuntimeId.Value != _self.RuntimeId.Value || peer.Pid != _self.Pid)
            .Select(HostIdentityDescriptor.From)
            .ToArray();
        var warnings = DetectOverlapWarnings(_self, observedPeers);
        return new LifecycleDiagnostics(DescribeSelfDescriptor(), peers, warnings);
    }

    public bool TryPlanShutdown(
        ShutdownTarget target,
        IReadOnlyList<HostIdentity> live,
        out HostIdentity? matched,
        out string? rejectionReason)
    {
        matched = null;
        rejectionReason = null;
        if (string.IsNullOrWhiteSpace(target.RuntimeId.Value) || target.Pid <= 0)
        {
            rejectionReason = "shutdown requires an exact runtime id and pid";
            return false;
        }

        var matches = live
            .Where(item =>
                item.RuntimeId.Value == target.RuntimeId.Value
                && item.Pid == target.Pid
                && (target.ParentPid is null || item.Parent.Pid == target.ParentPid))
            .ToList();
        if (matches.Count == 0)
        {
            rejectionReason = "no live identity matches the shutdown target";
            return false;
        }

        if (matches.Count > 1)
        {
            rejectionReason = "shutdown target is ambiguous";
            return false;
        }

        matched = matches[0];
        return true;
    }

    public static HostIdentity CaptureSelf(
        string? ownerLabel = null,
        OwnershipKind ownershipKind = OwnershipKind.SelfManaged,
        string transport = "stdio")
    {
        var current = Process.GetCurrentProcess();
        var parentPid = 0;
        var parentPidEnv = Environment.GetEnvironmentVariable("OCCAM_PARENT_PID")?.Trim();
        if (int.TryParse(parentPidEnv, out var parsedParent) && parsedParent > 0)
        {
            parentPid = parsedParent;
        }

        var home = Environment.GetEnvironmentVariable("OCCAM_HOME")?.Trim();
        if (string.IsNullOrEmpty(home))
        {
            home = AppContext.BaseDirectory;
        }

        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString();

        var binaryPath = "unknown";
        try
        {
            binaryPath = current.MainModule?.FileName
                ?? Environment.ProcessPath
                ?? "unknown";
        }
        catch
        {
            binaryPath = Environment.ProcessPath ?? "unknown";
        }

        var parentLabel = Environment.GetEnvironmentVariable("OCCAM_PARENT_LABEL")?.Trim();
        var envOwner = Environment.GetEnvironmentVariable("OCCAM_OWNER_LABEL")?.Trim();
        return new HostIdentity(
            RuntimeId.FromEnvironmentOrNew(),
            current.Id,
            new ParentHostIdentity(parentPid, string.IsNullOrEmpty(parentLabel) ? null : parentLabel),
            SessionId.FromEnvironmentOrNew(),
            StartTimestamp.Now(),
            new Ownership(
                ownershipKind,
                string.IsNullOrEmpty(ownerLabel)
                    ? (string.IsNullOrEmpty(envOwner) ? null : envOwner)
                    : ownerLabel),
            Path.GetFullPath(home),
            binaryPath,
            version,
            transport);
    }

    internal static IReadOnlyList<string> DetectOverlapWarnings(
        HostIdentity self,
        IReadOnlyList<HostIdentity> observed)
    {
        var warnings = new List<string>();
        foreach (var peer in observed)
        {
            if (peer.RuntimeId.Value == self.RuntimeId.Value && peer.Pid == self.Pid)
            {
                continue;
            }

            if (string.Equals(peer.OccamHome, self.OccamHome, StringComparison.OrdinalIgnoreCase)
                && string.Equals(peer.Transport, self.Transport, StringComparison.OrdinalIgnoreCase)
                && peer.Ownership.OwnerLabel is not null
                && self.Ownership.OwnerLabel is not null
                && string.Equals(peer.Ownership.OwnerLabel, self.Ownership.OwnerLabel, StringComparison.Ordinal))
            {
                warnings.Add(
                    $"overlap: same occamHome+transport+ownerLabel across runtimeIds "
                    + $"{self.RuntimeId.Value} and {peer.RuntimeId.Value} — diagnose only; do not global-kill");
            }
        }

        return warnings;
    }
}
