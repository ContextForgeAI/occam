using OccamMcp.Core.Lifecycle;
using OccamMcp.Core.Routing;

namespace OccamMcp.Rc2Regression;

internal static class PrGLifecycleCases
{
    public static void Run(TestHarness test)
    {
        ProductionTypes(test);
        DualHostTrees(test);
        ShutdownRequiresExactTarget(test);
        OverlapIsDiagnosticOnly(test);
        NoGlobalSingleton(test);
    }

    private static void ProductionTypes(TestHarness test)
    {
        var names = typeof(TranscodeOutcome).Assembly.GetTypes().Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        test.Check("D3", "production HostIdentityDescriptor is available",
            names.Contains("HostIdentityDescriptor")
            && names.Contains("HostIdentity")
            && names.Contains("LifecycleDiagnostics")
            && names.Contains("LocalLifecycleAdapter"),
            $"hasDescriptor={names.Contains("HostIdentityDescriptor")}; hasAdapter={names.Contains("LocalLifecycleAdapter")}");

        var adapter = new LocalLifecycleAdapter(Sample("gateway", 101, 100, "session-a", "rt-gateway"));
        var descriptor = adapter.DescribeSelfDescriptor();
        test.Check("D3", "descriptor exposes runtime, parent, session, and ownership",
            descriptor.RuntimeId == "rt-gateway"
            && descriptor.Pid == 101
            && descriptor.ParentPid == 100
            && descriptor.SessionId == "session-a"
            && descriptor.OwnershipKind == nameof(OwnershipKind.ExternalClient),
            $"runtime={descriptor.RuntimeId}; pid={descriptor.Pid}; parent={descriptor.ParentPid}; session={descriptor.SessionId}; ownership={descriptor.OwnershipKind}");
    }

    private static void DualHostTrees(TestHarness test)
    {
        var registry = new HostIdentityRegistry();
        registry.Add(Sample("gateway", 101, 100, "session-a", "rt-gateway"));
        registry.Add(Sample("dashboard", 201, 200, "session-b", "rt-dashboard"));
        var stopped = registry.TryStop(
            new ShutdownTarget(new RuntimeId("rt-gateway"), 101, 100),
            out var removed,
            out var rejection);
        test.Check("D3", "targeted stop removes only the matching identity",
            stopped
            && removed?.Ownership.OwnerLabel == "gateway"
            && registry.Live.Count == 1
            && registry.Live[0].Ownership.OwnerLabel == "dashboard"
            && rejection is null,
            $"stopped={stopped}; survivor={registry.Live[0].Ownership.OwnerLabel}; rejection={rejection ?? "none"}");
    }

    private static void ShutdownRequiresExactTarget(TestHarness test)
    {
        var adapter = new LocalLifecycleAdapter(Sample("gateway", 101, 100, "session-a", "rt-gateway"));
        var live = new[]
        {
            Sample("gateway", 101, 100, "session-a", "rt-gateway"),
            Sample("dashboard", 201, 200, "session-b", "rt-dashboard"),
        };

        var rejected = !adapter.TryPlanShutdown(
            new ShutdownTarget(new RuntimeId(""), 0),
            live,
            out _,
            out var reasonBlank);
        var wrong = !adapter.TryPlanShutdown(
            new ShutdownTarget(new RuntimeId("rt-missing"), 101),
            live,
            out _,
            out var reasonMissing);
        var ok = adapter.TryPlanShutdown(
            new ShutdownTarget(new RuntimeId("rt-dashboard"), 201, 200),
            live,
            out var matched,
            out var reasonOk);

        test.Check("D3", "shutdown without exact identity is rejected",
            rejected && wrong && reasonBlank is not null && reasonMissing is not null,
            $"blank={reasonBlank}; missing={reasonMissing}");
        test.Check("D3", "exact runtime id + pid plans shutdown without merging trees",
            ok && matched?.Ownership.OwnerLabel == "dashboard" && reasonOk is null,
            $"ok={ok}; matched={matched?.Ownership.OwnerLabel}; rejection={reasonOk ?? "none"}");
    }

    private static void OverlapIsDiagnosticOnly(TestHarness test)
    {
        var self = Sample("gateway", 101, 100, "session-a", "rt-gateway");
        var peer = Sample("gateway", 301, 300, "session-c", "rt-stale") with
        {
            OccamHome = self.OccamHome,
            Transport = self.Transport,
        };
        var adapter = new LocalLifecycleAdapter(self);
        var diagnostics = adapter.Diagnose([peer]);
        test.Check("D3", "overlap warnings diagnose without auto-kill",
            diagnostics.OverlapWarnings.Count == 1
            && diagnostics.ObservedPeers.Count == 1
            && diagnostics.OverlapWarnings[0].Contains("diagnose only", StringComparison.Ordinal),
            $"warnings={diagnostics.OverlapWarnings.Count}; peers={diagnostics.ObservedPeers.Count}");
    }

    private static void NoGlobalSingleton(TestHarness test)
    {
        var names = typeof(TranscodeOutcome).Assembly.GetTypes().Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        test.Check("D3", "no OS-global singleton host lock type is introduced",
            !names.Contains("GlobalHostMutex")
            && !names.Contains("OccamSingletonLock")
            && names.Contains("HostIdentityRegistry"),
            $"singletonAbsent=True; registryPresent={names.Contains("HostIdentityRegistry")}");
    }

    private static HostIdentity Sample(
        string owner,
        int pid,
        int parentPid,
        string session,
        string runtimeId) =>
        new(
            new RuntimeId(runtimeId),
            pid,
            new ParentHostIdentity(parentPid, owner),
            new SessionId(session),
            StartTimestamp.Now(),
            new Ownership(OwnershipKind.ExternalClient, owner),
            @"C:\occam\root",
            @"C:\occam\OccamMcp.Core.exe",
            "1.0.0-rc.2",
            "stdio");
}
