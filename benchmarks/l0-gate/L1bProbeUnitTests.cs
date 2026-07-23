using OccamMcp.Core.Access;
using OccamMcp.Core.Abstractions;
using OccamMcp.Core.PostProcessors;
using OccamMcp.Core.Probe;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Text;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

internal static class L1bProbeUnitTests
{
    // Regex baseline captured 2026-06-15 (Tier-A phase 3 parity lock).
    private const int BaselineDocsVisibleText = 185;
    private const int BaselineSpaVisibleText = 3;
    private const int BaselineOpenAiVisibleText = 858;
    private const int BaselineIgVisibleText = 427;
    private const int BaselineLiVisibleText = 429;

    public static void Run(Action<string, bool> assert)
    {
        RunClassifier(assert);
        RunChallengeDetector(assert);
        RunDomainTier(assert);
        RunLoginWall(assert);
        RunSharedAccessClassifier(assert);
        RunHttpThenBrowserPublicReference(assert);
        RunSocialMeta(assert);
    }

    private static void RunClassifier(Action<string, bool> assert)
    {
        var docsHtml = """
            <!DOCTYPE html><html><head><title>API Guide</title></head>
            <body><main><h1>API Guide</h1><p>Long documentation content about authentication and tokens for developers.</p>
            <p>More paragraphs with enough visible text to avoid SPA shell detection in probe classifier.</p></main></body></html>
            """;
        var fetch = new ProbeFetchResult
        {
            Ok = true,
            StatusCode = 200,
            RequestedUrl = "https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide",
            FinalUrl = "https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide",
            HtmlBytes = docsHtml.Length,
            HtmlSample = docsHtml,
        };
        var classification = HtmlProbeClassifier.Classify(fetch);
        assert("l1b classify documentation", classification.PageClass is "documentation" or "static_article");
        assert("l1b classify static docs no js", !classification.Signals.RequiresJavascript);
        assert("l1b visible text docs fixture", HtmlProbeClassifierVisibleText(docsHtml) == BaselineDocsVisibleText);

        var spaHtml = """
            <!DOCTYPE html><html><head><title>SPA</title></head>
            <body><div id="__nuxt__"></div><script>window.__NUXT__={}</script></body></html>
            """;
        var spaFetch = new ProbeFetchResult
        {
            Ok = true,
            StatusCode = 200,
            RequestedUrl = "https://nuxt.com/docs",
            FinalUrl = "https://nuxt.com/docs",
            HtmlBytes = spaHtml.Length,
            HtmlSample = spaHtml,
        };
        var spaClass = HtmlProbeClassifier.Classify(spaFetch);
        assert("l1b classify spa shell", spaClass.Signals.SpaShell);
        assert("l1b classify spa requires js", spaClass.Signals.RequiresJavascript);
        assert("l1b visible text spa fixture", HtmlProbeClassifierVisibleText(spaHtml) == BaselineSpaVisibleText);
    }

