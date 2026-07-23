using OccamMcp.Core.Access;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Text;

namespace OccamMcp.Core.Probe;

public sealed class PageClassification
{
    public required string PageClass { get; init; }
    public required ProbeSignals Signals { get; init; }
    public required string[] RiskFlags { get; init; }
    public double VisibleTextRatio { get; init; }
    public double ScriptDensity { get; init; }
    public ChallengeHint? Challenge { get; init; }
    public AccessAssessment? Access { get; init; }
}

public static class HtmlProbeClassifier
{
    public static PageClassification Classify(ProbeFetchResult fetch)
    {
        var baseline = ClassifyLegacy(fetch);
        var access = AccessClassifier.Classify(AccessEvidenceAdapters.FromProbeFetch(fetch));
        var likelyLogin = access.RequiresLogin;
        var baseSignals = baseline.Signals;
        var pageUrl = fetch.FinalUrl ?? fetch.RequestedUrl;
        var pageClass = likelyLogin
            ? "login"
            : baseline.PageClass == "login"
                ? ResolvePageClass(
                    (fetch.HtmlSample ?? string.Empty).ToLowerInvariant(),
                    login: false,
                    baseSignals.LikelyCookieConsent,
                    baseSignals.LikelyChallenge,
                    pageUrl)
                : baseline.PageClass;
        var signals = new ProbeSignals
        {
            PageClass = pageClass,
            RequiresJavascript = baseSignals.RequiresJavascript,
            SpaShell = baseSignals.SpaShell,
            LikelyCookieConsent = baseSignals.LikelyCookieConsent,
            LikelyChallenge = baseSignals.LikelyChallenge,
            LikelyLoginRequired = likelyLogin,
            LikelyPaywall = baseSignals.LikelyPaywall,
            VisibleTextRatio = baseSignals.VisibleTextRatio,
            HtmlBytes = baseSignals.HtmlBytes,
            HasTables = baseSignals.HasTables,
            HasLlmsTxtLink = baseSignals.HasLlmsTxtLink,
        };
        var risks = baseline.RiskFlags.Where(flag => flag != "login_required").ToList();
        if (likelyLogin)
        {
            risks.Add("login_required");
        }

        return new PageClassification
        {
            PageClass = pageClass,
            Signals = signals,
            RiskFlags = risks.ToArray(),
            VisibleTextRatio = baseline.VisibleTextRatio,
            ScriptDensity = baseline.ScriptDensity,
            Challenge = baseline.Challenge,
            Access = access,
        };
    }

