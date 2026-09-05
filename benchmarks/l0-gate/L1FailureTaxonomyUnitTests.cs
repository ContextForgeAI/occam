using System.Text.Json;
using OccamMcp.Core.Agent;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Tools;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

internal static class L1FailureTaxonomyUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        RunFailureCodeStrings(assert);
        RunTranscodeDecisions(assert);
    }

    private static void RunFailureCodeStrings(Action<string, bool> assert)
    {
        assert("failure http 404", FailureCodeStrings.FromHttpStatus(404) == "http_404");
        assert("failure http 401", FailureCodeStrings.FromHttpStatus(401) == "http_401");
        assert("failure http 403", FailureCodeStrings.FromHttpStatus(403) == "http_403");
        assert("failure http 200 no code", FailureCodeStrings.FromHttpStatus(200) is null);
        assert("failure normalize networkerror", FailureCodeStrings.Normalize("networkerror") == "network_error");
        assert("failure normalize econnreset network", FailureCodeStrings.Normalize("ECONNRESET") == "network_error");
        assert("failure normalize enotfound dns", FailureCodeStrings.Normalize("ENOTFOUND") == "dns_error");
        assert("failure normalize eai_again dns", FailureCodeStrings.Normalize("EAI_AGAIN") == "dns_error");
        assert("failure normalize cert expired tls", FailureCodeStrings.Normalize("CERT_HAS_EXPIRED") == "tls_error");
        assert("failure normalize altname tls", FailureCodeStrings.Normalize("ERR_TLS_CERT_ALTNAME_INVALID") == "tls_error");
        assert("failure resolve enotfound dns", FailureCodeStrings.ResolveTranscodeFailure("ENOTFOUND", 0) == "dns_error");
        assert("failure resolve cert tls", FailureCodeStrings.ResolveTranscodeFailure("self_signed_cert_in_chain", 0) == "tls_error");
        // Q-005: a raw JS error name from a worker catch-all must not leak as a failure code.
        assert("failure normalize typeerror -> extraction_failed", FailureCodeStrings.Normalize("TypeError") == "extraction_failed");
        assert("failure resolve typeerror -> extraction_failed", FailureCodeStrings.ResolveTranscodeFailure("TypeError", 0) == "extraction_failed");
        // Q-011: raw Node/undici TLS + socket error codes (variable suffixes) must fold into the taxonomy.
        assert("failure normalize err_ssl alert tls", FailureCodeStrings.Normalize("ERR_SSL_TLSV1_ALERT_INTERNAL_ERROR") == "tls_error");
        assert("failure normalize err_ssl unrecognized tls", FailureCodeStrings.Normalize("ERR_SSL_TLSV1_UNRECOGNIZED_NAME") == "tls_error");
        assert("failure normalize ssl/tls handshake alert tls", FailureCodeStrings.Normalize("err_ssl_ssl/tls_alert_handshake_failure") == "tls_error");
        assert("failure normalize und_err_socket network", FailureCodeStrings.Normalize("UND_ERR_SOCKET") == "network_error");
        assert("failure normalize und_err connect timeout", FailureCodeStrings.Normalize("UND_ERR_CONNECT_TIMEOUT") == "timeout");
        assert("failure resolve err_ssl alert tls", FailureCodeStrings.ResolveTranscodeFailure("ERR_SSL_TLSV1_ALERT_INTERNAL_ERROR", 0) == "tls_error");
        assert("failure resolve und_err_socket network", FailureCodeStrings.ResolveTranscodeFailure("UND_ERR_SOCKET", 0) == "network_error");
        assert("failure retryable dns", FailureCodeStrings.IsRetryable("dns_error"));
        assert("failure retryable thin_extract", FailureCodeStrings.IsRetryable("thin_extract"));
        assert("failure retryable render_error", FailureCodeStrings.IsRetryable("render_error"));
        assert("failure not retryable tls", !FailureCodeStrings.IsRetryable("tls_error"));
        assert(
            "failure dns message mentions dns",
            FailureCodeStrings.FormatTranscodeMessage("dns_error", 0).Contains("DNS", StringComparison.Ordinal));
        assert(
            "failure tls message mentions certificate",
            FailureCodeStrings.FormatTranscodeMessage("tls_error", 0).Contains("certificate", StringComparison.OrdinalIgnoreCase));
        assert("failure normalize aborterror", FailureCodeStrings.Normalize("aborterror") == "timeout");
        assert("failure normalize backend alias", FailureCodeStrings.Normalize("backend_unavailable") == "workers_unavailable");
        assert("failure resolve http status", FailureCodeStrings.ResolveTranscodeFailure("extraction_failed", 404) == "http_404");
        assert("failure resolve http token", FailureCodeStrings.ResolveTranscodeFailure("http_404", 0) == "http_404");
        assert("failure resolve workers", FailureCodeStrings.ResolveTranscodeFailure("no_json", 0) == "workers_unavailable");
        assert("failure resolve response too large", FailureCodeStrings.ResolveTranscodeFailure("response_too_large", 0) == "response_too_large");
        assert("failure resolve response truncated", FailureCodeStrings.ResolveTranscodeFailure("response_truncated", 0) == "response_truncated");
        assert("failure normalize action_failed identity", FailureCodeStrings.Normalize("action_failed") == "action_failed");
        assert("failure resolve action_failed", FailureCodeStrings.ResolveTranscodeFailure("action_failed", 0) == "action_failed");
        assert("failure not retryable action_failed", !FailureCodeStrings.IsRetryable("action_failed"));
        assert("failure format action_failed",
            FailureCodeStrings.FormatTranscodeMessage("action_failed", 0).Contains("action", StringComparison.OrdinalIgnoreCase));
        assert("failure parse status from code", FailureCodeStrings.TryParseHttpStatusCode("http_404") == 404);

        var next = NextActionFormatter.FromDecisions(TranscodeAgentDecisions.ForFailure("thin_extract"));
        assert("next_action thin_extract primary",
            next is not null
            && next.StartsWith("retry_transcode(backend_policy=browser)", StringComparison.Ordinal));
        assert("next_action empty decisions", NextActionFormatter.FromDecisions([]) is null);
        assert("next_action from suggestedNext",
            NextActionFormatter.FromHints(null, "occam_playbook_heal") == "continue: tool=occam_playbook_heal");
        assert("next_action ignores none", NextActionFormatter.FromHints(null, "none") is null);
        assert("failure retryable timeout", FailureCodeStrings.IsRetryable("timeout"));
        assert("failure retryable response too large", !FailureCodeStrings.IsRetryable("response_too_large"));
        assert("failure retryable 404", !FailureCodeStrings.IsRetryable("http_404"));
        assert(
            "failure transcode message mentions status",
            FailureCodeStrings.FormatTranscodeMessage("http_404", 404).Contains("404", StringComparison.Ordinal));
    }

    private static void RunTranscodeDecisions(Action<string, bool> assert)
    {
        var notFound = TranscodeAgentDecisions.ForFailure("http_404");
        assert("failure decision 404 stop", notFound.Any(d => d.Action == "stop"));

        var challenge = TranscodeAgentDecisions.ForFailure("captcha_or_challenge");
        assert("failure challenge session", challenge.Any(d => d.Action == "configure_session_profile"));
        assert("failure challenge retry browser", challenge.Any(d => d.Action == "retry_transcode" && d.Parameter?.Contains("browser") == true));
        assert("failure challenge inform", challenge.Any(d => d.Action == "inform_user"));
        assert("failure challenge stop", challenge.Any(d => d.Action == "stop"));
        assert("failure challenge no alternate url", !challenge.Any(d => d.Action == "use_alternate_url"));
        assert("failure challenge first is session", challenge[0].Action == "configure_session_profile");

        var thin = TranscodeAgentDecisions.ForFailure("thin_extract");
        assert("failure thin retry browser", thin.Any(d => d.Action == "retry_transcode" && d.Parameter?.Contains("browser") == true));

        var renderError = TranscodeAgentDecisions.ForFailure("render_error");
        assert("failure render_error retry browser", renderError.Any(d => d.Action == "retry_transcode" && d.Parameter?.Contains("browser") == true));
        var renderStop = TranscodeAgentDecisions.RenderErrorBrowserExhausted();
        assert("failure render_error browser exhausted stop", renderStop.Any(d => d.Action == "stop"));

        assert(
            "failure ranking extraction < thin < render_error < http_4xx",
            FailureRanking.Informativeness("extraction_failed") < FailureRanking.Informativeness("thin_extract")
            && FailureRanking.Informativeness("thin_extract") < FailureRanking.Informativeness("render_error")
            && FailureRanking.Informativeness("render_error") < FailureRanking.Informativeness("http_404")
            && FailureRanking.Informativeness("render_error") < FailureRanking.Informativeness("http_403"));
        assert(
            "failure render_error message names error shell",
            FailureCodeStrings.FormatTranscodeMessage("render_error", 0)
                .Contains("error shell", StringComparison.OrdinalIgnoreCase));

        RunRenderErrorCascade(assert);
        RunBrowserExhaustionEnvelope(assert);

        var oversize = TranscodeAgentDecisions.ForFailure("response_too_large");
        assert("failure response too large stop", oversize.Any(d => d.Action == "stop"));

        var login = TranscodeAgentDecisions.ForFailure("requires_login");
        assert("failure requires_login session", login.Any(d => d.Action == "configure_session_profile"));
        assert("failure requires_login browser", login.Any(d => d.Action == "retry_transcode" && d.Parameter?.Contains("browser") == true));

        var forbidden = TranscodeAgentDecisions.ForFailure("http_403");
        assert("failure http_403 session", forbidden.Any(d => d.Action == "configure_session_profile"));
        assert("failure http_403 browser", forbidden.Any(d => d.Action == "retry_transcode" && d.Parameter?.Contains("browser") == true));

        var workers = TranscodeAgentDecisions.ForFailure("workers_unavailable");
        assert("failure workers run_doctor", workers.Any(d => d.Action == "run_doctor"));

        var dns = TranscodeAgentDecisions.ForFailure("dns_error");
        assert("failure dns retry transcode", dns.Any(d => d.Action == "retry_transcode"));

        var tls = TranscodeAgentDecisions.ForFailure("tls_error");
        assert("failure tls stop", tls.Any(d => d.Action == "stop"));

        var schema = TranscodeAgentDecisions.ForFailure("knowledge_schema_missing");
        assert("failure schema transcode fallback", schema.Any(d => d.Tool == "occam_transcode"));

        var probeLogin = ProbeAgentHints.ForFailure("http_403");
        assert("probe failure 403 hints", probeLogin.Decisions.Any(d => d.Action == "configure_session_profile"));
    }

    private static void RunRenderErrorCascade(Action<string, bool> assert)
    {
        const string usable = """
            # Runtime documentation

            This page describes how to start the HTTP server, bind a port, and keep the process alive.
            It includes several paragraphs of operator guidance so the extract is clearly usable public content
            rather than a client error shell. Additional notes cover TLS certificates, health checks, and logs.
            """;
        const string errorShell = "## This page couldn’t load\n\nReload to try again, or go back.";

        var httpUsable = new ExtractRunResult(true, usable, "http", null, 12, "https://example.test/docs", false, 200);
        var browserError = new ExtractRunResult(true, errorShell, "browser", null, 40, "https://example.test/docs", false, 200);
        assert("cascade usable HTTP is successful", OccamRouter.IsSuccessfulExtractForTests(httpUsable));
        assert("cascade browser error shell is not successful", !OccamRouter.IsSuccessfulExtractForTests(browserError));

        var httpForbidden = new ExtractRunResult(false, null, "http", "http_403", 8, "https://example.test/docs", false, 403);
        var fallback = OccamRouter.ChooseRawFallbackForTests(httpForbidden, browserError);
        assert("cascade 403 outranks browser render_error", fallback.StatusCode == 403);

        var httpErrorShell = new ExtractRunResult(true, errorShell, "http", null, 10, "https://example.test/docs", false, 200);
        var browserTimeout = new ExtractRunResult(false, null, "browser", "timeout", 30, "https://example.test/docs", true, 0);
        var shellVsTimeout = OccamRouter.ChooseRawFallbackForTests(httpErrorShell, browserTimeout);
        assert("cascade render_error outranks timeout", shellVsTimeout.Backend == "http");
    }

    private static void RunBrowserExhaustionEnvelope(Action<string, bool> assert)
    {
        const string url = "https://example.test/docs";
        var recovery = new[]
        {
            new OccamTranscodeRecoveryInfo("http", true, 12, TransportOk: true, Usable: false, FailureCode: "render_error"),
            new OccamTranscodeRecoveryInfo("browser", false, 30, TransportOk: false, Usable: false, FailureCode: "timeout", EscalationReason: "render_error"),
        };
        var httpRenderError = new TranscodeOutcome(
            false,
            "## This page couldn’t load\n\nReload to try again, or go back.",
            url,
            "http",
            "render_error",
            "error shell",
            StatusCode: 200,
            Recovery:
            [
                new TranscodeAttempt("http", true, 12, true, false, "render_error"),
                new TranscodeAttempt("browser", false, 30, false, false, "timeout", "render_error"),
            ]);
        AssertExhaustedEnvelope(
            assert,
            "R1",
            OccamTranscodeTool.SerializePipelineFailureForTests(url, httpRenderError, recovery));

        var thinRecovery = new[]
        {
            new OccamTranscodeRecoveryInfo("http", true, 10, TransportOk: true, Usable: false, FailureCode: "thin_extract"),
            new OccamTranscodeRecoveryInfo("browser", false, 25, TransportOk: false, Usable: false, FailureCode: "timeout", EscalationReason: "thin_extract"),
        };
        var httpThin = new TranscodeOutcome(
            false,
            "Short.",
            url,
            "http",
            "thin_extract",
            "thin",
            StatusCode: 200,
            Recovery:
            [
                new TranscodeAttempt("http", true, 10, true, false, "thin_extract"),
                new TranscodeAttempt("browser", false, 25, false, false, "timeout", "thin_extract"),
            ]);
        AssertExhaustedEnvelope(
            assert,
            "R2",
            OccamTranscodeTool.SerializePipelineFailureForTests(url, httpThin, thinRecovery));

        var httpOnly = new TranscodeOutcome(false, null, url, "http", "render_error", "error shell", StatusCode: 200);
        var httpOnlyJson = OccamTranscodeTool.SerializePipelineFailureForTests(url, httpOnly);
        using (var httpOnlyDoc = JsonDocument.Parse(httpOnlyJson))
        {
            assert("HTTP-only render_error retryable", FailureRetryable(httpOnlyDoc.RootElement) == true);
            assert(
                "HTTP-only render_error retries browser",
                HasDecision(httpOnlyDoc.RootElement, "retry_transcode", "browser"));
            assert("HTTP-only render_error does not stop", !HasDecision(httpOnlyDoc.RootElement, "stop"));
        }

        var directBrowser = new TranscodeOutcome(false, null, url, "browser", "render_error", "error shell", StatusCode: 200);
        AssertExhaustedEnvelope(
            assert,
            "direct-browser-render_error",
            OccamTranscodeTool.SerializePipelineFailureForTests(url, directBrowser));

        var forbidden = new TranscodeOutcome(
            false, null, url, "http", "http_403", "forbidden", StatusCode: 403,
            Recovery: [new TranscodeAttempt("http", false, 8, false, false, "http_403")]);
        var forbiddenJson = OccamTranscodeTool.SerializePipelineFailureForTests(
            url,
            forbidden,
            [new OccamTranscodeRecoveryInfo("http", false, 8, false, false, "http_403")]);
        using var forbiddenDoc = JsonDocument.Parse(forbiddenJson);
        assert("403 stays http_403", FailureCode(forbiddenDoc.RootElement) == "http_403");
        assert("403 not retryable true", FailureRetryable(forbiddenDoc.RootElement) != true);
        assert("403 retries local browser", HasDecision(forbiddenDoc.RootElement, "retry_transcode", "browser"));
        assert("403 leads with session", HasDecision(forbiddenDoc.RootElement, "configure_session_profile"));
    }

    private static void AssertExhaustedEnvelope(Action<string, bool> assert, string id, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        assert($"{id} retryable is not true", FailureRetryable(root) != true);
        assert($"{id} stop", HasDecision(root, "stop"));
        assert($"{id} no retry_transcode browser", !HasDecision(root, "retry_transcode", "browser"));
    }

    private static string? FailureCode(JsonElement root) =>
        root.TryGetProperty("failure", out var failure)
        && failure.TryGetProperty("code", out var code)
            ? code.GetString()
            : null;

    private static bool? FailureRetryable(JsonElement root)
    {
        if (!root.TryGetProperty("failure", out var failure)
            || !failure.TryGetProperty("retryable", out var retryable)
            || retryable.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return retryable.ValueKind == JsonValueKind.True;
    }

    private static bool HasDecision(JsonElement root, string action, string? parameterNeedle = null)
    {
        if (!root.TryGetProperty("agentMeta", out var meta)
            || !meta.TryGetProperty("decisions", out var decisions)
            || decisions.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var decision in decisions.EnumerateArray())
        {
            if (!decision.TryGetProperty("action", out var actionEl)
                || actionEl.GetString() != action)
            {
                continue;
            }

            if (parameterNeedle is null)
            {
                return true;
            }

            if (decision.TryGetProperty("parameter", out var parameter)
                && parameter.GetString()?.Contains(parameterNeedle, StringComparison.Ordinal) == true)
            {
                return true;
            }
        }

        return false;
    }
}
