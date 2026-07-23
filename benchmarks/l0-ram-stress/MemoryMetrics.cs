using System.Diagnostics;
using System.Text;

namespace OccamMcp.RamStress;

internal readonly record struct MemorySnapshot(
    int PageIndex,
    string Url,
    string Phase,
    long ManagedBytes,
    long WorkingSetBytes,
    long NodeRssBytes,
    int NodeProcessCount,
    int ChromeProcessCount,
    int ChromiumProcessCount,
    int MsEdgeProcessCount,
    DateTimeOffset CapturedAt)
{
    public double ManagedMb => ManagedBytes / (1024.0 * 1024.0);
    public double WorkingSetMb => WorkingSetBytes / (1024.0 * 1024.0);
    public double NodeRssMb => NodeRssBytes / (1024.0 * 1024.0);
    public int BrowserProcessCount => ChromeProcessCount + ChromiumProcessCount + MsEdgeProcessCount;

    public static MemorySnapshot Capture(int pageIndex, string url, string phase)
    {
        var managed = GC.GetTotalMemory(forceFullCollection: false);
        using var self = Process.GetCurrentProcess();
        self.Refresh();

        long nodeRss = 0;
        var nodeCount = 0;
        foreach (var proc in Process.GetProcessesByName("node"))
        {
            try
            {
                proc.Refresh();
                if (proc.HasExited)
                {
                    continue;
                }

                nodeRss += proc.WorkingSet64;
                nodeCount++;
            }
            catch
            {
                // process exited between enumerate and refresh
            }
            finally
            {
                proc.Dispose();
            }
        }

        var chrome = CountPlaywrightProcesses("chrome");
        var chromium = CountPlaywrightProcesses("chromium");
        var msedge = CountPlaywrightProcesses("msedge");

        return new MemorySnapshot(
            pageIndex,
            url,
            phase,
            managed,
            self.WorkingSet64,
            nodeRss,
            nodeCount,
            chrome,
            chromium,
            msedge,
            DateTimeOffset.UtcNow);
    }

    private static int CountPlaywrightProcesses(string name)
    {
        var count = 0;
        foreach (var proc in Process.GetProcessesByName(name))
        {
            try
            {
                if (proc.HasExited)
                {
                    continue;
                }

                proc.Refresh();
                if (IsPlaywrightBrowserPath(proc))
                {
                    count++;
                }
            }
            catch
            {
                // access denied or exited
            }
            finally
            {
                proc.Dispose();
            }
        }

        return count;
    }

    private static bool IsPlaywrightBrowserPath(Process proc)
    {
        try
        {
            var path = proc.MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(path))
            {
                var normalized = path.Replace('\\', '/');
                if (normalized.Contains("ms-playwright", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("/playwright/", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // MainModule often blocked on Windows; fall through
        }

        return proc.WorkingSet64 >= 8 * 1024 * 1024;
    }

    public string ToConsoleLine() =>
        $"[{PageIndex,4}] WS={WorkingSetMb,7:F1}MB managed={ManagedMb,7:F1}MB node={NodeRssMb,7:F1}MB({NodeProcessCount}) browser={BrowserProcessCount} | {Phase} {Url}";

    public string ToCsvLine() =>
        string.Join(',',
            PageIndex.ToString(),
            Csv(Url),
            Csv(Phase),
            ManagedMb.ToString("F2"),
            WorkingSetMb.ToString("F2"),
            NodeRssMb.ToString("F2"),
            NodeProcessCount.ToString(),
            ChromeProcessCount.ToString(),
            ChromiumProcessCount.ToString(),
            MsEdgeProcessCount.ToString(),
            CapturedAt.ToString("O"));

    private static string Csv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
        {
            return '"' + value.Replace("\"", "\"\"") + '"';
        }

        return value;
    }

    public static string CsvHeader() =>
        "page_index,url,phase,managed_mb,working_set_mb,node_rss_mb,node_count,chrome_count,chromium_count,msedge_count,captured_at";
}

internal static class MemoryCsvWriter
{
    public static async Task WriteAsync(string path, IReadOnlyList<MemorySnapshot> rows)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await using var sw = new StreamWriter(path, append: false, Encoding.UTF8);
        await sw.WriteLineAsync(MemorySnapshot.CsvHeader());
        foreach (var row in rows)
        {
            await sw.WriteLineAsync(row.ToCsvLine());
        }
    }
}
