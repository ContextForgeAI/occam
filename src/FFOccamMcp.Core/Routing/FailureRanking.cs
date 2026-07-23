namespace OccamMcp.Core.Routing;

/// <summary>
/// Ranks failure codes by how actionable they are to the agent, so when two extract attempts both fail we
/// surface the more useful one instead of a generic catch-all (which would mask, e.g., an http_403 behind an
/// extraction_failed). Used by the router's single http→browser cascade (B1) to choose the fallback result.
/// </summary>
internal static class FailureRanking
{
    public static int Informativeness(string? code) => code switch
    {
        "http_401" or "http_403" or "requires_login" => 100,
        "captcha_or_challenge" or "anti_bot_blocked" => 90,
        "tls_error" => 85,
        _ when code is not null && code.StartsWith("http_4", StringComparison.Ordinal) => 80,
        _ when code is not null && code.StartsWith("http_5", StringComparison.Ordinal) => 70,
        "thin_extract" => 60,
        "timeout" or "network_error" or "dns_error" => 50,
        "content_selectors_miss" => 40,
        _ => 10,
    };
}
