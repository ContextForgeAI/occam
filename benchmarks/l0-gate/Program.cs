using System.Diagnostics;
using System.Text;
using OccamMcp.Core.Composition;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Services;
using OccamMcp.Core.Workers;
using OccamMcp.L0Gate;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

var options = L0GateCli.Parse(Environment.GetCommandLineArgs().Skip(1).ToArray());
var assert = new L0GateAssert();

// Proxy pool env must not affect gate singleton — l2-egress uses static OCCAM_HTTP_PROXY mocks.
Environment.SetEnvironmentVariable(ProxyRotationSettings.ProxyListVar, null);
Environment.SetEnvironmentVariable(ProxyRotationSettings.ProxyListFileVar, null);

var (paths, pipeline, probe, digest, map) = OccamServiceCollectionExtensions.BuildOccamCore();
Console.WriteLine($"OCCAM_HOME: {WorkerPaths.ResolveOccamHome() ?? "(not found)"}");
Console.WriteLine($"HTTP worker: {paths.HttpExtractScript ?? "(unset)"}");
Console.WriteLine($"Browser worker: {paths.BrowserExtractScript ?? "(unset)"}");

if (options.VisualMatrixRegen is not null)
{
    var runDir = Path.IsPathRooted(options.VisualMatrixRegen)
        ? options.VisualMatrixRegen
        : Path.Combine(WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory(), options.VisualMatrixRegen);
    var indexPath = VisualMatrixWriter.RegenerateIndexFromRunDir(runDir);
    Console.WriteLine($"VISUAL_MATRIX_REGEN: {indexPath}");
    if (options.OpenReport)
    {
        Process.Start(new ProcessStartInfo(indexPath) { UseShellExecute = true });
    }

    return 0;
}

if (options.VisualMatrix)
{
    return VisualMatrixRunner.Run(paths, pipeline, probe, options.OpenReport);
}

assert.Record("workers configured", paths.IsConfigured);

if (options.BenchBrowser)
{
    if (!paths.IsConfigured)
    {
        return 1;
    }

    var rounds = int.TryParse(options.BenchRoundsArg, out var parsedRounds) ? parsedRounds : 3;
    return L0BrowserBenchRunner.Run(pipeline, options.Url, rounds, options.BenchCompareSpawn);
}

string? visualRunDir = options.Visual ? L0ArtifactWriter.CreateRunDirectory() : null;
if (options.Visual)
{
    Console.WriteLine($"visual mode: ON — artifacts → {visualRunDir}");
}

if (options.Rc1Regression)
{
    return Rc1RegressionRunner.Run(pipeline, probe, digest, map, paths);
}

if (options.PerfAudit)
{
    if (!paths.IsConfigured)
    {
        Console.Error.WriteLine("PERF_AUDIT_FAIL: workers not configured");
        return 1;
    }

    return PerfAuditRunner.Run(pipeline);
}

if (options.DiscoveryFocusLive)
{
    if (!paths.IsConfigured)
    {
        Console.Error.WriteLine("DISCOVERY_FOCUS_LIVE_FAIL: workers not configured");
        return 1;
    }

    DiscoveryFocusLiveTests.Run(map, digest, assert.Record);
    if (assert.Failures.Count == 0)
    {
        return 0;
    }

    Console.Error.WriteLine($"L0 gate failed ({assert.Failures.Count}): {string.Join(", ", assert.Failures)}");
    return 1;
}

if (options.WorkflowLive)
{
    return WorkflowLiveUnitTests.Run(assert.Record);
}

