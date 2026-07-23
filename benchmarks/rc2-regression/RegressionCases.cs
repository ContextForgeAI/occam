using OccamMcp.Core.Access;
using OccamMcp.Core.Compile;
using OccamMcp.Core.PostProcessors;
using OccamMcp.Core.Probe;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Workers;

namespace OccamMcp.Rc2Regression;

internal static class RegressionCases
{
    private const string PublicSpecUrl = "https://standards.example/reference/authentication";
    private const string OpenIdUrl = "https://identity.example/specs/openid-connect-core";
    private const string RealLoginUrl = "https://app.example/account/sign-in";

    public static async Task<int> RunCharacterizationAsync(string fixtureRoot)
    {
        var test = new TestHarness("rc2-pr-a-characterization");
        AccessCharacterization(test, fixtureRoot);
        FocusCharacterization(test, fixtureRoot);
        BudgetCharacterization(test, fixtureRoot);
        SemanticCharacterization(test);
        LifecycleCharacterization(test);
        DigestParserCases.Run(test);
        await McpBoundaryCharacterization.RunAsync(test);
        return test.Finish();
    }

    public static async Task<int> RunExpectedRedAsync(string fixtureRoot)
    {
        var test = new TestHarness("rc2-pr-a-expected-red");
        AccessDesiredContract(test, fixtureRoot);
        FocusDesiredContract(test, fixtureRoot);
        BudgetDesiredContract(test, fixtureRoot);
        SemanticDesiredContract(test);
        LifecycleDesiredContract(test);
        await McpBoundaryCharacterization.RunDesiredContractAsync(test);
        return test.Finish();
    }

    private static void AccessCharacterization(TestHarness test, string root)
    {
        var publicHtml = Read(root, "access-public-auth.html");
        var publicMarkdown = Read(root, "access-public-auth.md");
        var openIdMarkdown = Read(root, "access-openid.md");
        var loginHtml = Read(root, "access-real-login.html");
        var loginMarkdown = Read(root, "access-real-login.md");

        var publicProbe = ProbeLegacy(publicHtml, PublicSpecUrl);
        var publicTx = LoginWallDetector.LooksLikeLoginWall(publicMarkdown, PublicSpecUrl);
        test.Check("D9", "public authentication prose is currently a probe login false positive",
            publicProbe.Signals.LikelyLoginRequired,
            $"probe={publicProbe.Signals.LikelyLoginRequired}; evidence=phrase:authentication_required");
        test.Check("D9", "public authentication prose is currently a transcode login false positive",
            publicTx, $"transcode={publicTx}; evidence=phrase:authentication_required");

        var openIdProbe = ProbeLegacy("<html><body><main>OpenID authentication specification without a password input.</main></body></html>", OpenIdUrl);
        var openIdTx = LoginWallDetector.LooksLikeLoginWall(openIdMarkdown, OpenIdUrl);
        test.Check("D19", "probe and transcode currently disagree on identity documentation",
            !openIdProbe.Signals.LikelyLoginRequired && openIdTx,
            $"probe={openIdProbe.Signals.LikelyLoginRequired}; transcode={openIdTx}; evidence=markdown password+sign-in prose");

        var loginProbe = ProbeLegacy(loginHtml, RealLoginUrl);
        var loginTx = LoginWallDetector.LooksLikeLoginWall(loginMarkdown, RealLoginUrl);
        test.Check("D9", "real login control remains a true positive",
            loginProbe.Signals.LikelyLoginRequired && loginTx,
            $"probe={loginProbe.Signals.LikelyLoginRequired}; transcode={loginTx}; evidence=password_control+login_path");

        var neutralProbe = ProbeLegacy(Read(root, "access-neutral.html"), "https://example.org/guide");
        test.Check("D9", "neutral documentation remains open", !neutralProbe.Signals.LikelyLoginRequired,
            $"probe={neutralProbe.Signals.LikelyLoginRequired}; evidence=none");
    }

    private static void AccessDesiredContract(TestHarness test, string root)
    {
        var publicHtml = Read(root, "access-public-auth.html");
        var publicMarkdown = Read(root, "access-public-auth.md");
        var openIdMarkdown = Read(root, "access-openid.md");
        var publicProbe = Probe(publicHtml, PublicSpecUrl);
        var publicTx = AccessClassifier.Classify(AccessEvidenceAdapters.FromMarkdown(publicMarkdown, PublicSpecUrl)).RequiresLogin;
        var openIdProbe = Probe("<html><body><main>OpenID authentication specification without a password input.</main></body></html>", OpenIdUrl);
        var openIdTx = AccessClassifier.Classify(AccessEvidenceAdapters.FromMarkdown(openIdMarkdown, OpenIdUrl)).RequiresLogin;

        test.Check("D9", "public prose must not produce a hard login verdict",
            !publicProbe.Signals.LikelyLoginRequired && !publicTx,
            $"probe={publicProbe.Signals.LikelyLoginRequired}; transcode={publicTx}", intentionallyRed: true);
        test.Check("D19", "probe and transcode must agree on public identity documentation",
            openIdProbe.Signals.LikelyLoginRequired == openIdTx && !openIdTx,
            $"probe={openIdProbe.Signals.LikelyLoginRequired}; transcode={openIdTx}", intentionallyRed: true);
    }

