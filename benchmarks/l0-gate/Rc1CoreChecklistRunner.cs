using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Transport;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

/// <summary>
/// Occam Core 1.0 RC checklist — maps the ten release verification areas onto existing
/// unit suites (no new product behavior). Emits <c>RC1_CORE_CHECKLIST_OK</c> / FAIL.
/// </summary>
internal static partial class Rc1CoreChecklistRunner
{
    private static readonly string[] ExpectedCoreTools =
    [
        "occam_client_capabilities",
        "occam_transcode",
        "occam_probe",
        "occam_digest",
        "occam_playbook_resolve",
        "occam_map",
        "occam_playbook_heal",
        "occam_playbook_save",
        "occam_extract_knowledge",
        "occam_search",
        "occam_verify",
        "occam_claim_check",
        "occam_attest",
        "occam_playbook_lint",
        "occam_dataset_export",
    ];

    internal sealed record AreaResult(
        string Area,
        bool Pass,
        int Asserted,
        int Failed,
        string[] EvidenceSuites,
        string[] Failures);

    internal sealed record ChecklistSummary(
        string RunId,
        int Areas,
        int Passed,
        int Failed,
        bool Go,
        string[] Markers,
        List<AreaResult> Results);

    [JsonSerializable(typeof(AreaResult))]
    [JsonSerializable(typeof(ChecklistSummary))]
    [JsonSerializable(typeof(List<AreaResult>))]
    private partial class ChecklistJsonContext : JsonSerializerContext;

    private sealed class SuiteCapture
    {
        public int Asserted { get; private set; }
        public int Failed { get; private set; }
        public List<string> Failures { get; } = [];

        public void Assert(string name, bool ok)
        {
            Asserted++;
            if (!ok)
            {
                Failed++;
                Failures.Add(name);
            }
        }

        public bool Pass => Failed == 0;
    }

    public static ChecklistSummary Run(WorkerPaths paths, string? outDir = null)
    {
        Console.WriteLine("rc1 core checklist: 10 release areas (unit evidence) …");

        // Each suite runs at most once; areas share evidence without re-executing.
        var suites = new Dictionary<string, SuiteCapture>(StringComparer.Ordinal);
        SuiteCapture Capture(string id, Action<Action<string, bool>> body)
        {
            var cap = new SuiteCapture();
            try
            {
                body(cap.Assert);
            }
            catch (Exception ex)
            {
                cap.Assert($"exception:{id}:{ex.GetType().Name}", false);
                cap.Failures.Add($"{id}: {ex.Message}");
            }

            suites[id] = cap;
            var mark = cap.Pass ? "ok" : "FAIL";
            Console.WriteLine($"  suite {id}: {mark} asserted={cap.Asserted} failed={cap.Failed}");
            return cap;
        }

        Capture("mcp_tools_registry", AssertMcpTools);
        Capture("public_mcp_contract", PublicMcpContractUnitTests.Run);
        Capture("l2_transport", L2TransportUnitTests.Run);
        Capture("l1_failure_taxonomy", L1FailureTaxonomyUnitTests.Run);
        Capture("receipts", ReceiptUnitTests.Run);
        Capture("capsule", CapsuleUnitTests.Run);
        Capture("claim_check", ClaimCheckUnitTests.Run);
        Capture("attest", AttestUnitTests.Run);
        Capture("l1b_probe_browser", L1bProbeUnitTests.Run);
        Capture("l0_infra_cache_diff_knowledge", a => L0InfraUnitTests.Run(paths, a));
        Capture("dataset_export", DatasetExportUnitTests.Run);
        Capture("l3_heal_learn", L3HealLearnUnitTests.Run);
        Capture("l4_genome", L4GenomeUnitTests.Run);
        Capture("playbook_lint", PlaybookLintUnitTests.Run);

        AreaResult Area(string area, params string[] suiteIds)
        {
            var asserted = 0;
            var failed = 0;
            var failures = new List<string>();
            foreach (var id in suiteIds)
            {
                if (!suites.TryGetValue(id, out var cap))
                {
                    failed++;
                    failures.Add($"missing suite {id}");
                    continue;
                }

                asserted += cap.Asserted;
                failed += cap.Failed;
                failures.AddRange(cap.Failures.Select(f => $"{id}/{f}"));
            }

            return new AreaResult(area, failed == 0, asserted, failed, suiteIds, [.. failures]);
        }

        var results = new List<AreaResult>
        {
            Area("1_mcp_tools", "mcp_tools_registry", "public_mcp_contract", "l2_transport"),
            Area("2_typed_failures", "l1_failure_taxonomy"),
            Area("3_receipts", "receipts"),
            Area("4_merkle", "receipts", "capsule", "claim_check", "attest"),
            Area("5_browser_fallback", "l1b_probe_browser"),
            Area("6_diff", "public_mcp_contract", "l0_infra_cache_diff_knowledge"),
            Area("7_cache", "l0_infra_cache_diff_knowledge"),
            Area("8_dataset_export", "dataset_export"),
            Area("9_playbooks", "l3_heal_learn", "l4_genome", "playbook_lint"),
            Area("10_knowledge_extraction", "l4_genome", "l0_infra_cache_diff_knowledge"),
        };

        var failedAreas = results.Count(r => !r.Pass);
        var runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var summary = new ChecklistSummary(
            runId,
            results.Count,
            results.Count - failedAreas,
            failedAreas,
            failedAreas == 0,
            [
                failedAreas == 0 ? "RC1_CORE_CHECKLIST_OK" : "RC1_CORE_CHECKLIST_FAIL",
                $"areas_passed={results.Count - failedAreas}/{results.Count}",
            ],
            results);

        var home = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory();
        var dir = outDir ?? Path.Combine(home, "artifacts", "rc1-regression", runId + "-checklist");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "checklist.json");
        File.WriteAllText(path, JsonSerializer.Serialize(summary, ChecklistJsonContext.Default.ChecklistSummary));
        Console.WriteLine($"RC1 checklist → {path}");
        foreach (var r in results)
        {
            var mark = r.Pass ? "PASS" : "FAIL";
            Console.WriteLine(
                $"  {mark} {r.Area} suites=[{string.Join(",", r.EvidenceSuites)}] " +
                $"asserted={r.Asserted} failed={r.Failed}");
            foreach (var f in r.Failures.Take(6))
            {
                Console.WriteLine($"    · {f}");
            }
        }

        Console.WriteLine(summary.Go ? "RC1_CORE_CHECKLIST_OK" : "RC1_CORE_CHECKLIST_FAIL");
        return summary;
    }

    private static void AssertMcpTools(Action<string, bool> assert)
    {
        var names = OccamMcpServerRegistration.OccamToolNames;
        assert("mcp tools count is 15", names.Length == 15);
        assert("mcp tools unique", names.Distinct(StringComparer.Ordinal).Count() == names.Length);
        foreach (var expected in ExpectedCoreTools)
        {
            assert($"mcp tool registered: {expected}", names.Contains(expected, StringComparer.Ordinal));
        }
    }
}
