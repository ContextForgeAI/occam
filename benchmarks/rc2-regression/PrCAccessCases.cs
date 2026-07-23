using OccamMcp.Core.Access;
using OccamMcp.Core.Probe;

namespace OccamMcp.Rc2Regression;

internal static class PrCAccessCases
{
    public static void Run(TestHarness test, string root)
    {
        const string publicUrl = "https://standards.example/reference/authentication";
        const string openIdUrl = "https://identity.example/specs/openid-connect-core";
        const string loginUrl = "https://app.example/account/sign-in";

        var publicProbe = AssessHtml(Read(root, "access-public-auth.html"), publicUrl);
        var publicTranscode = AssessMarkdown(Read(root, "access-public-auth.md"), publicUrl);
        test.Check("D9", "authentication prose is non-decisive in both adapters",
            !publicProbe.RequiresLogin && !publicTranscode.RequiresLogin,
            $"probe={publicProbe.Disposition}; transcode={publicTranscode.Disposition}");

        var openIdProbe = AssessHtml(
            "<html><body><main>OpenID authentication specification without a password input.</main></body></html>",
            openIdUrl);
        var openIdTranscode = AssessMarkdown(Read(root, "access-openid.md"), openIdUrl);
        test.Check("D19", "probe and transcode share the same uncertain disposition",
            openIdProbe.Disposition == AccessDisposition.Unknown
            && openIdTranscode.Disposition == AccessDisposition.Unknown,
            $"probe={openIdProbe.Disposition}; transcode={openIdTranscode.Disposition}");

        var loginProbe = AssessHtml(Read(root, "access-real-login.html"), loginUrl);
        var loginTranscode = AssessMarkdown(Read(root, "access-real-login.md"), loginUrl);
        test.Check("D9", "blocking identity UI remains a true positive",
            loginProbe.RequiresLogin && loginTranscode.RequiresLogin,
            $"probe={string.Join(',', loginProbe.EvidenceCodes)}; transcode={string.Join(',', loginTranscode.EvidenceCodes)}");

        var widgetHtml = $"<main><article>{new string('x', 700)}</article><form><input type=\"email\"><input type=\"password\"><button>Log in</button></form></main>";
        var widget = AssessHtml(widgetHtml, "https://example.org/article");
        test.Check("D9", "login widget does not block usable public content",
            widget.Disposition == AccessDisposition.Open,
            $"disposition={widget.Disposition}; evidence={string.Join(',', widget.EvidenceCodes)}");

        var pathOnly = AssessMarkdown("Public documentation", "https://example.org/login");
        test.Check("D9", "login-like requested path alone is not strong access evidence",
            pathOnly.Disposition == AccessDisposition.Unknown,
            $"disposition={pathOnly.Disposition}");

        var redirected = AccessClassifier.Classify(new AccessEvidence(
            RedirectedToLogin: true,
            Stage: AccessEvidenceStage.Combined));
        test.Check("D9", "redirect to login is strong evidence",
            redirected.RequiresLogin && redirected.EvidenceCodes.Contains("redirected_to_login"),
            $"disposition={redirected.Disposition}");

        var challenge = AccessClassifier.Classify(new AccessEvidence(
            StatusCode: 401,
            HasAuthenticationChallenge: true,
            Stage: AccessEvidenceStage.Prefetch));
        test.Check("D9", "401 authentication challenge is strong evidence",
            challenge.RequiresLogin
            && challenge.EvidenceCodes.Contains("http_401")
            && challenge.EvidenceCodes.Contains("authentication_challenge"),
            $"evidence={string.Join(',', challenge.EvidenceCodes)}");

        var first = AccessClassifier.Classify(AccessEvidenceAdapters.FromMarkdown(
            Read(root, "access-openid.md"), openIdUrl));
        var second = AccessClassifier.Classify(AccessEvidenceAdapters.FromMarkdown(
            Read(root, "access-openid.md"), openIdUrl));
        test.Check("D19", "access classification and diagnostics are deterministic",
            first.Disposition == second.Disposition
            && first.Confidence == second.Confidence
            && first.RecommendedAction == second.RecommendedAction
            && first.EvidenceCodes.SequenceEqual(second.EvidenceCodes),
            $"disposition={first.Disposition}; confidence={first.Confidence:0.00}");

        test.Check("D9", "diagnostics contain stable codes rather than page content",
            first.EvidenceCodes.All(code => !code.Contains("openid connect core", StringComparison.OrdinalIgnoreCase)),
            $"codes={string.Join(',', first.EvidenceCodes)}");
    }

    private static AccessAssessment AssessHtml(string html, string url)
    {
        var fetch = new ProbeFetchResult
        {
            Ok = true,
            StatusCode = 200,
            RequestedUrl = url,
            FinalUrl = url,
            HtmlBytes = System.Text.Encoding.UTF8.GetByteCount(html),
            HtmlSample = html,
        };
        return AccessClassifier.Classify(AccessEvidenceAdapters.FromProbeFetch(fetch));
    }

    private static AccessAssessment AssessMarkdown(string markdown, string url) =>
        AccessClassifier.Classify(AccessEvidenceAdapters.FromMarkdown(markdown, url));

    private static string Read(string root, string name) => File.ReadAllText(Path.Combine(root, name));
}