    private static void RunChallengeDetector(Action<string, bool> assert)
    {
        var html = "<html><body>Just a moment... Checking your browser before accessing. Ray ID: abc</body></html>";
        var hint = ChallengeKindClassifier.DetectHint(html, 503, "https://example.com");
        assert("l1b challenge js_challenge", hint?.Kind == "js_challenge");
        assert("l1b challenge not heal eligible", hint is { HealEligible: false });

        var docsHtml = """
            <!DOCTYPE html><html><head><title>API Guide</title></head>
            <body><main><h1>API Guide</h1><h2>Rate limits</h2>
            <p>Rate limits protect the platform. Each API key has a per-minute request quota and retry guidance.</p>
            <p>More documentation paragraphs with enough visible text to avoid false challenge detection on docs pages.</p>
            </main></body></html>
            """;
        var rateLimitHint = ChallengeKindClassifier.DetectHint(docsHtml, 200, "https://platform.openai.com/docs/concepts/rate-limits", 0.25);
        assert("l1b docs rate limits not challenge", rateLimitHint is null);

        var openAiHtml = """
            <!DOCTYPE html><html><head><title>Key concepts</title></head>
            <body><main><h1>Key concepts</h1><h2>Text generation models</h2>
            <p>Text generation models are a type of large language model that can generate text.</p>
            <h2>Rate limits</h2>
            <p>Rate limits protect the API. Each key has a per-minute quota and retry guidance for rate limit errors.</p>
            <p>Additional documentation paragraphs with enough visible prose to represent a real docs article body on OpenAI platform.</p>
            <p>More content about embeddings, tokens, and tokenization for developers reading the concepts guide page.</p>
            <p>Tokenization splits text into tokens for model input. Embeddings map text to vectors for semantic search and clustering tasks.</p>
            <p>Developers should understand context windows, prompt design, and how rate limits apply per organization and per API key tier.</p>
            <p>Further sections describe fine-tuning, batch APIs, structured outputs, and safety tooling available on the platform documentation site.</p>
            </main></body></html>
            """;
        var openAiHint = ChallengeKindClassifier.DetectHint(
            openAiHtml,
            200,
            "https://developers.openai.com/api/docs/concepts",
            0.04,
            1200);
        assert("l1b openai low ratio high prose not challenge", openAiHint is null);

        var truncatedOpenAi = openAiHtml[..Math.Min(openAiHtml.Length, 256 * 1024)];
        assert("l1b visible text openai truncated", HtmlProbeClassifierVisibleText(truncatedOpenAi) == BaselineOpenAiVisibleText);
        var truncatedHint = ChallengeKindClassifier.DetectHint(
            truncatedOpenAi,
            200,
            "https://developers.openai.com/api/docs/concepts",
            0.04,
            HtmlProbeClassifierVisibleText(truncatedOpenAi));
        assert("l1b openai truncated sample not challenge", truncatedHint is null);

        var truncatedClass = HtmlProbeClassifier.Classify(new ProbeFetchResult
        {
            Ok = true,
            StatusCode = 200,
            RequestedUrl = "https://platform.openai.com/docs/concepts",
            FinalUrl = "https://developers.openai.com/api/docs/concepts",
            HtmlBytes = truncatedOpenAi.Length,
            HtmlSample = truncatedOpenAi,
        });
        assert("l1b openai probe transcode consistent", !truncatedClass.Signals.LikelyChallenge);
        assert("l1b openai probe not challenge class", truncatedClass.PageClass != "challenge");

        var igHtml = """
            <!DOCTYPE html><html><head><title>Nat Geo (@natgeo) • Instagram</title>
            <meta property="og:description" content="Step into wonder with National Geographic." /></head>
            <body><main><h1>natgeo</h1>
            <p>269M followers · Step into wonder with National Geographic photography and exploration stories from every corner of the planet.</p>
            <p>Watch a mole-rat farmer tend underground crops in one of our latest reels from the field assignment team.</p>
            <p>Explore climate, wildlife, and human stories through field reporting, maps, and long-form features curated for curious readers worldwide.</p>
            <script src="https://newassets.hcaptcha.com/captcha/v1/api.js"></script>
            <div class="login">Log in to see more</div></main></body></html>
            """;
        var igProse = HtmlProbeClassifierVisibleText(igHtml);
        assert("l1b visible text instagram fixture", igProse == BaselineIgVisibleText);
        var igHint = ChallengeKindClassifier.DetectHint(
            igHtml,
            200,
            "https://www.instagram.com/natgeo/",
            0.03,
            igProse);
        assert("l1b instagram hcaptcha widget not hard stop", igHint is null);

        var igClass = HtmlProbeClassifier.Classify(new ProbeFetchResult
        {
            Ok = true,
            StatusCode = 200,
            RequestedUrl = "https://www.instagram.com/natgeo/",
            FinalUrl = "https://www.instagram.com/natgeo/",
            HtmlBytes = igHtml.Length,
            HtmlSample = igHtml,
        });
        assert("l1b instagram public profile not challenge class", igClass.PageClass != "challenge");
        assert("l1b instagram public profile no likely challenge", !igClass.Signals.LikelyChallenge);

        var liHtml = """
            <!DOCTYPE html><html><head><title>Microsoft | LinkedIn</title></head>
            <body><main><h1>Microsoft</h1>
            <p>Our mission is to empower every person and every organization on the planet to achieve more.</p>
            <p>Updates about Project Ex Vivo, AI Diffusion Report, and company news for partners and customers worldwide.</p>
            <p>Join our community of professionals exploring cloud, productivity, and AI innovation across industries.</p>
            <div>captcha verify you are human widget embedded in footer scripts for bot detection on some pages</div>
            </main></body></html>
            """;
        var liProse = HtmlProbeClassifierVisibleText(liHtml);
        assert("l1b visible text linkedin fixture", liProse == BaselineLiVisibleText);
        var liHint = ChallengeKindClassifier.DetectHint(
            liHtml,
            200,
            "https://www.linkedin.com/company/microsoft/",
            0.04,
            liProse);
        assert("l1b linkedin generic captcha noise suppressed", liHint is null);
    }

