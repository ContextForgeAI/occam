using OccamMcp.Core.Playbooks;

namespace OccamMcp.L0Gate;

internal static class L3HealLearnUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        assert("heal policy terminal captcha", PlaybookHealPolicy.IsTerminalFailure("captcha_or_challenge"));
        assert("heal policy terminal 404", PlaybookHealPolicy.IsTerminalFailure("http_404"));
        assert("heal policy terminal private", PlaybookHealPolicy.IsTerminalFailure("private_url_blocked"));
        assert("heal policy terminal workers", PlaybookHealPolicy.IsTerminalFailure("workers_unavailable"));
        assert("heal policy terminal timeout", PlaybookHealPolicy.IsTerminalFailure("timeout"));

        assert("heal policy offers thin_extract", PlaybookHealPolicy.ShouldOfferHeal("thin_extract"));
        assert("heal policy offers extraction_failed", PlaybookHealPolicy.ShouldOfferHeal("extraction_failed"));
        assert("heal policy offers content_selectors_miss", PlaybookHealPolicy.ShouldOfferHeal("content_selectors_miss"));
        assert("heal policy rejects captcha", !PlaybookHealPolicy.ShouldOfferHeal("captcha_or_challenge"));
        assert("heal policy rejects 404", !PlaybookHealPolicy.ShouldOfferHeal("http_404"));
        assert("heal policy rejects private", !PlaybookHealPolicy.ShouldOfferHeal("private_url_blocked"));
        assert("heal policy rejects login no session", !PlaybookHealPolicy.ShouldOfferHeal("requires_login"));
        assert("heal policy offers login with session", PlaybookHealPolicy.ShouldOfferHeal("requires_login", sessionProfileApplied: true));
        assert("heal policy selector miss warning", PlaybookHealPolicy.ShouldOfferHeal(
            null,
            ["content_selectors_miss: none matched [#main]"]));
        assert("heal policy degraded warning", PlaybookHealPolicy.ShouldOfferHeal(null, ["degraded_quality:low_retention"]));

        assert("heal policy rejects extraction_failed js_challenge url", !PlaybookHealPolicy.ShouldOfferHeal(
            "extraction_failed",
            finalUrl: "https://www.reddit.com/svc/shreddit/update-recaptcha?js_challenge=1",
            requestUrl: "https://www.reddit.com/r/test/comments/abc/test/"));
        assert("heal policy rejects challenge finalUrl", !PlaybookHealPolicy.ShouldOfferHeal(
            "extraction_failed",
            finalUrl: "https://example.com/cdn-cgi/challenge-platform/h/b"));
        assert("heal policy rejects captcha url", !PlaybookHealPolicy.ShouldOfferHeal(
            "extraction_failed",
            finalUrl: "https://geo.captcha-delivery.com/captcha/"));
        assert("heal policy offers extraction_failed clean url", PlaybookHealPolicy.ShouldOfferHeal(
            "extraction_failed",
            finalUrl: "https://nginx.org/en/docs/http/ngx_http_core_module.html",
            requestUrl: "https://nginx.org/en/docs/http/ngx_http_core_module.html"));
        assert("heal policy rejects anti_bot warning", !PlaybookHealPolicy.ShouldOfferHeal(
            "extraction_failed",
            agentWarnings: ["anti_bot_challenge: likely"]));
        assert("heal policy has challenge url helper", PlaybookHealPolicy.HasChallengeUrl(
            "https://www.reddit.com/?js_challenge=token"));

        assert("heal max verify retries", PlaybookHealPolicy.MaxVerifyRetries == 3);
        assert("heal max lessons", PlaybookHealPolicy.MaxLessonsPerFile == 50);

        RunAppendLesson(assert);
    }

    private static void RunAppendLesson(Action<string, bool> assert)
    {
        const string playbook = """
            {
              "schema_version": "1.0",
              "id": "heal-test",
              "hosts": ["example.com"]
            }
            """;

        var updated = PlaybookDocument.AppendLesson(
            playbook,
            "selector tightened",
            "content_selectors_miss",
            4,
            "cursor-desk");

        assert("append lesson adds array", updated.Contains("\"lessons\"", StringComparison.Ordinal));
        assert("append lesson note", updated.Contains("selector tightened", StringComparison.Ordinal));
        assert("append lesson failure", updated.Contains("content_selectors_miss", StringComparison.Ordinal));
        assert("append lesson host", updated.Contains("cursor-desk", StringComparison.Ordinal));
    }
}
