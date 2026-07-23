namespace OccamMcp.Core.Routing;

/// <summary>Challenge taxonomy for probe/transcode hints — detect-only, no solver.</summary>
public static class ChallengeKindClassifier
{
    private const double RateLimitVisibleRatioThreshold = 0.08;
    private const int RateLimitMinVisibleTextChars = 500;

    public static bool Detect(string? content, int statusCode = 0) =>
        DetectHint(content, statusCode) is not null;

    public static ChallengeHint? DetectHint(
        string? content,
        int statusCode = 0,
        string? url = null,
        double visibleTextRatio = 1.0,
        int visibleTextChars = 0)
    {
        if (string.IsNullOrWhiteSpace(content) && statusCode == 0)
        {
            return null;
        }

        var lower = content?.ToLowerInvariant() ?? string.Empty;
        var proseChars = visibleTextChars > 0
            ? visibleTextChars
            : EstimateVisibleTextChars(lower);

        var looksLikeDocsArticle = statusCode != 429
            && proseChars >= RateLimitMinVisibleTextChars
            && (lower.Contains("<main", StringComparison.Ordinal) || lower.Contains("<article", StringComparison.Ordinal))
            && lower.Contains("<h2", StringComparison.Ordinal);

        if (!looksLikeDocsArticle
            && (statusCode == 429
                || (Contains(lower, "rate limit", "too many requests", "slow down", "retry-after")
                    && visibleTextRatio < RateLimitVisibleRatioThreshold
                    && proseChars < RateLimitMinVisibleTextChars)))
        {
            return Hint("rate_limit", "retry_later");
        }

        if (Contains(lower, "turnstile", "challenges.cloudflare.com/turnstile"))
        {
            return Hint("turnstile", "skip_url");
        }

        if (Contains(lower, "hcaptcha.com", "hcaptcha", "newassets.hcaptcha.com"))
        {
            if (!string.IsNullOrWhiteSpace(url)
                && DomainTierRegistry.ShouldSuppressProbeChallengeStop(url, visibleTextRatio, proseChars))
            {
                return null;
            }

            return Hint("hcaptcha", "stop");
        }

        if (Contains(lower, "captcha-delivery.com", "geo.captcha-delivery.com", "datadome"))
        {
            return Hint("datadome", "stop");
        }

        if (Contains(
                lower,
                "cf-challenge",
                "challenges.cloudflare.com",
                "challenge-platform",
                "checking your browser",
                "just a moment",
                "performance & security by cloudflare",
                "ray id:",
                "enable javascript and cookies"))
        {
            return Hint("js_challenge", "session_cookies");
        }

        if (!string.IsNullOrWhiteSpace(url)
            && url.Contains("__cf_chl", StringComparison.OrdinalIgnoreCase))
        {
            return Hint("js_challenge", "session_cookies");
        }

        if (Contains(
                lower,
                "captcha",
                "verify you are human",
                "attention required",
                "why have i been blocked",
                "sorry, you have been blocked"))
        {
            if (!string.IsNullOrWhiteSpace(url)
                && DomainTierRegistry.ShouldSuppressProbeChallengeStop(url, visibleTextRatio, proseChars))
            {
                return null;
            }

            return Hint("generic_challenge", "stop");
        }

        if (statusCode is 403 or 503
            && !string.IsNullOrWhiteSpace(url)
            && (url.Contains("cloudflare", StringComparison.OrdinalIgnoreCase)
                || lower.Contains("cloudflare", StringComparison.Ordinal)))
        {
            return Hint("js_challenge", "skip_url");
        }

        return null;
    }

    private static ChallengeHint Hint(string kind, string recommendedAction) =>
        new(kind, HealEligible: false, recommendedAction);

    private static bool Contains(string lower, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (lower.Contains(needle, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static int EstimateVisibleTextChars(string lowerHtml)
    {
        var stripped = System.Text.RegularExpressions.Regex.Replace(lowerHtml, "<script[^>]*>[\\s\\S]*?</script>", " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        stripped = System.Text.RegularExpressions.Regex.Replace(stripped, "<style[^>]*>[\\s\\S]*?</style>", " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        stripped = System.Text.RegularExpressions.Regex.Replace(stripped, "<[^>]+>", " ");
        stripped = System.Text.RegularExpressions.Regex.Replace(stripped, "\\s+", " ");
        return stripped.Trim().Length;
    }
}

public sealed record ChallengeHint(string Kind, bool HealEligible, string RecommendedAction);
