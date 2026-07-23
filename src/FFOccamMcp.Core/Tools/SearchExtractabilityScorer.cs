using OccamMcp.Core.Services;

namespace OccamMcp.Core.Tools;

/// <summary>
/// Maps a cheap <see cref="ProbeAnalysis"/> to an extractability score in [0,1] used to rerank
/// <c>occam_search</c> hits — demoting dead/blocked/paywalled/anti-bot/JS-stub results below clean
/// HTTP-extractable pages. Pure and deterministic so it can be unit-tested without network.
/// </summary>
public static class SearchExtractabilityScorer
{
    public static double Score(ProbeAnalysis probe)
    {
        // Dead or error responses: nothing to extract.
        if (!probe.Ok || probe.StatusCode >= 400 || !string.IsNullOrEmpty(probe.FailureCode))
        {
            return 0.0;
        }

        var classification = probe.Classification;

        // Anti-bot / captcha challenge — effectively unreadable without a session/browser dance.
        if (classification?.Challenge is not null)
        {
            return 0.05;
        }

        // Login / paywall wall.
        if (classification?.Signals.LikelyLoginRequired == true)
        {
            return 0.15;
        }

        // Non-HTML (pdf/binary) — the recommender returns "none".
        if (string.Equals(probe.RecommendedBackend, "none", StringComparison.OrdinalIgnoreCase))
        {
            return 0.3;
        }

        // JS-heavy stub: very little visible text behind heavy script, or the recommender wants a
        // browser. Extractable but expensive and lower-yield than a clean HTTP page.
        var jsHeavy = string.Equals(probe.RecommendedBackend, "browser", StringComparison.OrdinalIgnoreCase);
        if (classification is not null
            && classification.VisibleTextRatio is > 0 and < 0.08
            && classification.ScriptDensity > 0.5)
        {
            return 0.45;
        }
        if (jsHeavy)
        {
            return 0.55;
        }

        // Clean HTTP-extractable page. Nudge well-structured doc/article classes to the top.
        var pageClass = classification?.PageClass;
        if (pageClass is "docs" or "article" or "reference" or "blog")
        {
            return 0.9;
        }

        return 0.7;
    }
}