    private static void FocusCharacterization(TestHarness test, string root)
    {
        var markdown = Read(root, "focus-sections.md");
        var numeric = LegacyTokenBudgetCharacterization.Apply(markdown, 128, "401");
        test.Check("D15", "numeric-only focus currently has no usable ranking term",
            numeric.Strategy != "focus_window" || !numeric.Text.Contains("ANSWER_401", StringComparison.Ordinal),
            $"strategy={numeric.Strategy}; answerPresent={numeric.Text.Contains("ANSWER_401", StringComparison.Ordinal)}");

        var wrong = LegacyTokenBudgetCharacterization.Apply(markdown, 128, "401 Unauthorized");
        test.Check("D15", "repeated heading terms can select the wrong section",
            wrong.Text.Contains("WRONG_SECTION", StringComparison.Ordinal)
            && !wrong.Text.Contains("ANSWER_401", StringComparison.Ordinal),
            DescribeSelection(wrong.Text, wrong.Strategy));

        var toc = LegacyTokenBudgetCharacterization.Apply(Read(root, "focus-toc.md"), 128, "client_max_body_size");
        test.Check("D11", "TOC occurrence can displace the answer-bearing body",
            toc.Text.Contains("TOC_ENTRY", StringComparison.Ordinal)
            && !toc.Text.Contains("ANSWER_NGINX", StringComparison.Ordinal),
            DescribeSelection(toc.Text, toc.Strategy));

        var fragment = ApplyLegacyFragmentBehavior(markdown, "https://standards.example/rfc#section-15.5.2", 128);
        test.Check("D17", "URL fragment is not currently routed into focus ranking",
            !fragment.Text.Contains("ANSWER_401", StringComparison.Ordinal),
            $"fragment=section-15.5.2; strategy={fragment.Strategy}; answerPresent=false");

        var duplicate = LegacyTokenBudgetCharacterization.Apply(Read(root, "focus-duplicates.md"), 128, "deployment status");
        test.Check("D15", "same-score candidate order is deterministic",
            duplicate.Text.Contains("FIRST_DUPLICATE", StringComparison.Ordinal),
            DescribeSelection(duplicate.Text, duplicate.Strategy));
    }

    private static void FocusDesiredContract(TestHarness test, string root)
    {
        var markdown = Read(root, "focus-sections.md");
        var numeric = TokenBudget.Apply(markdown, 128, "401");
        test.Check("D15", "numeric identifier must select its answer-bearing section",
            numeric.Text.Contains("ANSWER_401", StringComparison.Ordinal),
            DescribeSelection(numeric.Text, numeric.Strategy), intentionallyRed: true);

        var wrong = TokenBudget.Apply(markdown, 128, "401 Unauthorized");
        test.Check("D15", "heading coverage must beat distant repeated terms",
            wrong.Text.Contains("ANSWER_401", StringComparison.Ordinal)
            && !wrong.Text.Contains("WRONG_SECTION", StringComparison.Ordinal),
            DescribeSelection(wrong.Text, wrong.Strategy), intentionallyRed: true);

        var toc = TokenBudget.Apply(Read(root, "focus-toc.md"), 128, "client_max_body_size");
        test.Check("D11", "body definition must outrank a TOC entry",
            toc.Text.Contains("ANSWER_NGINX", StringComparison.Ordinal),
            DescribeSelection(toc.Text, toc.Strategy), intentionallyRed: true);

        var fragment = ApplyFragmentFocus(markdown, "https://standards.example/rfc#section-15.5.2", 128);
        test.Check("D17", "exact fragment must route to its deterministic anchor",
            fragment.Text.Contains("ANSWER_401", StringComparison.Ordinal),
            $"fragment=section-15.5.2; {DescribeSelection(fragment.Text, fragment.Strategy)}", intentionallyRed: true);
    }