if (options.UnitOnly)
{
    // Unit gate only — no live smoke. Used by Occam Core RC preparation.
    if (!options.SmokeOnly)
    {
        L0InfraUnitTests.Run(paths, assert.Record);
        L1aTokenEconomyTests.Run(assert.Record);
        L1bProbeUnitTests.Run(assert.Record);
        L1FailureTaxonomyUnitTests.Run(assert.Record);
        L2DigestUnitTests.Run(assert.Record);
        PublicMcpContractUnitTests.Run(assert.Record);
        McpArgumentBindingGuardUnitTests.Run(assert.Record);
        PublicMcpToolsListLiveTests.Run(assert.Record);
        L2MapUnitTests.Run(assert.Record);
        DiscoveryFocusUnitTests.Run(assert.Record);
        HttpProbeFetcherUnitTests.Run(assert.Record);
        L2SessionUnitTests.Run(assert.Record);
        L2TransportUnitTests.Run(assert.Record);
        L2EgressUnitTests.Run(assert.Record);
        L2MediaRefsUnitTests.Run(assert.Record);
        L3HealLearnUnitTests.Run(assert.Record);
        L4GenomeUnitTests.Run(assert.Record);
        ReceiptUnitTests.Run(assert.Record);
        CapsuleUnitTests.Run(assert.Record);
        ConsensusUnitTests.Run(assert.Record);
        ClaimCheckUnitTests.Run(assert.Record);
        AttestUnitTests.Run(assert.Record);
        MarkdownPrintableEscapesUnitTests.Run(assert.Record);
        PlaybookLintUnitTests.Run(assert.Record);
        DatasetExportUnitTests.Run(assert.Record);
        FailureAtlasUnitTests.Run(assert.Record);
        CliVerbsUnitTests.Run(assert.Record);
        ConditionalEconomyUnitTests.Run(assert.Record);
        BlockSurvivalUnitTests.Run(assert.Record);
        WorkflowFrozenUnitTests.Run(assert.Record);
        WorkflowSecurityUnitTests.Run(assert.Record);
        L8AgentFirstRunner.Run(pipeline, digest, assert.Record);
    }

    return assert.Finish(
        smokeOnly: false,
        smokeFast: false,
        l1bRan: false,
        l1FailureRan: true,
        l2DigestRan: false,
        l2MapRan: false,
        l2SessionRan: false,
        l2TransportRan: false,
        l2EgressRan: false,
        l2MediaRefsRan: true,
        l3HealLearnRan: true,
        l4GenomeRan: true,
        l5BatchRan: false,
        l6BrowserPoolRan: false,
        l7ResourceSafetyRan: false);
}

if (!string.IsNullOrWhiteSpace(options.Url))
{
    return RunAdhoc(options, pipeline, visualRunDir, assert);
}

// occam_search diagnostics: run one query via the configured search backend.
if (!string.IsNullOrWhiteSpace(options.Search))
{
    var searchSvc = OccamServiceCollectionExtensions.BuildSearchService();
    Console.WriteLine($"search: provider={Environment.GetEnvironmentVariable("OCCAM_SEARCH_PROVIDER") ?? "(unset)"} configured={searchSvc.IsConfigured}");
    var so = searchSvc.SearchAsync(options.Search!, 8, CancellationToken.None).GetAwaiter().GetResult();
    Console.WriteLine($"search result: ok={so.Ok} provider={so.Provider} failure={so.FailureCode ?? "-"} count={so.Results.Count} latencyMs={so.LatencyMs}");
    foreach (var r in so.Results.Take(8))
    {
        Console.WriteLine($"  - {r.Title[..Math.Min(60, r.Title.Length)]} :: {r.Url}");
    }
    return so.Ok ? 0 : 1;
}

// occam_watch diagnostics: extract a URL and report the change verdict vs a temp store.
if (!string.IsNullOrWhiteSpace(options.Watch))
{
    var tmpStore = Path.Combine(Path.GetTempPath(), "occam-watch-adhoc.json");
    var watchSvc = new OccamMcp.Core.Watch.WatchService(pipeline, new OccamMcp.Core.Watch.WatchStore(tmpStore), OccamMcp.Core.Receipts.ReceiptSigner.CreateEphemeral());
    OccamMcp.Core.Routing.OccamBackendPolicyParser.TryParse(options.Backend, out var wPolicy);
    var (ws, wf) = watchSvc.Watch(options.Watch!, wPolicy, OccamMcp.Core.Routing.OccamTranscodeOptions.Default, reset: false, includeDiff: true, includeHistory: false, CancellationToken.None);
    if (wf is not null)
    {
        Console.WriteLine($"watch failure: {wf.Code} — {wf.Message}");
        return 1;
    }
    Console.WriteLine($"watch: url={ws!.Url} firstSeen={ws.FirstSeen} changed={ws.Changed} blocks={ws.BlockCount} backend={ws.Backend} hash={ws.ContentHash[..Math.Min(12, ws.ContentHash.Length)]}");
    if (ws.Diff is not null)
    {
        Console.WriteLine($"  diff: added={ws.Diff.AddedBlocks.Length} removed={ws.Diff.RemovedHashes.Length}");
    }
    Console.WriteLine("(run again with the same URL to see changed=false; store: " + tmpStore + ")");
    return 0;
}