    /// <summary>Frozen RC.1 behavior retained only for explicit baseline characterization.</summary>
    public static PageClassification ClassifyLegacy(ProbeFetchResult fetch)
    {
        var html = fetch.HtmlSample ?? string.Empty;
        var lower = html.ToLowerInvariant();
        var visibleText = EstimateVisibleText(html);
        var htmlLen = Math.Max(html.Length, 1);
        // (double) cast is load-bearing: visibleText and htmlLen are both int, so `visibleText / htmlLen`
        // was integer division and collapsed to 0 for every real page (visible text is always < raw
        // HTML length). That pinned visibleRatio at ~0, which silently tripped the spaShell ratio
        // fallback and the low-visible-text paths on content-rich pages that merely happened not to sit
        // in an HttpOnly tier (e.g. docs.python.org).
        var visibleRatio = Math.Min(1.0, (double)visibleText / htmlLen);
        var scriptCount = CountOccurrences(lower, "<script");
        var scriptDensity = Math.Min(1.0, scriptCount / 40.0);

        var hasTables = CountOccurrences(lower, "<table") > 0;
        var hasLlmsTxtLink = ContainsAny(lower, "href=\"/llms.txt\"", "href=\"llms.txt\"", "rel=\"llms-txt\"");

        var likelyConsent = ContainsAny(lower,
            "cookie consent", "accept all", "onetrust", "gdpr", "privacy settings",
            "we use cookies", "consent banner");
        var likelyChallenge = ContainsAny(lower,
            "captcha-delivery.com", "geo.captcha-delivery.com",
            "cf-challenge", "hcaptcha", "turnstile",
            "just a moment", "checking your browser",
            "performance & security by cloudflare", "ray id:",
            "verify you are human", "attention required",
            "why have i been blocked")
            || (ContainsAny(lower, "captcha", "please enable js and disable any ad blocker")
                && visibleRatio < 0.05);
        var pageUrl = fetch.FinalUrl ?? fetch.RequestedUrl;
        var likelyLogin = DomainTierRegistry.IsLoginPath(pageUrl)
            || ContainsAny(lower,
                "sign in to continue", "log in to continue",
                "authentication required")
            || (!DomainTierRegistry.IsPublicReferencePage(pageUrl)
                && HasDedicatedLoginWall(lower, visibleRatio));
        var likelyPaywall = ContainsAny(lower,
            "subscribe to read", "paywall", "premium subscriber", "unlock this article");
        var spaShell = SpaShellDetector.DetectFromHtml(html, out _)
            || (visibleRatio < 0.02 && scriptDensity > 0.15);

        // Script density and a bare <noscript> only imply "needs a browser" when the static HTML does
        // NOT already carry the readable content. Large static docs pages (python docs, MDN reference)
        // routinely ship 10+ <script> tags (search, syntax highlight, version switcher) yet render
        // fully over plain HTTP — so a content-rich page is never requiresJavascript on those signals
        // alone. spaShell stays authoritative: it already requires thin text (< StubVisibleTextMax).
        var contentIsSelfSufficient = visibleText >= SpaShellDetector.StubVisibleTextMax;
        var requiresJavascript = spaShell
            || (!contentIsSelfSufficient && (scriptDensity > 0.25 || lower.Contains("noscript")));

        var challenge = ChallengeKindClassifier.DetectHint(
            html,
            fetch.StatusCode,
            pageUrl,
            visibleRatio,
            visibleText);
        if (challenge is not null)
        {
            likelyChallenge = true;
        }

        if (likelyChallenge
            && (DomainTierRegistry.ShouldSuppressProbeChallengeStop(pageUrl, visibleRatio, visibleText)
                || (DomainTierRegistry.IsPublicReferencePage(pageUrl) && visibleText >= 500)))
        {
            likelyChallenge = false;
            challenge = null;
        }

        var pageClass = ResolvePageClass(lower, likelyLogin, likelyConsent, likelyChallenge, fetch.FinalUrl ?? fetch.RequestedUrl);

        var signals = new ProbeSignals
        {
            PageClass = pageClass,
            RequiresJavascript = requiresJavascript,
            SpaShell = spaShell,
            LikelyCookieConsent = likelyConsent,
            LikelyChallenge = likelyChallenge,
            LikelyLoginRequired = likelyLogin,
            LikelyPaywall = likelyPaywall,
            VisibleTextRatio = visibleRatio,
            HtmlBytes = fetch.HtmlBytes,
            HasTables = hasTables,
            HasLlmsTxtLink = hasLlmsTxtLink,
        };

        var risks = new List<string>();
        if (likelyConsent) risks.Add("cookie_consent");
        if (likelyChallenge) risks.Add("anti_bot_challenge");
        if (challenge is not null) risks.Add($"challenge_kind:{challenge.Kind}");
        if (likelyLogin) risks.Add("login_required");
        if (likelyPaywall) risks.Add("paywall");
        if (spaShell) risks.Add("spa_shell");
        if (visibleRatio < 0.03) risks.Add("low_visible_text");

        return new PageClassification
        {
            PageClass = pageClass,
            Signals = signals,
            RiskFlags = risks.ToArray(),
            VisibleTextRatio = visibleRatio,
            ScriptDensity = scriptDensity,
            Challenge = challenge,
        };
    }

    private static bool HasDedicatedLoginWall(string lower, double visibleRatio)
    {
        if (!ContainsAny(lower, "type=\"password\"", "type='password'"))
        {
            return false;
        }

        var hasLoginUi = ContainsAny(lower,
            "sign in", "log in", "login-form", "id=\"login", "class=\"login",
            "please log in", "please sign in", "member login");
        return hasLoginUi || (visibleRatio < 0.04 && lower.Contains("password", StringComparison.Ordinal));
    }

    private static string ResolvePageClass(string lower, bool login, bool consent, bool challenge, string url)
    {
        if (login) return "login";
        if (challenge) return "challenge";
        var host = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : "";
        if (consent
            || DomainTierRegistry.IsNewsConsentTier(url)
            || host.Contains("bbc.", StringComparison.Ordinal)
            || host.Contains("reuters.", StringComparison.Ordinal))
            return "news";
        if (host.Contains("docs.", StringComparison.Ordinal) || host.Contains("learn.microsoft", StringComparison.Ordinal))
            return "documentation";
        if (lower.Contains("<article") || lower.Contains("blog-post")) return "article";
        return "static_article";
    }

    private static int EstimateVisibleText(string html) =>
        HtmlVisibleTextScanner.CountVisibleText(html);

    private static int CountOccurrences(string text, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (text.Contains(needle, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
