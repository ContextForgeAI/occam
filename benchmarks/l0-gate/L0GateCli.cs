namespace OccamMcp.L0Gate;

internal sealed record L0GateOptions(
    bool Visual,
    bool VisualMatrix,
    string? VisualMatrixRegen,
    bool OpenReport,
    bool SmokeOnly,
    bool SmokeFast,
    bool BenchBrowser,
    string? BenchRoundsArg,
    bool BenchCompareSpawn,
    string? Url,
    string Backend,
    string Id,
    bool JsonBlocks,
    bool Traps,
    string? TranslateTo,
    string? ManagedFetch,
    string? Search,
    string? Watch,
    bool Rc1Regression,
    bool PerfAudit,
    bool UnitOnly,
    bool DiscoveryFocusLive,
    bool WorkflowLive);

internal static class L0GateCli
{
    public static L0GateOptions Parse(string[] args)
    {
        return new L0GateOptions(
            Visual: args.Contains("--visual", StringComparer.OrdinalIgnoreCase),
            VisualMatrix: args.Contains("--visual-matrix", StringComparer.OrdinalIgnoreCase),
            VisualMatrixRegen: args.FirstOrDefault(a => a.StartsWith("--visual-matrix-regen=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2)[1],
            OpenReport: args.Contains("--open", StringComparer.OrdinalIgnoreCase),
            SmokeOnly: args.Contains("--smoke-only", StringComparer.OrdinalIgnoreCase),
            SmokeFast: args.Contains("--fast", StringComparer.OrdinalIgnoreCase)
                || string.Equals(Environment.GetEnvironmentVariable("OCCAM_L0_GATE_FAST"), "1", StringComparison.Ordinal),
            BenchBrowser: args.Contains("--bench-browser", StringComparer.OrdinalIgnoreCase),
            BenchRoundsArg: args.FirstOrDefault(a => a.StartsWith("--rounds=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2)[1],
            BenchCompareSpawn: !args.Contains("--no-spawn-compare", StringComparer.OrdinalIgnoreCase),
            Url: args.FirstOrDefault(a => a.StartsWith("--url=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2)[1],
            Backend: args.FirstOrDefault(a => a.StartsWith("--backend=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2)[1]
                ?? "http_then_browser",
            Id: args.FirstOrDefault(a => a.StartsWith("--id=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2)[1]
                ?? "adhoc",
            JsonBlocks: args.Contains("--json-blocks", StringComparer.OrdinalIgnoreCase),
            Traps: args.Contains("--traps", StringComparer.OrdinalIgnoreCase),
            TranslateTo: args.FirstOrDefault(a => a.StartsWith("--translate-to=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2)[1],
            ManagedFetch: args.FirstOrDefault(a => a.StartsWith("--managed-fetch=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2)[1],
            Search: args.FirstOrDefault(a => a.StartsWith("--search=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2)[1],
            Watch: args.FirstOrDefault(a => a.StartsWith("--watch=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2)[1],
            Rc1Regression: args.Contains("--rc1-regression", StringComparer.OrdinalIgnoreCase),
            PerfAudit: args.Contains("--perf-audit", StringComparer.OrdinalIgnoreCase),
            UnitOnly: args.Contains("--unit-only", StringComparer.OrdinalIgnoreCase),
            DiscoveryFocusLive: args.Contains("--discovery-focus-live", StringComparer.OrdinalIgnoreCase),
            WorkflowLive: args.Contains("--workflow-live", StringComparer.OrdinalIgnoreCase));
    }
}
