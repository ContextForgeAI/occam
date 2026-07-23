using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Compile;

/// <summary>
/// Factual token spend per payload bucket after <see cref="ResponseBudgetPlanner"/> ran.
/// Sum of bucket fields is the honest whole-response estimate (excludes envelope metadata).
/// </summary>
public sealed record ResponseBudgetAllocation(
    int Total,
    int Markdown,
    int Blocks,
    int Tables,
    int Chunks,
    int Media,
    int Feed,
    int Receipt);

/// <summary>How many structured items were dropped to fit the shared <c>max_tokens</c> budget.</summary>
public sealed record ResponseBudgetDropped(
    int Blocks,
    int Tables,
    int Chunks,
    int Media,
    int FeedItems,
    bool Screenshot);

/// <summary>Input sidecars before structured trim (post-extract).</summary>
public sealed record ResponseBudgetSidecars(
    IReadOnlyList<WorkerExtractBlockInfo>? Blocks,
    IReadOnlyList<WorkerExtractTableInfo>? Tables,
    IReadOnlyList<WorkerExtractChunkInfo>? Chunks,
    IReadOnlyList<MediaRefInfo>? MediaRefs,
    WorkerExtractFeedInfo? Feed,
    string? Screenshot,
    bool ExpectReceipt = true,
    bool IsProjected = false);

/// <summary>Markdown cap chosen before the markdown compiler runs.</summary>
public sealed record ResponseBudgetMarkdownCap(
    int MarkdownCap,
    int Pool,
    int ReceiptReserve,
    int MarkdownFloor);

/// <summary>Result of trimming structured sidecars into the leftover budget after markdown compile.</summary>
public sealed record ResponseBudgetTrimResult(
    IReadOnlyList<WorkerExtractBlockInfo>? Blocks,
    IReadOnlyList<WorkerExtractTableInfo>? Tables,
    IReadOnlyList<WorkerExtractChunkInfo>? Chunks,
    IReadOnlyList<MediaRefInfo>? MediaRefs,
    WorkerExtractFeedInfo? Feed,
    string? Screenshot,
    ResponseBudgetAllocation Allocation,
    ResponseBudgetDropped Dropped,
    bool StructuredTrimmed);

/// <summary>How the whole-response budget treats markdown vs sidecars.</summary>
public enum ResponseBudgetMode
{
    /// <summary>Normal extract: markdown floor + structured share.</summary>
    Full = 0,
    /// <summary>Conditional 304-style: no markdown floor; envelope is near-empty.</summary>
    Unchanged = 1,
    /// <summary>Delta-as-primary: empty markdown; reserve receipt then delta/metadata.</summary>
    DeltaOnly = 2,
}

/// <summary>
/// Whole-response layer of <see cref="BudgetOwnership"/>: distributes a single public
/// <c>max_tokens</c> across markdown + structured payload buckets so the MCP success body cannot
/// silently exceed the caller's token expectation. Surface/semantic retention stays in
/// <c>MaterializationPlanner</c> under the markdown cap this type allocates.
/// </summary>
public static class ResponseBudgetPlanner
{
    public const int MinMarkdownTokens = 128;
    public const int ReceiptSkeletonTokens = 48;
    public const int TokensPerBlockLeaf = 16;