    private static int HtmlProbeClassifierVisibleText(string html) =>
        HtmlVisibleTextScanner.CountVisibleText(html);

    private static void RunLoginWall(Action<string, bool> assert)
    {
        // WebFetch baseline: ngx_http_core_module — auth_delay mentions "password"; proxy_pass uses login.example.com
        const string nginxCoreUrl = "https://nginx.org/en/docs/http/ngx_http_core_module.html";
        var nginxModuleMd = """
            # Module ngx_http_core_module

            ## auth_delay

            Delays processing of unauthorized requests with 401 response code to prevent timing attacks when access is limited by password, by the result of subrequest, or by JWT.

            ## listen

            Sets the addresses and port for the accepted connections. Syntax: `listen` address[:port] ...

            ## server_name

            Sets names of a virtual server, for example:

            ```
            location = /user {
                proxy_pass http://login.example.com;
            }
            ```
            """;

        assert("l1b login wall nginx core module not wall", !LoginWallDetector.LooksLikeLoginWall(nginxModuleMd, nginxCoreUrl));
        assert("l1b login wall tier_a_docs reference", DomainTierRegistry.IsTierADocsReferencePage(nginxCoreUrl));
        assert("l1b public reference rfc-editor", DomainTierRegistry.IsPublicReferencePage("https://www.rfc-editor.org/rfc/rfc9110.html"));
        assert("l1b public reference wikipedia", DomainTierRegistry.IsPublicReferencePage("https://en.wikipedia.org/wiki/JavaScript"));

        var rfcLoginMd = """
            # RFC 9110

            The user authentication scheme is based on a username and password.
            Clients MUST NOT send passwords in clear text.
            Authentication required for some origins is described in Section 11.
            """;
        assert("l1b login wall rfc spec not wall", !LoginWallDetector.LooksLikeLoginWall(rfcLoginMd, "https://www.rfc-editor.org/rfc/rfc9110.html"));

        var realWallMd = "# Sign in\n\nPlease log in to continue.\n\nPassword: ______";
        assert("l1b login wall explicit phrase still stops", LoginWallDetector.LooksLikeLoginWall(realWallMd, "https://app.example.com/dashboard"));

        var loginPathUrl = "https://nginx.org/en/login";
        assert("l1b login wall login path still stops", LoginWallDetector.LooksLikeLoginWall("public docs", loginPathUrl));

        assert("l1b login wall ssl_password_file not password word", !TextNeedle.ContainsWord("ssl_password_file directive", "password"));
        assert("l1b login wall hostname login not phrase", !TextNeedle.ContainsAnyPhrase("proxy_pass http://login.example.com;", "sign in", "log in", "log in to", "sign in to"));
    }