    private static void BudgetCharacterization(TestHarness test, string root)
    {
        var rawSidecars = Sidecars();
        var caps = LegacyBudgetOwnershipCharacterization.PrepareSurfaceBudget(700, rawSidecars);
        test.Check("D10", "raw hidden sidecars currently reduce the surface budget",
            caps.SurfaceMaxTokens is int cap && cap < 652,
            $"requested=700; surface={caps.SurfaceMaxTokens}; structuredRaw={ResponseBudgetPlanner.EstimateStructuredRaw(rawSidecars)}; receipt=48");

        var emptyCaps = LegacyBudgetOwnershipCharacterization.PrepareSurfaceBudget(700, EmptySidecars());
        test.Check("D10", "unrequested sidecar inventory changes markdown allowance",
            caps.SurfaceMaxTokens < emptyCaps.SurfaceMaxTokens,
            $"withHidden={caps.SurfaceMaxTokens}; projectedOnly={emptyCaps.SurfaceMaxTokens}");

        var source = Read(root, "budget-answer.md");
        var constrained = LegacyTokenBudgetCharacterization.Apply(source, 128, "simple requests");
        var larger = LegacyTokenBudgetCharacterization.Apply(source, 512, "simple requests");
        test.Check("D10", "constrained budget loses answer-bearing list while larger budget retains it",
            !constrained.Text.Contains("ANSWER_BODY", StringComparison.Ordinal)
            && larger.Text.Contains("ANSWER_BODY", StringComparison.Ordinal),
            $"requested=128/512; estimated={TokenEstimator.Estimate(constrained.Text)}/{TokenEstimator.Estimate(larger.Text)}; strategy={constrained.Strategy}/{larger.Strategy}");

        var tocDense = LegacyTokenBudgetCharacterization.Apply(Read(root, "focus-toc.md"), 128, "client_max_body_size");
        test.Check("C10b", "focus plus constrained budget is not complete",
            tocDense.Truncated && !tocDense.Text.Contains("ANSWER_NGINX", StringComparison.Ordinal),
            $"requested=128; serializedEstimate={TokenEstimator.Estimate(tocDense.Text)}; answerPresent=false; strategy={tocDense.Strategy}");
    }

    private static void BudgetDesiredContract(TestHarness test, string root)
    {
        var rawSidecars = Sidecars();
        var caps = BudgetOwnership.PrepareSurfaceBudget(700, rawSidecars);
        var projectedOnly = BudgetOwnership.PrepareSurfaceBudget(700, EmptySidecars());
        test.Check("D10", "hidden non-serialized fields must consume zero public allocation",
            caps.SurfaceMaxTokens == projectedOnly.SurfaceMaxTokens,
            $"withHidden={caps.SurfaceMaxTokens}; projectedOnly={projectedOnly.SurfaceMaxTokens}", intentionallyRed: true);

        var constrained = TokenBudget.Apply(Read(root, "budget-answer.md"), 128, "simple requests");
        test.Check("D10", "planner must preserve a minimum answer-bearing unit when it fits",
            constrained.Text.Contains("ANSWER_BODY", StringComparison.Ordinal),
            $"requested=128; serializedEstimate={TokenEstimator.Estimate(constrained.Text)}; strategy={constrained.Strategy}", intentionallyRed: true);

        var tocDense = TokenBudget.Apply(Read(root, "focus-toc.md"), 128, "client_max_body_size");
        test.Check("C10b", "constrained focus must not masquerade as complete",
            tocDense.Text.Contains("ANSWER_NGINX", StringComparison.Ordinal),
            $"requested=128; answerPresent={tocDense.Text.Contains("ANSWER_NGINX", StringComparison.Ordinal)}; truncated={tocDense.Truncated}", intentionallyRed: true);
    }

    private static void SemanticCharacterization(TestHarness test)
    {
        // Frozen honesty: Ok remains transport completion. Usability is an independent dimension.
        var transportSucceededButUnusable = TranscodeAttempt.Create(
            "node_readability_turndown",
            transportOk: true,
            latencyMs: 14,
            usable: false,
            failureCode: "thin_extract");
        test.Check("SEMANTIC", "recovery ok currently represents raw transport/extract completion",
            transportSucceededButUnusable.Ok,
            "transport=true; access=unknown; usability=false (external quality gate); focus=unknown; completeness=unknown");
        test.Check("SEMANTIC", "transport success alone does not imply usability",
            transportSucceededButUnusable.Ok
            && transportSucceededButUnusable.TransportOk
            && !transportSucceededButUnusable.Usable,
            "fields=Backend,Ok,TransportOk,Usable,LatencyMs,FailureCode,EscalationReason; Ok aliases TransportOk");
    }

    private static void SemanticDesiredContract(TestHarness test)
    {
        var fields = typeof(TranscodeAttempt).GetProperties().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        test.Check("SEMANTIC", "backend attempt must distinguish transport from usability",
            fields.Contains("TransportOk") && fields.Contains("Usable"),
            $"fields={string.Join(',', fields.Order())}", intentionallyRed: true);
    }