    /// <summary>
    /// Choose the markdown token cap so structured sidecars can claim up to half the pool, while
    /// markdown never falls below a 50% floor (min 128). Unchanged/delta modes allocate no markdown floor.
    /// </summary>
    public static ResponseBudgetMarkdownCap AllocateMarkdownCap(
        int maxTokens,
        ResponseBudgetSidecars sidecars,
        ResponseBudgetMode mode = ResponseBudgetMode.Full)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxTokens, MinMarkdownTokens);

        var receiptReserve = sidecars.ExpectReceipt ? ReceiptSkeletonTokens : 0;

        if (mode is ResponseBudgetMode.Unchanged or ResponseBudgetMode.DeltaOnly)
        {
            // Intentionally empty markdown — do not reserve a markdown floor. Remaining pool goes to
            // receipt (+ delta payload estimated outside this planner for Unchanged).
            var conditionalPool = Math.Max(0, maxTokens - receiptReserve);
            return new ResponseBudgetMarkdownCap(
                MarkdownCap: 0,
                Pool: conditionalPool,
                ReceiptReserve: receiptReserve,
                MarkdownFloor: 0);
        }

        var pool = Math.Max(MinMarkdownTokens, maxTokens - receiptReserve);
        var markdownFloor = Math.Max(MinMarkdownTokens, pool / 2);
        var structuredCeiling = pool - markdownFloor;
        var rawStructured = EstimateStructuredRaw(sidecars);
        var structuredReserve = Math.Min(rawStructured, structuredCeiling);
        var markdownCap = Math.Max(markdownFloor, pool - structuredReserve);
        return new ResponseBudgetMarkdownCap(markdownCap, pool, receiptReserve, markdownFloor);
    }

    /// <summary>
    /// After markdown compile + block reconcile: fill structured buckets from <c>pool - mdTokens</c>.
    /// Screenshot is dropped first when over budget. Greedy prefix: blocks → tables → chunks → media → feed.
    /// </summary>
    public static ResponseBudgetTrimResult TrimStructured(
        int maxTokens,
        ResponseBudgetMarkdownCap caps,
        string compiledMarkdown,
        ResponseBudgetSidecars reconciled)
    {
        var mdTokens = TokenEstimator.Estimate(compiledMarkdown);
        var remaining = Math.Max(0, caps.Pool - mdTokens);

        var dropped = new ResponseBudgetDropped(0, 0, 0, 0, 0, false);
        var structuredTrimmed = false;

        IReadOnlyList<WorkerExtractBlockInfo>? keptBlocks = reconciled.Blocks;
        IReadOnlyList<WorkerExtractTableInfo>? keptTables = reconciled.Tables;
        IReadOnlyList<WorkerExtractChunkInfo>? keptChunks = reconciled.Chunks;
        IReadOnlyList<MediaRefInfo>? keptMedia = reconciled.MediaRefs;
        WorkerExtractFeedInfo? keptFeed = reconciled.Feed;
        string? keptScreenshot = reconciled.Screenshot;

        // Screenshot first — base64 dominates budgets.
        if (keptScreenshot is { Length: > 0 })
        {
            var shotCost = TokenEstimator.Estimate(keptScreenshot);
            if (shotCost > remaining)
            {
                keptScreenshot = null;
                dropped = dropped with { Screenshot = true };
                structuredTrimmed = true;
            }
            else
            {
                remaining -= shotCost;
            }
        }

        (keptBlocks, remaining, var blocksDropped, var blocksCost) =
            TakePrefix(keptBlocks, remaining, EstimateBlock);
        if (blocksDropped > 0)
        {
            dropped = dropped with { Blocks = blocksDropped };
            structuredTrimmed = true;
        }

        (keptTables, remaining, var tablesDropped, var tablesCost) =
            TakePrefix(keptTables, remaining, EstimateTable);
        if (tablesDropped > 0)
        {
            dropped = dropped with { Tables = tablesDropped };
            structuredTrimmed = true;
        }

        (keptChunks, remaining, var chunksDropped, var chunksCost) =
            TakePrefix(keptChunks, remaining, EstimateChunk);
        if (chunksDropped > 0)
        {
            dropped = dropped with { Chunks = chunksDropped };
            structuredTrimmed = true;
        }

        (keptMedia, remaining, var mediaDropped, var mediaCost) =
            TakePrefix(keptMedia, remaining, EstimateMedia);
        if (mediaDropped > 0)
        {
            dropped = dropped with { Media = mediaDropped };
            structuredTrimmed = true;
        }

        var feedCost = 0;
        if (keptFeed is not null)
        {
            var feedResult = TrimFeed(keptFeed, remaining);
            keptFeed = feedResult.Feed;
            feedCost = feedResult.Cost;
            if (feedResult.ItemsDropped > 0 || (feedResult.Feed is null && reconciled.Feed is not null))
            {
                dropped = dropped with { FeedItems = feedResult.ItemsDropped > 0
                    ? feedResult.ItemsDropped
                    : reconciled.Feed!.Items.Length };
                structuredTrimmed = true;
            }
        }

        var mediaTotal = mediaCost + (keptScreenshot is { Length: > 0 }
            ? TokenEstimator.Estimate(keptScreenshot)
            : 0);

        var blockCount = keptBlocks?.Count ?? 0;
        var receiptCost = caps.ReceiptReserve
            + (reconciled.ExpectReceipt ? blockCount * TokensPerBlockLeaf : 0);

        var usedWithoutReceipt = mdTokens + blocksCost + tablesCost + chunksCost + mediaTotal + feedCost;
        var receiptFit = Math.Min(receiptCost, Math.Max(0, maxTokens - usedWithoutReceipt));
        var total = usedWithoutReceipt + receiptFit;

        var allocation = new ResponseBudgetAllocation(
            Total: total,
            Markdown: mdTokens,
            Blocks: blocksCost,
            Tables: tablesCost,
            Chunks: chunksCost,
            Media: mediaTotal,
            Feed: feedCost,
            Receipt: receiptFit);

        return new ResponseBudgetTrimResult(
            Blocks: keptBlocks,
            Tables: keptTables,
            Chunks: keptChunks,
            MediaRefs: keptMedia,
            Feed: keptFeed,
            Screenshot: keptScreenshot,
            Allocation: allocation,
            Dropped: dropped,
            StructuredTrimmed: structuredTrimmed);
    }

    /// <summary>ASCII bar chart of an allocation (for gate / diagnostics).</summary>
    public static string FormatAllocationDiagram(ResponseBudgetAllocation a, int budget)
    {
        static string Bar(int n, int max)
        {
            if (max <= 0 || n <= 0)
            {
                return "";
            }

            var width = Math.Max(1, (int)Math.Round(20.0 * n / max));
            return new string('█', Math.Min(40, width));
        }

        var maxBucket = Math.Max(budget, Math.Max(a.Markdown, Math.Max(a.Blocks,
            Math.Max(a.Tables, Math.Max(a.Chunks, Math.Max(a.Media, Math.Max(a.Feed, a.Receipt)))))));

        return
            $"budget {budget}\n" +
            $"├─ markdown  {a.Markdown,4} {Bar(a.Markdown, maxBucket)}\n" +
            $"├─ blocks    {a.Blocks,4} {Bar(a.Blocks, maxBucket)}\n" +
            $"├─ tables    {a.Tables,4} {Bar(a.Tables, maxBucket)}\n" +
            $"├─ chunks    {a.Chunks,4} {Bar(a.Chunks, maxBucket)}\n" +
            $"├─ media     {a.Media,4} {Bar(a.Media, maxBucket)}\n" +
            $"├─ feed      {a.Feed,4} {Bar(a.Feed, maxBucket)}\n" +
            $"└─ receipt   {a.Receipt,4} {Bar(a.Receipt, maxBucket)}";
    }

    public static int EstimateStructuredRaw(ResponseBudgetSidecars input)
    {
        var cost = 0;
        if (input.Blocks is { Count: > 0 })
        {
            foreach (var b in input.Blocks)
            {
                cost += EstimateBlock(b);
            }
        }

        if (input.Tables is { Count: > 0 })
        {
            foreach (var t in input.Tables)
            {
                cost += EstimateTable(t);
            }
        }

        if (input.Chunks is { Count: > 0 })
        {
            foreach (var c in input.Chunks)
            {
                cost += EstimateChunk(c);
            }
        }

        if (input.MediaRefs is { Count: > 0 })
        {
            foreach (var m in input.MediaRefs)
            {
                cost += EstimateMedia(m);
            }
        }

        if (input.Feed is not null)
        {
            cost += EstimateFeed(input.Feed);
        }

        if (input.Screenshot is { Length: > 0 })
        {
            cost += TokenEstimator.Estimate(input.Screenshot);
        }

        return cost;
    }

    public static int EstimateBlock(WorkerExtractBlockInfo b) =>
        Math.Max(1, TokenEstimator.Estimate(b.Text)
            + TokenEstimator.Estimate(b.SourceSelector)
            + TokenEstimator.Estimate(b.Type));

    public static int EstimateTable(WorkerExtractTableInfo t)
    {
        var cost = TokenEstimator.Estimate(t.Caption) + TokenEstimator.Estimate(t.SourceSelector);
        foreach (var h in t.Headers)
        {
            cost += TokenEstimator.Estimate(h);
        }

        foreach (var row in t.Rows)
        {
            foreach (var cell in row)
            {
                cost += TokenEstimator.Estimate(cell);
            }
        }

        if (t.Records is { Length: > 0 })
        {
            foreach (var r in t.Records)
            {
                cost += TokenEstimator.Estimate(r.Title)
                    + TokenEstimator.Estimate(r.Url)
                    + TokenEstimator.Estimate(r.Author)
                    + TokenEstimator.Estimate(r.Site)
                    + TokenEstimator.Estimate(r.Age);
            }
        }

        return Math.Max(1, cost);
    }

    public static int EstimateChunk(WorkerExtractChunkInfo c)
    {
        var cost = TokenEstimator.Estimate(c.Text);
        foreach (var h in c.Headers)
        {
            cost += TokenEstimator.Estimate(h);
        }

        return Math.Max(1, cost);
    }

    public static int EstimateMedia(MediaRefInfo m) =>
        Math.Max(1, TokenEstimator.Estimate(m.Url)
            + TokenEstimator.Estimate(m.Kind)
            + TokenEstimator.Estimate(m.Alt)
            + TokenEstimator.Estimate(m.ContextHeading)
            + TokenEstimator.Estimate(m.SelectorHint));

    public static int EstimateFeed(WorkerExtractFeedInfo feed)
    {
        var cost = TokenEstimator.Estimate(feed.Title);
        foreach (var item in feed.Items)
        {
            cost += EstimateFeedItem(item);
        }

        return Math.Max(1, cost);
    }

    public static int EstimateFeedItem(WorkerExtractFeedItemInfo item) =>
        Math.Max(1, TokenEstimator.Estimate(item.Title)
            + TokenEstimator.Estimate(item.Link)
            + TokenEstimator.Estimate(item.PublishedAt)
            + TokenEstimator.Estimate(item.SummaryText)
            + TokenEstimator.Estimate(item.SummaryMarkdown)
            + TokenEstimator.Estimate(item.SummaryHtml));

    private static (IReadOnlyList<T>? Kept, int Remaining, int Dropped, int Cost) TakePrefix<T>(
        IReadOnlyList<T>? items,
        int remaining,
        Func<T, int> estimate)
    {
        if (items is null || items.Count == 0)
        {
            return (items, remaining, 0, 0);
        }

        var kept = new List<T>(items.Count);
        var cost = 0;
        foreach (var item in items)
        {
            var itemCost = estimate(item);
            if (itemCost > remaining)
            {
                break;
            }

            kept.Add(item);
            remaining -= itemCost;
            cost += itemCost;
        }

        var dropped = items.Count - kept.Count;
        if (dropped == 0)
        {
            return (items, remaining, 0, cost);
        }

        return (kept.Count == 0 ? null : kept, remaining, dropped, cost);
    }

    private static (WorkerExtractFeedInfo? Feed, int Cost, int ItemsDropped) TrimFeed(
        WorkerExtractFeedInfo feed, int remaining)
    {
        var titleCost = Math.Max(1, TokenEstimator.Estimate(feed.Title));
        if (feed.Items.Length == 0)
        {
            return titleCost <= remaining ? (feed, titleCost, 0) : (null, 0, 0);
        }

        if (titleCost > remaining)
        {
            return (null, 0, feed.Items.Length);
        }

        remaining -= titleCost;
        var keptItems = new List<WorkerExtractFeedItemInfo>(feed.Items.Length);
        var itemsCost = 0;
        foreach (var item in feed.Items)
        {
            var c = EstimateFeedItem(item);
            if (c > remaining)
            {
                break;
            }

            keptItems.Add(item);
            remaining -= c;
            itemsCost += c;
        }

        var dropped = feed.Items.Length - keptItems.Count;
        if (dropped == 0)
        {
            return (feed, titleCost + itemsCost, 0);
        }

        return (
            new WorkerExtractFeedInfo { Title = feed.Title, Items = [.. keptItems] },
            titleCost + itemsCost,
            dropped);
    }
}