    private static void RunSharedAccessClassifier(Action<string, bool> assert)
    {
        const string publicUrl = "https://example.com/docs/authentication";
        var terminology = AccessClassifier.Classify(AccessEvidenceAdapters.FromMarkdown(
            "# Authentication\n\nThis guide explains identity, passwords, and how to sign in to the API.",
            publicUrl,
            publicUrl,
            200));
        assert("l1b access terminology alone unknown", terminology.Disposition == AccessDisposition.Unknown);
        assert("l1b access terminology alone no login", !terminology.RequiresLogin);

        var pathOnly = AccessClassifier.Classify(AccessEvidenceAdapters.FromMarkdown(
            "# Public documentation\n\nThis page contains enough public guidance to remain usable without an account.",
            "https://example.com/login",
            "https://example.com/login",
            200));
        assert("l1b access login path alone unknown", pathOnly.Disposition == AccessDisposition.Unknown);
        assert("l1b access login path alone no hard stop", !pathOnly.RequiresLogin);

        const string realWall = "# Sign in\n\nEmail address\n\nPassword\n\nSign in";
        var restricted = AccessClassifier.Classify(AccessEvidenceAdapters.FromMarkdown(
            realWall,
            "https://app.example.com/dashboard",
            "https://app.example.com/dashboard",
            200));
        assert("l1b access blocking identity ui restricted", restricted.Disposition == AccessDisposition.Restricted);
        assert("l1b access restricted has stable code", restricted.EvidenceCodes.Contains("blocking_identity_ui", StringComparer.Ordinal));

        var processor = new RequiresLoginPostProcessor();
        var context = new TranscodeContext(
            publicUrl,
            OccamBackendPolicy.HttpThenBrowser,
            new OccamTranscodeOptions());
        var proseOutcome = new TranscodeOutcome(
            true,
            "# Authentication\n\nAuthentication required is an HTTP concept described by this public guide.",
            publicUrl,
            "http",
            null,
            null,
            StatusCode: 200);
        var proseProcessed = processor.Process(proseOutcome, context);
        assert("l1b access postprocessor keeps public prose", proseProcessed.Ok);
        assert("l1b access postprocessor records unknown", proseProcessed.AccessAssessment?.Disposition == AccessDisposition.Unknown);

        var wallOutcome = new TranscodeOutcome(
            true,
            realWall,
            "https://app.example.com/dashboard",
            "browser",
            null,
            null,
            StatusCode: 200);
        var wallProcessed = processor.Process(
            wallOutcome,
            new TranscodeContext(
                "https://app.example.com/dashboard",
                OccamBackendPolicy.Browser,
                new OccamTranscodeOptions()));
        assert("l1b access postprocessor stops blocking ui", !wallProcessed.Ok && wallProcessed.FailureCode == "requires_login");
        assert("l1b access postprocessor preserves assessment", wallProcessed.AccessAssessment?.Disposition == AccessDisposition.Restricted);
    }

    private static void RunHttpThenBrowserPublicReference(Action<string, bool> assert)
    {
        const string rfcUrl = "https://www.rfc-editor.org/rfc/rfc9110.html";
        const string rfcMd = "# RFC 9110: HTTP Semantics\n\n## 1. Introduction\n\nThe Hypertext Transfer Protocol (HTTP) is a family of stateless, application-level, request/response protocols.";
        var httpBackend = new CountingStubBackend(
            "http",
            _ => new ExtractRunResult(true, rfcMd, "local_http", null, 120, rfcUrl, false));
        var browserBackend = new CountingStubBackend(
            "browser",
            _ => new ExtractRunResult(false, null, "browser_playwright", "extraction_failed", 500, rfcUrl, false));

        var router = new OccamRouter([httpBackend, browserBackend]);
        var outcome = router.Transcode(rfcUrl, OccamBackendPolicy.HttpThenBrowser, CancellationToken.None);

        assert("l1b http_then_browser rfc ok", outcome.Ok);
        assert("l1b http_then_browser rfc markdown", outcome.Markdown?.Contains("RFC 9110", StringComparison.Ordinal) == true);
        assert("l1b http_then_browser rfc skips browser", httpBackend.CallCount == 1 && browserBackend.CallCount == 0);

        var spaHttp = new CountingStubBackend(
            "http",
            _ => new ExtractRunResult(false, null, "local_http", "extraction_failed", 80, "https://dev.to/x", false));
        var spaBrowser = new CountingStubBackend(
            "browser",
            _ => new ExtractRunResult(true, "# Article\n\nBody text.", "browser_playwright", null, 200, "https://dev.to/x", false));
        var spaRouter = new OccamRouter([spaHttp, spaBrowser]);
        var spaOutcome = spaRouter.Transcode("https://dev.to/sylwia-lask/article", OccamBackendPolicy.HttpThenBrowser, CancellationToken.None);

        assert("l1b http_then_browser spa escalates browser", spaHttp.CallCount == 1 && spaBrowser.CallCount == 1);
        assert("l1b http_then_browser spa ok", spaOutcome.Ok);
    }

    private sealed class CountingStubBackend(string name, Func<string, ExtractRunResult> extract) : IExtractBackend
    {
        public string Name => name;
        public bool IsReady => true;
        public int CallCount { get; private set; }

        public ValueTask<ExtractRunResult> ExtractAsync(string url, CancellationToken cancellationToken)
        {
            CallCount++;
            return ValueTask.FromResult(extract(url));
        }
    }

