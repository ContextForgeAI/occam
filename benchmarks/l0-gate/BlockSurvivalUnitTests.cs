using OccamMcp.Core.Compile;
using OccamMcp.Core.Receipts;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

/// <summary>
/// Prompt 4 — structured block survival after focus pruning (integrity + usefulness).
/// </summary>
internal static class BlockSurvivalUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        RunAsyncioFocusFixture(assert);
        RunDefinitionFixture(assert);
        RunListDerive(assert);
        RunTruncationDropsFillers(assert);
        RunReceiptIntegrity(assert);
        Console.WriteLine("L_BLOCK_SURVIVAL_OK");
    }

    private static void RunAsyncioFocusFixture(Action<string, bool> assert)
    {
        var blocks = new[]
        {
            new WorkerExtractBlockInfo
            {
                Type = "paragraph",
                Text = "Changed in version 3.10: this page documents asyncio.",
                SourceSelector = "#version",
            },
            new WorkerExtractBlockInfo
            {
                Type = "paragraph",
                Text = "asyncio is a library to write concurrent code using the async/await syntax.",
                SourceSelector = "#def",
            },
            new WorkerExtractBlockInfo
            {
                Type = "paragraph",
                Text = "An event loop runs asynchronous tasks and callbacks, performs network IO, and runs subprocesses.",
                SourceSelector = "#loop",
            },
            new WorkerExtractBlockInfo
            {
                Type = "paragraph",
                Text = "Synchronization primitives include Lock, Event, Condition, Semaphore, and Barrier.",
                SourceSelector = "#sync",
            },
            new WorkerExtractBlockInfo
            {
                Type = "paragraph",
                Text = "Footer — report a problem with this content.",
                SourceSelector = "#footer",
            },
        };

        // Simulated fit_markdown output: emphasis/link reflow on the useful paragraphs; version note
        // survives verbatim; footer dropped.
        const string pruned = """
            # asyncio

            asyncio is a library to write concurrent code using the **async/await** syntax.

            An event loop runs asynchronous tasks and callbacks, performs network IO, and runs subprocesses.

            Synchronization primitives include Lock, Event, Condition, Semaphore, and Barrier.

            Changed in version 3.10: this page documents asyncio.
            """;

        var original = pruned + "\n\nFooter — report a problem with this content.\n\n## Unrelated\n\nQueues are elsewhere.";
        var survivors = BlockReconciler.SurvivingBlocks(blocks, pruned, original);
        assert("asyncio: reconciler keeps useful blocks despite emphasis",
            survivors is { Count: >= 3 }
            && survivors.Any(b => b.SourceSelector == "#def")
            && survivors.Any(b => b.SourceSelector == "#loop"));

        var prioritized = BlockReconciler.PrioritizeForFocus(survivors, "event loop tasks synchronization");
        assert("asyncio: focus prioritizes loop/sync over version note",
            prioritized is { Count: > 0 }
            && prioritized[0].SourceSelector is "#loop" or "#sync" or "#def");

        // Version-note-only set must fail the usefulness gate.
        var versionOnly = survivors!.Where(b => b.SourceSelector == "#version").ToList();
        assert("asyncio: version-note-only is not focus-relevant",
            BlockReconciler.CountFocusRelevant(versionOnly, "event loop tasks synchronization") == 0);
    }

    private static void RunDefinitionFixture(Action<string, bool> assert)
    {
        var blocks = new[]
        {
            new WorkerExtractBlockInfo { Type = "heading", Text = "Widget", SourceSelector = "h1", Level = 1 },
            new WorkerExtractBlockInfo
            {
                Type = "paragraph",
                Text = "A widget is a reusable UI component that encapsulates state and rendering.",
                SourceSelector = "#defn",
            },
            new WorkerExtractBlockInfo
            {
                Type = "paragraph",
                Text = "See also the changelog for release notes.",
                SourceSelector = "#see",
            },
        };
        const string pruned = "# Widget\n\nA widget is a reusable UI component that encapsulates state and rendering.";
        var survivors = BlockReconciler.SurvivingBlocks(blocks, pruned, pruned + "\n\nSee also the changelog for release notes.");
        var focused = BlockReconciler.PrioritizeForFocus(survivors, "widget definition component");
        assert("definition: keeps definition block",
            focused is { Count: >= 1 } && focused.Any(b => b.SourceSelector == "#defn"));
    }

    private static void RunListDerive(Action<string, bool> assert)
    {
        var blocks = new[]
        {
            new WorkerExtractBlockInfo
            {
                Type = "list_item",
                Text = "- alpha task\n- beta queue\n- gamma unrelated filler line that will be pruned",
                SourceSelector = "ul > li",
            },
        };
        const string pruned = "- alpha task\n- beta queue";
        var survivors = BlockReconciler.SurvivingBlocks(blocks, pruned, blocks[0].Text);
        assert("list: derives surviving lines without inventing text",
            survivors is { Count: 1 }
            && survivors[0].Text.Contains("alpha task", StringComparison.Ordinal)
            && survivors[0].Text.Contains("beta queue", StringComparison.Ordinal)
            && !survivors[0].Text.Contains("gamma", StringComparison.Ordinal));
        assert("list: derived text is present in markdown",
            BlockReconciler.NormalizeForMatch(pruned)
                .Contains(BlockReconciler.NormalizeForMatch(survivors![0].Text), StringComparison.Ordinal)
            || survivors[0].Text.Split('\n').All(line =>
                string.IsNullOrWhiteSpace(line)
                || BlockReconciler.NormalizeForMatch(pruned)
                    .Contains(BlockReconciler.NormalizeForMatch(line), StringComparison.Ordinal)));
    }

    private static void RunTruncationDropsFillers(Action<string, bool> assert)
    {
        var available = new[]
        {
            new WorkerExtractBlockInfo
            {
                Type = "paragraph",
                Text = "The event loop schedules coroutines and tasks.",
                SourceSelector = "#relevant",
            },
            new WorkerExtractBlockInfo
            {
                Type = "paragraph",
                Text = "Changed in version 3.8 notes only.",
                SourceSelector = "#filler",
            },
        };
        // Budget kept only the filler (DOM-early / cheap).
        var kept = new[] { available[1] };
        var cleaned = BlockReconciler.DropFocusIrrelevantKeepers(
            kept, available, "event loop coroutines tasks", out var dropped);
        assert("truncation: drops filler-only keepers", cleaned is { Count: 0 } && dropped == 1);
    }

    private static void RunReceiptIntegrity(Action<string, bool> assert)
    {
        var blocks = new[]
        {
            new WorkerExtractBlockInfo { Type = "paragraph", Text = "Alpha kept.", SourceSelector = "#a" },
            new WorkerExtractBlockInfo { Type = "paragraph", Text = "Beta pruned.", SourceSelector = "#b" },
        };
        const string pruned = "Alpha kept.";
        var survivors = BlockReconciler.SurvivingBlocks(blocks, pruned, pruned + "\n\nBeta pruned.");
        (string Text, string? SourceSelector)[] pairs = [.. survivors!.Select(b => (b.Text, (string?)b.SourceSelector))];
        var leaves = MerkleTree.LeafHashesHex(pairs);
        assert("receipt: root matches leaves", MerkleTree.Root(pairs) == MerkleTree.RootFromLeafHashes(leaves));
        assert("receipt: every survivor is in markdown",
            survivors!.All(b => BlockReconciler.NormalizeForMatch(pruned)
                .Contains(BlockReconciler.NormalizeForMatch(b.Text), StringComparison.Ordinal)));
        assert("receipt: pruned block not provable",
            !survivors!.Any(b => b.SourceSelector == "#b"));
    }
}