// Package 3 managed-adapter diagnostics: directly fetch one URL via the managed backend.
if (!string.IsNullOrWhiteSpace(options.ManagedFetch))
{
    var managed = OccamServiceCollectionExtensions.BuildManagedBackend();
    Console.WriteLine($"managed: provider={Environment.GetEnvironmentVariable("OCCAM_MANAGED_PROVIDER") ?? "(unset)"} ready={managed.IsReady} shouldAttempt={managed.ShouldAttempt(options.ManagedFetch!)}");
    var mr = managed.Extract(options.ManagedFetch!, CancellationToken.None);
    Console.WriteLine($"managed result: ok={mr.Ok} backend={mr.Backend} failure={mr.Failure ?? "-"} latencyMs={mr.LatencyMs} mdlen={(mr.Markdown?.Length ?? 0)}");
    if (mr.Markdown is { Length: > 0 })
    {
        Console.WriteLine("  head: " + mr.Markdown[..Math.Min(160, mr.Markdown.Length)].Replace("\n", " "));
    }
    return mr.Ok ? 0 : 1;
}

// Parity "traps" tier (LT) — opt-in, third-party hosts; not part of fast/default gate.
if (options.Traps)
{
    if (!paths.IsConfigured)
    {
        Console.Error.WriteLine("LT_TRAPS_FAIL: workers not configured");
        return 1;
    }

    return TrapsRunner.Run(pipeline, probe);
}

if (!options.SmokeOnly)
{
    L0InfraUnitTests.Run(paths, assert.Record);
    L1aTokenEconomyTests.Run(assert.Record);
    L1bProbeUnitTests.Run(assert.Record);
    L1FailureTaxonomyUnitTests.Run(assert.Record);
    L2DigestUnitTests.Run(assert.Record);
    PublicMcpContractUnitTests.Run(assert.Record);
    McpArgumentBindingGuardUnitTests.Run(assert.Record);
    PublicMcpToolsListLiveTests.Run(assert.Record);
    L2MapUnitTests.Run(assert.Record);
    DiscoveryFocusUnitTests.Run(assert.Record);
    HttpProbeFetcherUnitTests.Run(assert.Record);
    L2SessionUnitTests.Run(assert.Record);
    L2TransportUnitTests.Run(assert.Record);
    L2EgressUnitTests.Run(assert.Record);
    L2MediaRefsUnitTests.Run(assert.Record);
    L3HealLearnUnitTests.Run(assert.Record);
    L4GenomeUnitTests.Run(assert.Record);
    ReceiptUnitTests.Run(assert.Record);
    CapsuleUnitTests.Run(assert.Record);
    ConsensusUnitTests.Run(assert.Record);
    ClaimCheckUnitTests.Run(assert.Record);
    AttestUnitTests.Run(assert.Record);
    MarkdownPrintableEscapesUnitTests.Run(assert.Record);
    PlaybookLintUnitTests.Run(assert.Record);
    DatasetExportUnitTests.Run(assert.Record);
    FailureAtlasUnitTests.Run(assert.Record);
    CliVerbsUnitTests.Run(assert.Record);
    ConditionalEconomyUnitTests.Run(assert.Record);
    BlockSurvivalUnitTests.Run(assert.Record);
    WorkflowFrozenUnitTests.Run(assert.Record);
    WorkflowSecurityUnitTests.Run(assert.Record);
    L8AgentFirstRunner.Run(pipeline, digest, assert.Record);
}

var l1bLiveRan = false;
var l2DigestRan = false;
var l2MapRan = false;
var l2SessionRan = false;
var l2TransportRan = false;
var l2EgressRan = false;
var l3HealLearnRan = false;
var l4GenomeRan = false;
var l5BatchRan = false;
var l6BrowserPoolRan = false;
var l7ResourceSafetyRan = false;
if (!options.SmokeOnly && !options.SmokeFast)
{
    l1bLiveRan = true;
    L1bProbeRunner.Run(probe, assert.Record);
    l2DigestRan = true;
    L2DigestRunner.Run(digest, assert.Record);
    l2MapRan = true;
    L2MapRunner.Run(map, assert.Record);
    DiscoveryFocusLiveTests.Run(map, digest, assert.Record);
    l2SessionRan = true;
    L2SessionRunner.Run(pipeline, probe, digest, map, assert.Record);
    l2TransportRan = true;
    L2TransportRunner.Run(assert.Record);
    l2EgressRan = true;
    L2EgressRunner.Run(pipeline, assert.Record);
    l3HealLearnRan = true;
    L3HealLearnRunner.Run(paths, assert.Record);
    l4GenomeRan = true;
    L4GenomeRunner.Run(paths, assert.Record);
    l5BatchRan = true;
    L5BatchRunner.Run(paths, assert.Record);
    l6BrowserPoolRan = true;
    L6BrowserPoolRunner.Run(paths, assert.Record);
    l7ResourceSafetyRan = true;
    L7ResourceSafetyRunner.Run(pipeline, assert.Record);
    L9GoldenRunner.Run(pipeline, assert.Record);
}

