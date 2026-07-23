using OccamMcp.Core.Agent;
using OccamMcp.Core.Routing;

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
        assert("failure parse status from code", FailureCodeStrings.TryParseHttpStatusCode("http_404") == 404);
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
        assert("failure challenge inform", challenge.Any(d => d.Action == "inform_user"));
        assert("failure challenge stop", challenge.Any(d => d.Action == "stop"));
        assert("failure challenge no alternate url", !challenge.Any(d => d.Action == "use_alternate_url"));

        var thin = TranscodeAgentDecisions.ForFailure("thin_extract");
        assert("failure thin retry browser", thin.Any(d => d.Action == "retry_transcode" && d.Parameter?.Contains("browser") == true));

        var oversize = TranscodeAgentDecisions.ForFailure("response_too_large");
        assert("failure response too large stop", oversize.Any(d => d.Action == "stop"));

        var login = TranscodeAgentDecisions.ForFailure("requires_login");
        assert("failure requires_login session", login.Any(d => d.Action == "configure_session_profile"));

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
}
