using System.Collections.Frozen;

namespace OccamMcp.Core.Playbooks;

/// <summary>Heal trigger matrix — HEAL_LEARN_TEST_PLAN §2.</summary>
public static class PlaybookHealPolicy
{
    public const int MaxVerifyRetries = 3;
    public const int MaxLessonsPerFile = 50;

    private static readonly FrozenSet<string> TerminalFailures = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "captcha_or_challenge",
        "private_url_blocked",
        "workers_unavailable",
        "timeout",
        "invalid_arguments",
        "invalid_policy",
        "invalid_url",
        "invalid_failure_reason",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> HealFailureCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "thin_extract",
        "extraction_failed",
        "content_extraction_failed",
        "content_selectors_miss",
        "playbook_verify_failed",
        "playbook_verify_low_score",
        "playbook_verify_high_noise",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static bool IsTerminalFailure(string? failureCode)
    {
        if (string.IsNullOrWhiteSpace(failureCode))
        {
            return false;
        }

        var code = Normalize(failureCode);
        if (TerminalFailures.Contains(code))
        {
            return true;
        }

        return code.StartsWith("http_4", StringComparison.Ordinal)
            || code.StartsWith("http_5", StringComparison.Ordinal);
    }

    public static bool HasChallengeUrl(string? finalUrl, string? requestUrl = null) =>
        LooksLikeChallengeUrl(finalUrl) || LooksLikeChallengeUrl(requestUrl);

    public static bool ShouldOfferHeal(
        string? failureCode,
        IEnumerable<string>? agentWarnings = null,
        bool sessionProfileApplied = false,
        string? finalUrl = null,
        string? requestUrl = null)
    {
        if (HasChallengeUrl(finalUrl, requestUrl))
        {
            return false;
        }

        if (agentWarnings?.Any(HasChallengeWarning) == true)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(failureCode))
        {
            return agentWarnings?.Any(w =>
                w.StartsWith("content_selectors_miss", StringComparison.OrdinalIgnoreCase)
                || HasDegradedQualityWarning(w)) == true;
        }

        var code = Normalize(failureCode);

        if (code is "requires_login" or "http_401" or "http_403")
        {
            return sessionProfileApplied;
        }

        if (IsTerminalFailure(code))
        {
            return false;
        }

        if (HealFailureCodes.Contains(code))
        {
            return true;
        }

        return agentWarnings?.Any(w =>
            w.StartsWith("content_selectors_miss", StringComparison.OrdinalIgnoreCase)
            || HasDegradedQualityWarning(w)) == true;
    }

    public static string NormalizeFailureReason(string failureReason) => Normalize(failureReason);

    private static bool HasDegradedQualityWarning(string warning) =>
        warning.Contains("degraded_quality", StringComparison.OrdinalIgnoreCase)
        || warning.Contains("noise_only", StringComparison.OrdinalIgnoreCase);

    private static bool HasChallengeWarning(string warning)
    {
        var w = warning.ToLowerInvariant();
        return w.Contains("anti_bot", StringComparison.Ordinal)
            || w.Contains("likely_challenge", StringComparison.Ordinal)
            || w.StartsWith("challenge_kind:", StringComparison.Ordinal)
            || w.Contains("js_challenge", StringComparison.Ordinal);
    }

    private static bool LooksLikeChallengeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        // Anti-bot / challenge PROVIDERS live in the host (datadome captcha-delivery, hcaptcha, cloudflare
        // turnstile) — this is the strong signal and stays a host substring match.
        var host = uri.Host.ToLowerInvariant();
        if (host.Contains("captcha", StringComparison.Ordinal)
            || host.Contains("hcaptcha", StringComparison.Ordinal)
            || host.Contains("challenges.cloudflare", StringComparison.Ordinal))
        {
            return true;
        }

        // Cloudflare interstitial serves its challenge under this exact path — a definitive marker that
        // won't collide with a content slug (unlike a bare "challenge" substring).
        if (uri.AbsolutePath.Contains("/cdn-cgi/challenge-platform", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Definitive challenge markers in the query (Cloudflare interstitial redirect params).
        var query = uri.Query.ToLowerInvariant();
        if (query.Contains("__cf_chl", StringComparison.Ordinal)
            || query.Contains("challenge=", StringComparison.Ordinal))
        {
            return true;
        }

        // A dedicated challenge PATH SEGMENT — but not a content slug that merely mentions the word (e.g.
        // /blog/captcha-ux, /docs/anti-captcha), which should stay heal-eligible.
        foreach (var segment in uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment.Equals("challenge", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("js_challenge", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string code)
    {
        var c = code.Trim().ToLowerInvariant();
        return c switch
        {
            "anti_bot" => "captcha_or_challenge",
            _ => c,
        };
    }
}
