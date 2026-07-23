namespace OccamMcp.Core.Routing;

public static class FailureCodeStrings
{
    public static bool IsSuccessHttpStatus(int statusCode) => statusCode is >= 200 and < 300;

    public static string? FromHttpStatus(int statusCode)
    {
        if (statusCode <= 0 || IsSuccessHttpStatus(statusCode))
        {
            return null;
        }

        return statusCode switch
        {
            401 => "http_401",
            403 => "http_403",
            404 => "http_404",
            410 => "http_410",
            >= 400 and < 500 => $"http_{statusCode}",
            >= 500 and < 600 => $"http_{statusCode}",
            _ => "http_error",
        };
    }

    public static int TryParseHttpStatusCode(string? failureCode)
    {
        if (string.IsNullOrWhiteSpace(failureCode)
            || !failureCode.StartsWith("http_", StringComparison.Ordinal))
        {
            return 0;
        }

        var suffix = failureCode["http_".Length..];
        return int.TryParse(suffix, out var status) ? status : 0;
    }

    public static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "content_extraction_failed";
        }

        var c = code.Trim().ToLowerInvariant().Replace('-', '_');

        // Raw Node/undici TLS + socket error codes carry variable suffixes (the SSL alert
        // type differs per host), so an exact-match switch can't catch them — they leaked
        // straight through as failure codes in the 1k sweep (err_ssl_tlsv1_alert_internal_error,
        // err_ssl_ssl/tls_alert_handshake_failure, und_err_socket). Fold the families into the
        // taxonomy before the switch so no raw code reaches the agent. Q-011.
        if (c.StartsWith("err_ssl", StringComparison.Ordinal)
            || (c.Contains("tls", StringComparison.Ordinal) && c.Contains("alert", StringComparison.Ordinal)))
        {
            return "tls_error";
        }
        if (c.Contains("timeout", StringComparison.Ordinal)
            && c.StartsWith("und_err", StringComparison.Ordinal))
        {
            return "timeout";
        }
        if (c.StartsWith("und_err_socket", StringComparison.Ordinal)
            || c == "und_err_socket"
            || c.Contains("socket", StringComparison.Ordinal))
        {
            return "network_error";
        }

        return c switch
        {
            "networkerror" => "network_error",
            "contentextractionfailed" => "content_extraction_failed",
            "invalidarguments" => "invalid_arguments",
            "backendunavailable" => "workers_unavailable",
            "backend_unavailable" => "workers_unavailable",
            "aborterror" or "taskcanceled" => "timeout",
            "httpnotfound" or "not_found" => "http_404",
            "httpforbidden" => "http_403",
            "httpunauthorized" => "http_401",
            // DNS resolution failures — typed separately from generic connect errors (transient, retryable).
            "enotfound" or "eai_again" or "eai_noname" or "dns_resolution_failed" => "dns_error",
            // SSRF block from the worker DNS guard — canonicalize onto the documented private-URL code.
            "private_ip_blocked" => "private_url_blocked",
            // TLS/certificate failures — typed separately (persistent, not retryable).
            "cert_has_expired"
                or "depth_zero_self_signed_cert"
                or "self_signed_cert_in_chain"
                or "unable_to_verify_leaf_signature"
                or "err_tls_cert_altname_invalid"
                or "cert_untrusted" => "tls_error",
            "econnreset" or "etimedout" or "econnrefused" => "network_error",
            // Defense-in-depth: a raw JS error name must never leak to the agent as a failure code
            // (a worker catch-all once emitted "typeerror" etc.). Fold them into the taxonomy. Q-005.
            "typeerror" or "referenceerror" or "rangeerror" or "syntaxerror"
                or "evalerror" or "urierror" or "error" => "extraction_failed",
            _ => c,
        };
    }

    /// <summary>Canonical transcode failure code — prefers HTTP status, then worker http_* token.</summary>
    public static string ResolveTranscodeFailure(string? workerFailure, int statusCode = 0)
    {
        if (statusCode > 0)
        {
            var fromStatus = FromHttpStatus(statusCode);
            if (fromStatus is not null)
            {
                return Normalize(fromStatus);
            }
        }

        if (!string.IsNullOrWhiteSpace(workerFailure))
        {
            var normalized = Normalize(workerFailure);
            if (normalized.StartsWith("http_", StringComparison.Ordinal))
            {
                return normalized;
            }

            if (IsInfrastructureFailure(workerFailure))
            {
                return "workers_unavailable";
            }

            if (normalized is "timeout" or "network_error" or "dns_error" or "tls_error")
            {
                return normalized;
            }

            if (normalized is "extraction_failed" or "content_extraction_failed")
            {
                return "extraction_failed";
            }

            return normalized;
        }

        return "extraction_failed";
    }

    public static bool IsRetryable(string failureCode) =>
        failureCode is "timeout" or "network_error" or "dns_error" or "thin_extract"
        || failureCode == "http_429"
        || (failureCode.StartsWith("http_5", StringComparison.Ordinal) && failureCode.Length >= 6);

    public static string FormatProbeMessage(string? failureCode, int statusCode)
    {
        if (!string.IsNullOrWhiteSpace(failureCode) && failureCode.StartsWith("http_", StringComparison.Ordinal))
        {
            return statusCode > 0
                ? $"HTTP {statusCode} ({failureCode})."
                : failureCode;
        }

        return failureCode switch
        {
            "timeout" => "Probe timed out.",
            "dns_error" => "DNS resolution failed for the host.",
            "tls_error" => "TLS/certificate error while probing the URL.",
            "network_error" => "Network error while probing the URL.",
            "unsupported_content_type" => "URL is not HTML or PDF.",
            "invalid_url" => "URL is not valid.",
            _ => "Probe could not classify the URL.",
        };
    }

    public static string FormatExtractKnowledgeMessage(string failureCode, string? workerRaw = null) =>
        FormatTranscodeMessage(failureCode, TryParseHttpStatusCode(failureCode), workerRaw);

    public static string FormatTranscodeMessage(string failureCode, int statusCode, string? workerRaw = null) =>
        failureCode switch
        {
            _ when failureCode.StartsWith("http_", StringComparison.Ordinal) =>
                statusCode > 0
                    ? $"HTTP {statusCode} ({failureCode})."
                    : $"HTTP error ({failureCode}).",
            "timeout" => "Occam worker timed out.",
            "workers_unavailable" when workerRaw is "playwright_missing" =>
                "Occam browser worker failed to start. Run occam doctor.",
            // A no_json exit can come from EITHER worker — this method only sees the raw worker string,
            // not the backend, so don't claim it was the browser (that mislabelling sent a real HTTP-worker
            // crash on a chase through the browser stack). The raw tail below carries the real detail.
            "workers_unavailable" when workerRaw is "no_json" =>
                "Occam worker exited without JSON (crash or OOM). Reload MCP after doctor; try raising OCCAM_NODE_MAX_OLD_SPACE_MB (browser: OCCAM_BROWSER_NODE_MAX_OLD_SPACE_MB).",
            "workers_unavailable" when workerRaw?.StartsWith("no_json:", StringComparison.Ordinal) == true =>
                $"Occam worker crashed before JSON: {workerRaw["no_json:".Length..].Trim()}",
            "workers_unavailable" => "Occam workers are not ready.",
            "thin_extract" => "Extracted content is too thin or empty after compile.",
            "content_selectors_miss" => "None of the content_selectors matched any section.",
            "captcha_or_challenge" => "Occam extract hit an anti-bot or Cloudflare challenge page.",
            "requires_login" => "Page likely requires login and no session_profile was provided.",
            "session_profile_not_found" => "session_profile file was not found or could not be read.",
            "invalid_session_profile" => "session_profile id is invalid.",
            "private_url_blocked" => "Private or local URLs are blocked.",
            "robots_disallowed" => "Fetch disallowed by the site's robots.txt (OCCAM_RESPECT_ROBOTS=1).",
            "invalid_arguments" => workerRaw ?? "Invalid transcode arguments.",
            "invalid_policy" => "Unknown backend policy.",
            "network_error" => "Network error while fetching the URL.",
            "dns_error" => "DNS resolution failed for the host — check the domain name or network.",
            "tls_error" => "TLS/certificate error — the site certificate is invalid, expired, or untrusted.",
            "response_too_large" => "HTTP response body exceeded OCCAM_MAX_RESPONSE_BYTES — increase cap or skip URL.",
            "response_truncated" => "HTTP body exceeded cap; partial markdown only — do not cite as full page.",
            "extraction_failed" => $"Occam extract failed: {workerRaw ?? "empty markdown"}.",
            "playbook_not_found" => "No playbook matches the host.",
            "knowledge_schema_missing" => "Playbook has no knowledge_schema block — call occam_playbook_resolve first.",
            "page_class_unmatched" => "URL did not match any page_class and no default schema exists.",
            "knowledge_schema_empty" => "Matched page class has zero schema fields.",
            _ => workerRaw is not null
                ? $"Occam extract failed: {workerRaw}."
                : "Occam transcode failed.",
        };

    private static bool IsInfrastructureFailure(string? workerFailure) =>
        workerFailure is "no_json" or "playwright_missing" or "worker_missing" or "spawn_failed" or "bad_json"
        || workerFailure?.StartsWith("no_json:", StringComparison.Ordinal) == true;
}
