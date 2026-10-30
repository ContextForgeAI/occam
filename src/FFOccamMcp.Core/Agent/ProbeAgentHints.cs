using System.Text.Json.Serialization;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Services;

namespace OccamMcp.Core.Agent;

public sealed record ProbeDecision(
    string Action,
    string Reason,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Url = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Tool = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Parameter = null);

public static class ProbeAgentHints
{
    // A page above this raw HTML size is worth a token-budget nudge (fit_markdown / max_tokens).
    private const int LargePageBytes = 750_000;

    public sealed record Hints(
        string SuggestedNextTool,
        string[] Warnings,
        ProbeDecision[] Decisions);

    public static Hints ForProbe(ProbeAnalysis analysis)
    {
        var signals = analysis.Classification?.Signals;
        var warnings = new List<string>();
        var next = "occam_transcode";
        var challenge = analysis.Challenge;
        var hardStop = IsHardStop(challenge);

        if (DomainTierRegistry.IsAntiBotBlogTier(analysis.FinalUrl ?? analysis.Url))
        {
            warnings.Add("anti_bot_blog: expect captcha_or_challenge; no CAPTCHA solver in core.");
            if (signals?.LikelyChallenge == true && hardStop)
            {
                next = "none";
                warnings.Add("anti_bot_blocked: hard CAPTCHA class — skip URL or use another source.");
            }
            else if (signals?.LikelyChallenge == true)
            {
                warnings.Add(
                    "anti_bot_blog_soft: try occam_transcode with backend_policy=http_then_browser (or browser); "
                    + "if still challenged, export cookies via occam-session.mjs and pass session_profile.");
            }
        }
        else if (signals?.LikelyChallenge == true)
        {
            warnings.Add(
                hardStop
                    ? "likely_challenge: hard anti-bot — do not invent content; stop or use a different URL."
                    : "likely_challenge: soft wall — call occam_transcode (http_then_browser) before giving up; "
                      + "session_profile if browser still challenges.");
        }

        if (challenge is { } ch)
        {
            warnings.Add(
                $"challenge_kind:{ch.Kind}: recommended_action={ch.RecommendedAction}");
        }

        if (signals?.RequiresJavascript == true)
        {
            warnings.Add("requires_javascript: browser worker may be needed (backend_policy=http_then_browser or browser).");
        }

        if (signals?.LikelyLoginRequired == true)
        {
            warnings.Add("likely_login_required: configure session_profile before transcode (host operator runs occam-session.mjs).");
        }

        var contentType = analysis.ContentType ?? string.Empty;
        if (signals?.HasTables == true)
        {
            warnings.Add("tables_detected: page contains HTML tables; you can pass json_tables=true to occam_transcode to extract them as structured JSON.");
        }

        if (signals?.HasLlmsTxtLink == true)
        {
            warnings.Add("llms_txt_detected: site may publish an /llms.txt; pass prefer_llms_txt=true to occam_transcode for the sanctioned LLM copy.");
        }

        if (contentType.Contains("rss", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("atom", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("feed_detected: pass json_feed=true to occam_transcode for structured {title, items[]} instead of article extraction.");
        }

        if ((signals?.HtmlBytes ?? 0) >= LargePageBytes)
        {
            warnings.Add($"large_page ({signals!.HtmlBytes} bytes): set max_tokens, or fit_markdown=true + focus_query, to keep occam_transcode output small.");
        }

        if (signals?.LikelyPaywall == true)
        {
            warnings.Add("likely_paywall: content may be gated; expect thin_extract — a session_profile may help if you have access.");
        }

        if (string.Equals(analysis.RecommendedBackend, "none", StringComparison.Ordinal)
            || hardStop)
        {
            next = "none";
        }

        var decisions = BuildDecisions(
            analysis.FinalUrl ?? analysis.Url,
            signals?.LikelyChallenge == true,
            signals?.LikelyLoginRequired == true,
            challenge);
        return new Hints(next, warnings.ToArray(), decisions);
    }

    public static Hints ForFailure(string failureCode)
    {
        var decisions = TranscodeAgentDecisions.ForFailure(failureCode);
        var next = failureCode.StartsWith("http_404", StringComparison.Ordinal) || failureCode == "http_410"
            ? "none"
            : "occam_transcode";
        if (failureCode is "workers_unavailable" or "invalid_url" or "invalid_arguments"
            or "stale_handle" or "unknown_handle")
        {
            next = "none";
        }

        return new Hints(next, [], decisions);
    }

    private static bool IsHardStop(ChallengeHint? challenge) =>
        challenge?.RecommendedAction is "stop" or "skip_url"
        || challenge?.Kind is "turnstile" or "hcaptcha" or "datadome";

    private static ProbeDecision[] BuildDecisions(
        string url,
        bool likelyChallenge,
        bool likelyLoginRequired,
        ChallengeHint? challenge)
    {
        if (likelyLoginRequired)
        {
            return
            [
                new ProbeDecision(
                    "configure_session_profile",
                    "Login likely required — host operator exports cookies via occam-session.mjs export-state; retry probe/transcode with session_profile.",
                    Parameter: "session_profile"),
            ];
        }

        if (!likelyChallenge && challenge is null)
        {
            return [];
        }

        var decisions = new List<ProbeDecision>
        {
            new(
                "inform_user",
                "Anti-bot or challenge page signals detected. Occam has no CAPTCHA solver / residential unlocker."),
        };

        var action = challenge?.RecommendedAction;
        if (action is "try_browser" or "session_cookies"
            || (action is null && likelyChallenge && !IsHardStop(challenge)))
        {
            decisions.Add(new ProbeDecision(
                "try_browser",
                "Call occam_transcode with default backend_policy=http_then_browser (or backend_policy=browser). "
                + "Do not treat probe challenge as final unknown until browser has been tried.",
                Tool: "occam_transcode",
                Parameter: "backend_policy=http_then_browser"));
            decisions.Add(new ProbeDecision(
                "configure_session_profile",
                "If browser still returns captcha_or_challenge: operator exports cookies via "
                + "`occam-session.mjs export-state`, then retry with session_profile + backend_policy=browser. "
                + "See docs/guides/sessions.md.",
                Parameter: "session_profile"));
        }
        else if (action is "skip_url" or "stop"
            && !DomainTierRegistry.IsBrowserFriendlySocialHost(url))
        {
            decisions.Add(new ProbeDecision(
                "stop",
                "Hard CAPTCHA / unlocker class — skip URL; local backends cannot solve it."));
        }
        else if (DomainTierRegistry.IsBrowserFriendlySocialHost(url)
            && (likelyChallenge || challenge is not null))
        {
            decisions.Add(new ProbeDecision(
                "try_browser",
                "Public social page — try occam_transcode with backend_policy=browser or http_then_browser.",
                Tool: "occam_transcode",
                Parameter: "backend_policy=http_then_browser"));
        }

        return decisions.ToArray();
    }
}
