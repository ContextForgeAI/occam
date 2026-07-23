using System.Text.Json;
using OccamMcp.Core.Compile;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Tools;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

/// <summary>
/// Prompt 3 — conditional whole-response economy, materialization identity, delta_only.
/// </summary>
internal static class ConditionalEconomyUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        RunMaterializationKey(assert);
        RunUnchangedEnvelope(assert);
        RunDeltaOnlyEnvelope(assert);
        RunBudgetModes(assert);
        Console.WriteLine("L_CONDITIONAL_ECONOMY_OK");
    }

    private static void RunMaterializationKey(Action<string, bool> assert)
    {
        var baseOpts = new OccamTranscodeOptions
        {
            MaxTokens = 1200,
            FitMarkdown = true,
            FocusQuery = "event loop tasks synchronization",
            JsonBlocks = true,
            SemanticChunking = true,
        };

        var k1 = MaterializationKey.Compute("https://docs.python.org/3/library/asyncio.html", "http_then_browser", baseOpts);
        var k2 = MaterializationKey.Compute("https://docs.python.org/3/library/asyncio.html", "http_then_browser", baseOpts);
        assert("mat key: deterministic", k1 == k2);
        assert("mat key: sha256 prefix", k1.StartsWith("sha256:", StringComparison.Ordinal));

        var focusChanged = MaterializationKey.Compute(
            "https://docs.python.org/3/library/asyncio.html",
            "http_then_browser",
            baseOpts with { FocusQuery = "coroutines only" });
        assert("mat key: focus_query changes key", focusChanged != k1);

        var budgetChanged = MaterializationKey.Compute(
            "https://docs.python.org/3/library/asyncio.html",
            "http_then_browser",
            baseOpts with { MaxTokens = 800 });
        assert("mat key: max_tokens changes key (budget is part of materialization)", budgetChanged != k1);

        var playbookChanged = MaterializationKey.Compute(
            "https://docs.python.org/3/library/asyncio.html",
            "http_then_browser",
            baseOpts,
            playbookId: "python-docs",
            playbookVersion: "2");
        assert("mat key: playbook identity changes key", playbookChanged != k1);
    }

    private static void RunUnchangedEnvelope(Action<string, bool> assert)
    {
        const string md = "# Asyncio\n\nThe event loop runs tasks and synchronization primitives.";
        var hash = ContentHashToken.BareHex(md);
        var matKey = MaterializationKey.Compute(
            "https://example.com/asyncio",
            "http",
            new OccamTranscodeOptions { MaxTokens = 1200, FitMarkdown = true, FocusQuery = "event loop", JsonBlocks = true });

        var chunks = new[]
        {
            new WorkerExtractChunkInfo { Text = new string('x', 4000), Headers = ["Asyncio"] },
        };
        var blocks = new[]
        {
            new WorkerExtractBlockInfo { Type = "paragraph", Text = "Changed in version 3.10.", SourceSelector = "#ver" },
            new WorkerExtractBlockInfo { Type = "paragraph", Text = "The event loop runs tasks.", SourceSelector = "#el" },
        };

        // Simulate the tool's omitHeavySidecars path for unchanged:true.
        var resp = new OccamTranscodeSuccessResponse(
            true,
            new OccamTranscodeUrlInfo("https://example.com/asyncio", "https://example.com/asyncio"),
            string.Empty,
            "http",
            [],
            Compile: null,
            Unchanged: true,
            Chunks: null,
            Blocks: null,
            Tables: null,
            Feed: null,
            ContentHash: hash,
            MaterializationKey: matKey,
            Receipt: new OccamTranscodeReceiptInfo(null, null, 0.9, 12, TokenEstimator: TokenEstimator.EstimatorId));

        var json = JsonSerializer.Serialize(resp, OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        assert("unchanged: flag true", root.GetProperty("unchanged").GetBoolean());
        assert("unchanged: empty markdown", root.GetProperty("markdown").GetString() == string.Empty);
        assert("unchanged: no chunks", !root.TryGetProperty("chunks", out _));
        assert("unchanged: no blocks", !root.TryGetProperty("blocks", out _));
        assert("unchanged: no tables", !root.TryGetProperty("tables", out _));
        assert("unchanged: no feed", !root.TryGetProperty("feed", out _));
        assert("unchanged: echoes contentHash", root.GetProperty("contentHash").GetString() == hash);
        assert("unchanged: has materializationKey", root.GetProperty("materializationKey").GetString() == matKey);

        var tokenEst = TokenEstimator.Estimate(json);
        assert("unchanged: response far smaller than sidecars", tokenEst < 400);

        // Option mismatch is NOT source drift: different focus → different mat key; same page hash
        // would be a client error to reuse if_none_match across materializations.
        var otherKey = MaterializationKey.Compute(
            "https://example.com/asyncio",
            "http",
            new OccamTranscodeOptions { MaxTokens = 1200, FitMarkdown = true, FocusQuery = "queues only", JsonBlocks = true });
        assert("option mismatch: materialization key changes", otherKey != matKey);
        // Silence unused (would be sent on a full first response).
        _ = chunks;
        _ = blocks;
    }

    private static void RunDeltaOnlyEnvelope(Action<string, bool> assert)
    {
        var prior = new[]
        {
            new WorkerExtractBlockInfo { Type = "paragraph", Text = "Intro.", SourceSelector = "#a" },
            new WorkerExtractBlockInfo { Type = "paragraph", Text = "Old middle.", SourceSelector = "#b" },
        };
        var current = new[]
        {
            new WorkerExtractBlockInfo { Type = "paragraph", Text = "Intro.", SourceSelector = "#a" },
            new WorkerExtractBlockInfo { Type = "paragraph", Text = "New middle.", SourceSelector = "#b" },
        };
        var priorHashes = prior.Select(BlockDiff.Hash).ToArray();
        var diff = BlockDiff.Compute(current, priorHashes);
        var fullMd = string.Join("\n\n", current.Select(b => b.Text));
        var resp = new OccamTranscodeSuccessResponse(
            true,
            new OccamTranscodeUrlInfo("https://example.com/", null),
            string.Empty,
            "http",
            [],
            Diff: diff,
            ContentHash: ContentHashToken.BareHex(fullMd),
            DeltaOnly: true,
            MaterializationKey: MaterializationKey.Compute(
                "https://example.com/", "http", new OccamTranscodeOptions { JsonBlocks = true }),
            Blocks: null,
            Chunks: null);

        var json = JsonSerializer.Serialize(resp, OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        assert("delta: empty markdown", json.Contains("\"markdown\":\"\"", StringComparison.Ordinal));
        assert("delta: has diff", json.Contains("\"diff\"", StringComparison.Ordinal));
        assert("delta: deltaOnly", json.Contains("\"deltaOnly\":true", StringComparison.Ordinal));
        assert("delta: no blocks sidecar", !json.Contains("\"blocks\"", StringComparison.Ordinal));

        // Reconstruction matches contentHash.
        var priorByHash = prior.ToDictionary(BlockDiff.Hash, b => b.Text, StringComparer.OrdinalIgnoreCase);
        var addedByHash = diff.AddedBlocks.ToDictionary(a => a.Hash, a => a.Text, StringComparer.OrdinalIgnoreCase);
        var reconstructed = string.Join("\n\n", diff.BlockHashes.Select(h =>
            addedByHash.TryGetValue(h, out var t) ? t : priorByHash[h]));
        assert("delta: reconstruction matches hash", ContentHashToken.Matches(reconstructed, resp.ContentHash!));
        assert("delta: tampered reconstruction fails",
            !ContentHashToken.Matches(reconstructed + "x", resp.ContentHash!));
    }

    private static void RunBudgetModes(Action<string, bool> assert)
    {
        var empty = new ResponseBudgetSidecars(null, null, null, null, null, null);
        var full = ResponseBudgetPlanner.AllocateMarkdownCap(512, empty, ResponseBudgetMode.Full);
        assert("budget full: markdown floor > 0", full.MarkdownFloor >= ResponseBudgetPlanner.MinMarkdownTokens);

        var unchanged = ResponseBudgetPlanner.AllocateMarkdownCap(512, empty, ResponseBudgetMode.Unchanged);
        assert("budget unchanged: no markdown floor", unchanged.MarkdownFloor == 0 && unchanged.MarkdownCap == 0);

        var delta = ResponseBudgetPlanner.AllocateMarkdownCap(512, empty, ResponseBudgetMode.DeltaOnly);
        assert("budget delta: no markdown floor", delta.MarkdownFloor == 0 && delta.MarkdownCap == 0);
        assert("budget delta: receipt reserve kept", delta.ReceiptReserve == ResponseBudgetPlanner.ReceiptSkeletonTokens);
    }
}