var corpusPath = L0SmokeRunner.ResolveCorpusPath();
Console.WriteLine($"smoke corpus: {corpusPath}");
if (options.SmokeFast)
{
    Console.WriteLine("smoke tier: FAST (mdn, nginx, not-found — HTTP only)");
}

L0SmokeRunner.Run(corpusPath, pipeline, assert.Record, options.Visual, visualRunDir, options.SmokeFast);

if (options.Visual && visualRunDir is not null && options.OpenReport)
{
    var indexPath = Path.Combine(visualRunDir, "index.html");
    if (File.Exists(indexPath))
    {
        TryOpenFile(indexPath);
    }
}

return assert.Finish(options.SmokeOnly, options.SmokeFast, l1bLiveRan, l1FailureRan: !options.SmokeOnly, l2DigestRan: l2DigestRan, l2MapRan: l2MapRan, l2SessionRan: l2SessionRan, l2TransportRan: l2TransportRan, l2EgressRan: l2EgressRan, l2MediaRefsRan: !options.SmokeOnly, l3HealLearnRan: l3HealLearnRan, l4GenomeRan: l4GenomeRan, l5BatchRan: l5BatchRan, l6BrowserPoolRan: l6BrowserPoolRan, l7ResourceSafetyRan: l7ResourceSafetyRan);

static int RunAdhoc(L0GateOptions options, TranscodePipeline pipeline, string? visualRunDir, L0GateAssert assert)
{
    if (!OccamBackendPolicyParser.TryParse(options.Backend, out var adhocPolicy))
    {
        Console.Error.WriteLine($"Invalid --backend={options.Backend}");
        return 1;
    }

    visualRunDir ??= L0ArtifactWriter.CreateRunDirectory();
    Console.WriteLine($"adhoc: {options.Id} ({options.Url}) backend={options.Backend} json_blocks={options.JsonBlocks}");
    var adhocOptions = options.JsonBlocks
        ? OccamTranscodeOptions.Default with { JsonBlocks = true }
        : OccamTranscodeOptions.Default;
    var adhocResult = pipeline.Transcode(options.Url!, adhocPolicy, adhocOptions, CancellationToken.None);
    if (options.JsonBlocks)
    {
        var blockCount = adhocResult.Blocks?.Count ?? 0;
        Console.WriteLine($"adhoc blocks: {blockCount}");
        foreach (var b in (adhocResult.Blocks ?? []).Take(8))
        {
            Console.WriteLine($"  [{b.Type}] {b.SourceSelector}  links={b.Links.Length}");
        }
    }

    if (!string.IsNullOrWhiteSpace(options.TranslateTo) && adhocResult.Ok && !string.IsNullOrEmpty(adhocResult.Markdown))
    {
        var translator = OccamServiceCollectionExtensions.BuildTranslationService();
        Console.WriteLine($"adhoc translate: to={options.TranslateTo} configured={translator.IsConfigured}");
        var translated = translator.Translate(adhocResult.Markdown!, options.TranslateTo!, out var warn);
        if (translated is not null)
        {
            Console.WriteLine($"adhoc translatedMarkdown ({translated.Length} chars): {translated[..Math.Min(160, translated.Length)]}");
        }
        else
        {
            Console.WriteLine($"adhoc translate skipped — warning={warn}");
        }
    }
    var entry = new L0SmokeCase
    {
        Id = options.Id,
        Url = options.Url!,
        Backend = options.Backend,
        ExpectOk = true,
    };
    var artifact = L0ArtifactWriter.WriteCase(visualRunDir, entry, adhocResult, adhocResult.Ok, []);
    L0ArtifactWriter.PrintConsoleExcerpt(artifact);
    var indexPath = L0ArtifactWriter.WriteIndexHtml(visualRunDir, [artifact]);
    L0ArtifactWriter.WriteLatestPointer(visualRunDir);
    Console.WriteLine($"VISUAL_REPORT: {indexPath}");
    if (options.OpenReport)
    {
        TryOpenFile(indexPath);
    }

    return adhocResult.Ok ? 0 : 1;
}

static void TryOpenFile(string path)
{
    try
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
        });
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Could not open {path}: {ex.Message}");
    }
}
