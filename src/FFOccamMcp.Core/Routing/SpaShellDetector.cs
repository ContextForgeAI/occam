using OccamMcp.Core.Text;

namespace OccamMcp.Core.Routing;

public static class SpaShellDetector
{
    public const int StubVisibleTextMax = 500;
    public const double StubVisibleRatioMax = 0.04;

    public static bool IsStub(ProbeSignals? probe) =>
        probe?.SpaShell == true
        || probe is { HtmlBytes: >= 4000, VisibleTextRatio: < StubVisibleRatioMax };

    public static bool DetectFromHtml(string html, out int visibleTextLength)
    {
        visibleTextLength = EstimateVisibleText(html);
        if (visibleTextLength >= StubVisibleTextMax)
        {
            return false;
        }

        return HasStubMarker(html.ToLowerInvariant());
    }

    public static bool HasStubMarker(string lowerHtml) =>
        lowerHtml.Contains("id=\"app\"", StringComparison.Ordinal)
        || lowerHtml.Contains("id='app'", StringComparison.Ordinal)
        || lowerHtml.Contains("id=\"root\"", StringComparison.Ordinal)
        || lowerHtml.Contains("id='root'", StringComparison.Ordinal)
        || lowerHtml.Contains("id=\"__next\"", StringComparison.Ordinal)
        || lowerHtml.Contains("__next_data__", StringComparison.Ordinal)
        || lowerHtml.Contains("__nuxt__", StringComparison.Ordinal)
        || lowerHtml.Contains("data-reactroot", StringComparison.Ordinal)
        || lowerHtml.Contains("ng-version=\"", StringComparison.Ordinal);

    internal static int EstimateVisibleText(string html) =>
        HtmlVisibleTextScanner.CountVisibleText(html);
}
