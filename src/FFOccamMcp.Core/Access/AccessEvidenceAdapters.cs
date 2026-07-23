using OccamMcp.Core.Probe;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Text;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Access;

public static class AccessEvidenceAdapters
{
    private const int UsableVisibleTextCharacters = 600;

    public static AccessEvidence FromProbeFetch(ProbeFetchResult fetch)
    {
        var html = fetch.HtmlSample ?? string.Empty;
        var lower = html.ToLowerInvariant();
        var visibleCharacters = HtmlVisibleTextScanner.CountVisibleText(html);
        return new AccessEvidence(
            StatusCode: fetch.StatusCode,
            HasAuthenticationChallenge: fetch.HasAuthenticationChallenge,
            RedirectedToLogin: IsLoginRedirect(fetch.RequestedUrl, fetch.FinalUrl, fetch.RedirectChain),
            PasswordField: ContainsAny(lower, "type=\"password\"", "type='password'"),
            IdentityField: ContainsAny(lower,
                "type=\"email\"", "type='email'",
                "autocomplete=\"username\"", "autocomplete='username'",
                "name=\"username\"", "name='username'"),
            LoginFormAction: ContainsAny(lower,
                "action=\"/login", "action='/login", "action=\"/signin", "action='/signin",
                "action=\"/sign-in", "action='/sign-in", "action=\"/session", "action='/session")
                || (lower.Contains("<button", StringComparison.Ordinal)
                    && ContainsAny(lower, ">log in", ">sign in", ">login")),
            LoginHeading: ContainsAny(lower,
                "<h1>sign in", "<h1>log in", "<h1>login",
                "<h2>sign in", "<h2>log in", "<h2>login"),
            BlockingOverlay: ContainsAny(lower, "aria-modal=\"true\"", "aria-modal='true'", "class=\"login-modal", "class='login-modal"),
            HasUsableContent: visibleCharacters >= UsableVisibleTextCharacters,
            AuthenticationTerminology: ContainsAuthenticationTerminology(lower),
            Stage: AccessEvidenceStage.Dom);
    }

    public static AccessEvidence FromTranscode(
        WorkerAccessEvidenceInfo? workerEvidence,
        string? markdown,
        string requestedUrl,
        string? finalUrl,
        int statusCode)
    {
        if (workerEvidence is not null)
        {
            return new AccessEvidence(
                StatusCode: statusCode,
                HasAuthenticationChallenge: workerEvidence.HasAuthenticationChallenge,
                RedirectedToLogin: workerEvidence.RedirectedToLogin
                    || IsLoginRedirect(requestedUrl, finalUrl, redirectChain: null),
                PasswordField: workerEvidence.PasswordField,
                IdentityField: workerEvidence.IdentityField,
                LoginFormAction: workerEvidence.LoginFormAction,
                LoginHeading: workerEvidence.LoginHeading,
                BlockingOverlay: workerEvidence.BlockingOverlay,
                HasUsableContent: workerEvidence.HasUsableContent,
                AuthenticationTerminology: workerEvidence.AuthenticationTerminology,
                Stage: AccessEvidenceStage.Combined);
        }

        return FromMarkdown(markdown, requestedUrl, finalUrl, statusCode);
    }

    public static AccessEvidence FromMarkdown(
        string? markdown,
        string requestedUrl,
        string? finalUrl = null,
        int statusCode = 0)
    {
        var text = markdown ?? string.Empty;
        var lower = text.ToLowerInvariant();
        var lines = lower.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var passwordLabel = lines.Any(line => line is "password" or "password:" or "password: ______");
        var identityLabel = lines.Any(line => line is "email" or "email:" or "username" or "username:");
        var loginHeading = lines.Any(line => line is "# sign in" or "# log in" or "# login"
            or "## sign in" or "## log in" or "## login");
        var loginAction = lines.Any(line => line is "please log in to continue."
            or "please sign in to continue."
            or "log in"
            or "sign in");

        return new AccessEvidence(
            StatusCode: statusCode,
            RedirectedToLogin: IsLoginRedirect(requestedUrl, finalUrl, redirectChain: null),
            PasswordField: passwordLabel,
            IdentityField: identityLabel,
            LoginFormAction: loginAction,
            LoginHeading: loginHeading,
            HasUsableContent: text.Length >= UsableVisibleTextCharacters,
            AuthenticationTerminology: ContainsAuthenticationTerminology(lower),
            Stage: AccessEvidenceStage.Extracted);
    }

    private static bool IsLoginRedirect(
        string requestedUrl,
        string? finalUrl,
        IReadOnlyList<string>? redirectChain)
    {
        if (string.IsNullOrWhiteSpace(finalUrl)
            || string.Equals(requestedUrl, finalUrl, StringComparison.OrdinalIgnoreCase)
            || !DomainTierRegistry.IsLoginPath(finalUrl))
        {
            return false;
        }

        return redirectChain is null || redirectChain.Count > 0;
    }

    private static bool ContainsAuthenticationTerminology(string lower) =>
        ContainsAny(lower,
            "authentication", "authorization", "openid", "oauth", "bearer token",
            "basic auth", "password", "sign in", "log in", "login");

    private static bool ContainsAny(string text, params string[] needles) =>
        needles.Any(needle => text.Contains(needle, StringComparison.Ordinal));
}