    private static void RunSocialMeta(Action<string, bool> assert)
    {
        var forward = """
            <!DOCTYPE html><html lang="en-US"><head>
            <meta property="og:title" content="Forward OG Title" />
            <meta property="og:description" content="Forward OG Desc" />
            <meta property="og:image" content="/assets/hero.png" />
            <meta property="og:site_name" content="Example Docs" />
            <meta property="twitter:card" content="summary" />
            <meta name="title" content="Name Title Fallback" />
            </head><body></body></html>
            """;
        var forwardMeta = HtmlSocialMetaExtractor.Extract(forward, "https://example.com/docs/page");
        assert("l1b social forward og:title", forwardMeta.Title == "Forward OG Title");
        assert("l1b social forward og:description", forwardMeta.Description == "Forward OG Desc");
        assert("l1b social forward og:image abs", forwardMeta.Image == "https://example.com/assets/hero.png");
        assert("l1b social forward site_name", forwardMeta.SiteName == "Example Docs");
        assert("l1b social forward twitter:card", forwardMeta.TwitterCard == "summary");
        assert("l1b social forward html lang", forwardMeta.Lang == "en-US");

        var reverse = """
            <html lang='ru'><head>
            <meta content="Reverse Title" property="og:title" />
            <meta content="Reverse Desc" name="description" />
            <meta content='/img/cover.jpg' property='og:image' />
            <meta content="summary_large_image" name="twitter:card" />
            </head></html>
            """;
        var reverseMeta = HtmlSocialMetaExtractor.Extract(reverse, "https://cdn.example.com/blog/post");
        assert("l1b social reverse og:title", reverseMeta.Title == "Reverse Title");
        assert("l1b social reverse name description", reverseMeta.Description == "Reverse Desc");
        assert("l1b social reverse og:image abs", reverseMeta.Image == "https://cdn.example.com/img/cover.jpg");
        assert("l1b social reverse twitter name", reverseMeta.TwitterCard == "summary_large_image");
        assert("l1b social reverse html lang", reverseMeta.Lang == "ru");

        var uppercase = """
            <HTML LANG="de"><HEAD>
            <META PROPERTY="OG:TITLE" CONTENT="Upper Title" />
            <META NAME="DESCRIPTION" CONTENT="Upper Desc" />
            </HEAD><BODY></BODY></HTML>
            """;
        var upperMeta = HtmlSocialMetaExtractor.Extract(uppercase);
        assert("l1b social uppercase og:title", upperMeta.Title == "Upper Title");
        assert("l1b social uppercase name description", upperMeta.Description == "Upper Desc");
        assert("l1b social uppercase lang", upperMeta.Lang == "de");

        var titleFallback = """
            <head>
            <meta name="title" content="Only Name Title" />
            <meta name="description" content="Only Name Desc" />
            </head>
            """;
        var fallbackMeta = HtmlSocialMetaExtractor.Extract(titleFallback);
        assert("l1b social name title fallback", fallbackMeta.Title == "Only Name Title");
        assert("l1b social name description fallback", fallbackMeta.Description == "Only Name Desc");

        var htmlEntities = """
            <head><meta property="og:title" content="Fish &amp; Chips" /></head>
            """;
        var entityMeta = HtmlSocialMetaExtractor.Extract(htmlEntities);
        assert("l1b social html decode", entityMeta.Title == "Fish & Chips");

        var empty = HtmlSocialMetaExtractor.Extract("   ");
        assert("l1b social empty html", empty.Title is null && empty.Lang is null);

        var bodyIgnored = """
            <head><meta property="og:title" content="Head Title" /></head>
            <body><meta property="og:title" content="Body Title" /></body>
            """;
        var headOnly = HtmlSocialMetaExtractor.Extract(bodyIgnored);
        assert("l1b social head only not body", headOnly.Title == "Head Title");
    }

    private static void RunDomainTier(Action<string, bool> assert)
    {
        var tier = DomainTierRegistry.TryResolve("https://nginx.org/en/docs/");
        assert("l1b tier nginx docs", tier?.TierId == "tier_a_docs");
        assert("l1b tier nginx http only", tier?.HttpOnly == true);

        var spaTier = DomainTierRegistry.TryResolve("https://nuxt.com/docs/getting-started/introduction");
        assert("l1b tier nuxt spa_docs", spaTier?.TierId == "spa_docs");
    }
}
