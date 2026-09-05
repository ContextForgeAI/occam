namespace OccamMcp.Core.Agent;

public static class TranscodeAgentDecisions
{
    // thin_extract when the browser backend was ALREADY used: a further render will not help — the
    // page is genuinely minimal. Override the default retry_transcode(browser) hint (which loops a
    // compliant agent) with a clean stop.
    public static ProbeDecision[] ThinExtractBrowserExhausted() =>
    [
        new ProbeDecision(
            "stop",
            "Content is still thin after a full browser render — the page is genuinely near-empty. Report the little content that came back (or that the page has almost none); do not retry further and do not invent content."),
    ];

    public static ProbeDecision[] RenderErrorBrowserExhausted() =>
    [
        new ProbeDecision(
            "stop",
            "The returned document was a browser/client render error shell rather than usable page content. Access is unknown, not a login wall. Do not invent an answer from it and do not retry the same browser policy."),
    ];

    public static ProbeDecision[] ForFailure(string failureCode)
    {
        if (failureCode.StartsWith("http_404", StringComparison.Ordinal) || failureCode == "http_410")
        {
            return
            [
                new ProbeDecision(
                    "stop",
                    "Page not found — fix the URL or remove it from the corpus. Do not hallucinate content."),
            ];
        }

        if (failureCode is "http_401" or "http_403" or "requires_login")
        {
            return
            [
                new ProbeDecision(
                    "configure_session_profile",
                    "Login / access wall — host operator exports cookies via occam-session.mjs export-state (or occam session export-state); retry transcode with session_profile.",
                    Parameter: "session_profile"),
                new ProbeDecision(
                    "retry_transcode",
                    "If cookies alone are not enough, retry once with backend_policy=browser (local Playwright) using the same session_profile when available. Do not escalate to third-party scrape APIs.",
                    Tool: "occam_transcode",
                    Parameter: "backend_policy=browser"),
                new ProbeDecision(
                    "stop",
                    "Do not summarize page content from memory until session is configured."),
            ];
        }

        if (failureCode == "session_profile_not_found")
        {
            return
            [
                new ProbeDecision(
                    "configure_session_profile",
                    "session_profile id missing under OCCAM_SESSIONS_ROOT — create profile JSON locally.",
                    Parameter: "session_profile"),
            ];
        }

        if (failureCode == "workers_unavailable")
        {
            return
            [
                new ProbeDecision(
                    "run_doctor",
                    "Workers missing or OCCAM_HOME wrong — run occam doctor.",
                    Tool: "occam doctor"),
            ];
        }

        if (failureCode == "playbook_not_found")
        {
            return
            [
                new ProbeDecision(
                    "continue",
                    "No playbook for host — use occam_transcode with default backend_policy.",
                    Tool: "occam_transcode"),
            ];
        }

        if (failureCode is "knowledge_schema_missing" or "page_class_unmatched" or "knowledge_schema_empty")
        {
            return
            [
                new ProbeDecision(
                    "stop",
                    "No matching knowledge_schema — use occam_transcode for markdown instead of extract.",
                    Tool: "occam_transcode"),
            ];
        }

        if (failureCode == "heal_not_applicable")
        {
            return
            [
                new ProbeDecision(
                    "stop",
                    "Failure is not healable — read failure.code and act on it directly."),
            ];
        }

        if (failureCode is "playbook_verify_failed" or "playbook_verify_low_score" or "playbook_verify_high_noise")
        {
            return
            [
                new ProbeDecision(
                    "revise_playbook",
                    "Dry-run verify failed — adjust contentSelectors in host-drafted playbook_json.",
                    Tool: "occam_playbook_save"),
            ];
        }

        if (failureCode is "playbook_schema_invalid" or "playbook_save_rejected")
        {
            return
            [
                new ProbeDecision(
                    "stop",
                    "Fix playbook_json schema or remove forbidden secret keys before save."),
            ];
        }

        if (failureCode == "sitemap_not_found")
        {
            return
            [
                new ProbeDecision(
                    "retry_map",
                    "No sitemap links — retry occam_map with source=homepage.",
                    Tool: "occam_map",
                    Parameter: "source=homepage"),
            ];
        }

        if (failureCode == "invalid_urls")
        {
            return
            [
                new ProbeDecision(
                    "stop",
                    "Fix urls parameter — JSON array or delimited list of absolute HTTP(S) URLs."),
            ];
        }

        if (failureCode == "digest_failed")
        {
            return
            [
                new ProbeDecision(
                    "retry_transcode",
                    "All digest URLs failed — retry singles with occam_transcode.",
                    Tool: "occam_transcode"),
            ];
        }

        if (failureCode is "captcha_or_challenge" or "anti_bot_blocked")
        {
            return
            [
                new ProbeDecision(
                    "configure_session_profile",
                    "Anti-bot challenge — Occam has no CAPTCHA solver. Operator may export a warm browser session via occam-session.mjs export-state and retry with session_profile (sometimes clears JS challenges).",
                    Parameter: "session_profile"),
                new ProbeDecision(
                    "retry_transcode",
                    "Retry once with backend_policy=browser on the local Playwright path (with session_profile when available). Do not call third-party scrape APIs.",
                    Tool: "occam_transcode",
                    Parameter: "backend_policy=browser"),
                new ProbeDecision(
                    "inform_user",
                    "If session + browser still fail, stop — access is unknown; do not invent page content."),
                new ProbeDecision(
                    "stop",
                    "Do not invent article content from challenge pages."),
            ];
        }

        if (failureCode == "thin_extract")
        {
            return
            [
                new ProbeDecision(
                    "retry_transcode",
                    "Content too thin over HTTP — retry with backend_policy=browser or http_then_browser.",
                    Tool: "occam_transcode",
                    Parameter: "backend_policy=browser"),
            ];
        }

        if (failureCode == "render_error")
        {
            return
            [
                new ProbeDecision(
                    "retry_transcode",
                    "HTTP extract was a browser/client render error shell — retry once with backend_policy=browser if the browser has not already been tried.",
                    Tool: "occam_transcode",
                    Parameter: "backend_policy=browser"),
            ];
        }

        if (failureCode == "response_too_large")
        {
            return
            [
                new ProbeDecision(
                    "stop",
                    "HTTP body exceeded OCCAM_MAX_RESPONSE_BYTES — do not summarize from memory. Increase cap for batch jobs or skip URL."),
            ];
        }

        if (failureCode == "response_truncated")
        {
            return
            [
                new ProbeDecision(
                    "stop",
                    "Partial page extract only — markdown may be incomplete. Do not cite as full page; retry with higher OCCAM_MAX_RESPONSE_BYTES or on_oversize=fail."),
            ];
        }

        if (failureCode is "timeout" or "network_error" or "dns_error")
        {
            return
            [
                new ProbeDecision(
                    "retry_transcode",
                    "Transient fetch failure — retry once, then skip or escalate.",
                    Tool: "occam_transcode"),
            ];
        }

        if (failureCode == "tls_error")
        {
            return
            [
                new ProbeDecision(
                    "stop",
                    "TLS/certificate error — the site certificate is invalid, expired, or untrusted. Do not retry blindly; verify the host or use a trusted endpoint."),
            ];
        }

        if (failureCode.StartsWith("http_5", StringComparison.Ordinal) || failureCode == "http_429")
        {
            return
            [
                new ProbeDecision(
                    "retry_transcode",
                    "Server or rate-limit error — retry once after a short wait, then stop.",
                    Tool: "occam_transcode"),
            ];
        }

        return [];
    }
}
