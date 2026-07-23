namespace OccamMcp.RamStress;

internal enum LeakKind
{
    None,
    DotNetHeap,
    DotNetWorkingSet,
    NodeRss,
    PlaywrightOrphans,
    RamBudget,
}

internal readonly record struct LeakVerdict(
    bool LeakDetected,
    LeakKind Kind,
    int CheckpointPage,
    string Message);

internal sealed class MemoryTrendAnalyzer
{
    private const int DefaultCapacity = 1000;

    private readonly int _ramBudgetMb;
    private readonly MemorySnapshot[] _ring;
    private readonly int _capacity;
    private int _head;
    private int _count;
    private long _peakWorkingSet;
    private long _peakNodeRss;
    private int _peakBrowsers;
    private readonly int[] _checkpoints = [10, 20, 50];

    public MemoryTrendAnalyzer(int ramBudgetMb = 250, int capacity = DefaultCapacity)
    {
        _ramBudgetMb = ramBudgetMb;
        _capacity = Math.Max(4, capacity);
        _ring = new MemorySnapshot[_capacity];
    }

    public IReadOnlyList<MemorySnapshot> Samples => GetOrderedSamples();

    public void Add(MemorySnapshot sample)
    {
        _ring[_head] = sample;
        _head = (_head + 1) % _capacity;
        if (_count < _capacity)
        {
            _count++;
        }

        _peakWorkingSet = Math.Max(_peakWorkingSet, sample.WorkingSetBytes);
        _peakNodeRss = Math.Max(_peakNodeRss, sample.NodeRssBytes);
        _peakBrowsers = Math.Max(_peakBrowsers, sample.BrowserProcessCount);

        if (sample.WorkingSetMb > _ramBudgetMb)
        {
            throw new LeakAbortException(new LeakVerdict(
                true,
                LeakKind.RamBudget,
                sample.PageIndex,
                $"RAM budget exceeded: Working Set {sample.WorkingSetMb:F1} MB > {_ramBudgetMb} MB at page {sample.PageIndex}."));
        }
    }

    public LeakVerdict? EvaluateCheckpoints()
    {
        foreach (var checkpoint in _checkpoints)
        {
            if (_count < checkpoint)
            {
                continue;
            }

            var verdict = EvaluateAt(checkpoint);
            if (verdict is { LeakDetected: true })
            {
                return verdict;
            }
        }

        return null;
    }

    private LeakVerdict? EvaluateAt(int checkpointPage)
    {
        var window = GetOrderedSamples().Take(checkpointPage).ToList();
        if (window.Count < checkpointPage)
        {
            return null;
        }

        var baseline = AverageWorkingSet(window.Take(3));
        var tail = AverageWorkingSet(window.TakeLast(3));
        var wsGrowthMb = (tail - baseline) / (1024.0 * 1024.0);

        var baselineManaged = AverageManaged(window.Take(3));
        var tailManaged = AverageManaged(window.TakeLast(3));
        var managedGrowthMb = (tailManaged - baselineManaged) / (1024.0 * 1024.0);

        var slopeWs = SlopeMbPerPage(window, s => s.WorkingSetBytes);
        var slopeNode = SlopeMbPerPage(window, s => s.NodeRssBytes);

        var firstBrowsers = window.Take(3).Average(s => s.BrowserProcessCount);
        var tailBrowsers = window.TakeLast(3).Average(s => s.BrowserProcessCount);
        var browserDrift = tailBrowsers - firstBrowsers;

        var gcDropMb = (_peakWorkingSet - window[^1].WorkingSetBytes) / (1024.0 * 1024.0);
        var sawGcRecovery = gcDropMb >= 8.0 || wsGrowthMb < 20.0;

        if (slopeWs > 2.5 && wsGrowthMb > 35.0 && managedGrowthMb > 15.0 && !sawGcRecovery)
        {
            return new LeakVerdict(
                true,
                LeakKind.DotNetWorkingSet,
                checkpointPage,
                "Утечка в .NET: Working Set растёт линейно без отката после GC.");
        }

        if (slopeWs > 1.5 && managedGrowthMb > 25.0 && tailManaged > baselineManaged * 1.35)
        {
            return new LeakVerdict(
                true,
                LeakKind.DotNetHeap,
                checkpointPage,
                "Утечка в .NET: Managed Heap не очищается.");
        }

        if (slopeNode > 3.0 && (tail - baseline) > 40 * 1024 * 1024)
        {
            return new LeakVerdict(
                true,
                LeakKind.NodeRss,
                checkpointPage,
                "Утечка в Node: RSS растёт без плато.");
        }

        if (browserDrift >= 2.0 && tailBrowsers >= 2.0)
        {
            return new LeakVerdict(
                true,
                LeakKind.PlaywrightOrphans,
                checkpointPage,
                "Утечка Playwright: процессы Chromium не уничтожаются.");
        }

        return null;
    }

    private List<MemorySnapshot> GetOrderedSamples()
    {
        if (_count == 0)
        {
            return [];
        }

        var rows = new List<MemorySnapshot>(_count);
        var start = _count < _capacity ? 0 : _head;
        for (var i = 0; i < _count; i++)
        {
            rows.Add(_ring[(start + i) % _capacity]);
        }

        return rows;
    }

    private static double AverageWorkingSet(IEnumerable<MemorySnapshot> rows) =>
        rows.Any() ? rows.Average(r => (double)r.WorkingSetBytes) : 0;

    private static double AverageManaged(IEnumerable<MemorySnapshot> rows) =>
        rows.Any() ? rows.Average(r => (double)r.ManagedBytes) : 0;

    private static double SlopeMbPerPage(IReadOnlyList<MemorySnapshot> rows, Func<MemorySnapshot, long> selector)
    {
        if (rows.Count < 4)
        {
            return 0;
        }

        var tail = rows.TakeLast(Math.Min(12, rows.Count)).ToList();
        var n = tail.Count;
        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
        for (var i = 0; i < n; i++)
        {
            var x = i + 1.0;
            var y = selector(tail[i]) / (1024.0 * 1024.0);
            sumX += x;
            sumY += y;
            sumXY += x * y;
            sumX2 += x * x;
        }

        var denom = n * sumX2 - sumX * sumX;
        if (Math.Abs(denom) < 0.0001)
        {
            return 0;
        }

        return (n * sumXY - sumX * sumY) / denom;
    }
}

internal sealed class LeakAbortException(LeakVerdict verdict) : Exception(verdict.Message)
{
    public LeakVerdict Verdict { get; } = verdict;
}