    private static void LifecycleCharacterization(TestHarness test)
    {
        // Frozen PR-A harness model: owner-label stop leaves an unrelated tree alive.
        var registry = new TestHostRegistry();
        registry.Add(new HostIdentitySpike("gateway", 100, 101, "stdio:gateway", "session-a"));
        registry.Add(new HostIdentitySpike("dashboard", 200, 201, "stdio:dashboard", "session-b"));
        registry.Stop("gateway");
        test.Check("D3", "test harness models two valid independent host trees",
            registry.Live.Count == 1 && registry.Live[0].Owner == "dashboard",
            "targetedStop=gateway; survivor=dashboard; globalKill=false");
        // After PR-G the production descriptor exists; keep the coexistence finding above frozen.
        test.Check("D3", "production lifecycle identity descriptor is available",
            typeof(TranscodeOutcome).Assembly.GetTypes().Any(type => type.Name == "HostIdentityDescriptor"),
            "automation=production; productionType=HostIdentityDescriptor; pr=PR-G");
    }

    private static void LifecycleDesiredContract(TestHarness test)
    {
        PrGLifecycleCases.Run(test);
    }

    private static PageClassification ProbeLegacy(string html, string url) =>
        HtmlProbeClassifier.ClassifyLegacy(new ProbeFetchResult
        {
            Ok = true,
            StatusCode = 200,
            RequestedUrl = url,
            FinalUrl = url,
            HtmlBytes = System.Text.Encoding.UTF8.GetByteCount(html),
            HtmlSample = html,
        });

    private static PageClassification Probe(string html, string url) =>
        HtmlProbeClassifier.Classify(new ProbeFetchResult
        {
            Ok = true,
            StatusCode = 200,
            RequestedUrl = url,
            FinalUrl = url,
            HtmlBytes = System.Text.Encoding.UTF8.GetByteCount(html),
            HtmlSample = html,
        });

    private static (string Text, bool Truncated, string? Strategy) ApplyLegacyFragmentBehavior(
        string markdown,
        string url,
        int maxTokens)
    {
        _ = new Uri(url).Fragment; // Recorded intent; current production planner receives no fragment input.
        return LegacyTokenBudgetCharacterization.Apply(markdown, maxTokens, focusQuery: null);
    }

    private static (string Text, bool Truncated, string? Strategy) ApplyFragmentFocus(
        string markdown,
        string url,
        int maxTokens)
    {
        var intent = FocusIntent.FromUrl(url);
        return TokenBudget.Apply(markdown, maxTokens, focusFragment: intent.Fragment);
    }

    private static ResponseBudgetSidecars Sidecars()
    {
        var blocks = Enumerable.Range(0, 18)
            .Select(i => new WorkerExtractBlockInfo
            {
                Type = "paragraph",
                Text = $"Internal block {i}: " + new string('x', 90),
                SourceSelector = $"main > p:nth-of-type({i + 1})",
            })
            .ToArray();
        var tables = new[]
        {
            new WorkerExtractTableInfo
            {
                Caption = "Internal table",
                Headers = ["key", "value"],
                Rows = Enumerable.Range(0, 10).Select(i => new[] { $"k{i}", new string('v', 50) }).ToArray(),
                SourceSelector = "main > table",
            },
        };
        return new ResponseBudgetSidecars(blocks, tables, null, null, null, null, ExpectReceipt: true);
    }

    private static ResponseBudgetSidecars EmptySidecars() =>
        new(null, null, null, null, null, null, ExpectReceipt: true);

    private static string DescribeSelection(string text, string? strategy)
    {
        var heading = text.Split('\n').FirstOrDefault(line => line.StartsWith('#')) ?? "(none)";
        var anchor = text.Contains("section-15.5.2", StringComparison.Ordinal) ? "section-15.5.2" : "(unavailable)";
        var source = text.Contains("TOC_ENTRY", StringComparison.Ordinal) ? "toc" : "body";
        return $"selected={heading}; anchor={anchor}; source={source}; scoreTrace=unavailable; strategy={strategy}";
    }

    private static string Read(string root, string name) => File.ReadAllText(Path.Combine(root, name));

    private sealed record HostIdentitySpike(string Owner, int ParentPid, int ChildPid, string Endpoint, string Session);

    private sealed class TestHostRegistry
    {
        private readonly List<HostIdentitySpike> _live = [];
        public IReadOnlyList<HostIdentitySpike> Live => _live;
        public void Add(HostIdentitySpike identity) => _live.Add(identity);
        public void Stop(string owner) => _live.RemoveAll(item => item.Owner == owner);
    }
}
