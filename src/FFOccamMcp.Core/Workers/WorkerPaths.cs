namespace OccamMcp.Core.Workers;

public sealed class WorkerPaths
{
    public string? HttpExtractScript { get; init; }
    public string? BrowserExtractScript { get; init; }
    public string? CssExtractScript { get; init; }
    public string? DomSkeletonScript { get; init; }

    public static WorkerPaths Resolve()
    {
        var httpOverride = Environment.GetEnvironmentVariable("OCCAM_HTTP_EXTRACT_SCRIPT");
        var browserOverride = Environment.GetEnvironmentVariable("OCCAM_BROWSER_EXTRACT_SCRIPT");
        if (!string.IsNullOrWhiteSpace(httpOverride) && !string.IsNullOrWhiteSpace(browserOverride))
        {
            return new WorkerPaths
            {
                HttpExtractScript = httpOverride,
                BrowserExtractScript = browserOverride,
            };
        }

        var root = ResolveOccamHome();
        if (root is null)
        {
            return new WorkerPaths();
        }

        return new WorkerPaths
        {
            HttpExtractScript = Path.Combine(root, "workers", "http-extract", "extract.mjs"),
            BrowserExtractScript = Path.Combine(root, "workers", "browser-extract", "browser-extract.mjs"),
            CssExtractScript = Path.Combine(root, "workers", "css-extract", "css-extract.mjs"),
            DomSkeletonScript = Path.Combine(root, "workers", "browser-extract", "dom-skeleton-capture.mjs"),
        };
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(HttpExtractScript)
        && File.Exists(HttpExtractScript!)
        && !string.IsNullOrWhiteSpace(BrowserExtractScript)
        && File.Exists(BrowserExtractScript!);

    public bool IsCssExtractConfigured =>
        !string.IsNullOrWhiteSpace(CssExtractScript) && File.Exists(CssExtractScript!);

    /// <summary>True when browser worker is a distinct script (not HTTP-only fallback).</summary>
    public bool HasDistinctBrowserWorker
    {
        get
        {
            if (string.IsNullOrWhiteSpace(HttpExtractScript)
                || string.IsNullOrWhiteSpace(BrowserExtractScript)
                || !File.Exists(HttpExtractScript!)
                || !File.Exists(BrowserExtractScript!))
            {
                return false;
            }

            var httpFull = Path.GetFullPath(HttpExtractScript!);
            var browserFull = Path.GetFullPath(BrowserExtractScript!);
            if (string.Equals(httpFull, browserFull, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return browserFull.Contains("browser", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static string? TryGetRepoRoot() => ResolveOccamHome();

    public static string? ResolveOccamHome()
    {
        var envRoot = Environment.GetEnvironmentVariable("OCCAM_HOME");
        if (!string.IsNullOrWhiteSpace(envRoot) && IsOccamRoot(envRoot))
        {
            return Path.GetFullPath(envRoot);
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (IsOccamRoot(dir.FullName))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        var cwd = Directory.GetCurrentDirectory();
        return IsOccamRoot(cwd) ? Path.GetFullPath(cwd) : null;
    }

    private static bool IsOccamRoot(string root) =>
        File.Exists(Path.Combine(root, "workers", "http-extract", "extract.mjs"))
        || File.Exists(Path.Combine(root, "src", "OccamMcp.Core", "OccamMcp.Core.csproj"));
}
