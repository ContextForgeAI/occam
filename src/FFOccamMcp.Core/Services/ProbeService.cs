using OccamMcp.Core.Probe;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Session;

namespace OccamMcp.Core.Services;

public sealed class ProbeService(HttpProbeFetcher fetcher)
{
    public async Task<ProbeAnalysis> AnalyzeAsync(
        string url,
        int timeoutMs = 10_000,
        bool includeSocialMeta = false,
        string? sessionProfile = null,
        CancellationToken cancellationToken = default)
    {
        var preflight = FetchPreflight.Prepare(url, sessionProfile);
        if (!preflight.Ok)
        {
            var privacy = PrivacyClassifier.Classify(url);
            return new ProbeAnalysis
            {
                Ok = false,
                Url = url,
                Privacy = privacy,
                FailureCode = preflight.FailureCode,
                LatencyMs = 0,
            };
        }

        using (preflight.HeadersScope)
        {
            return await AnalyzeCoreAsync(
                url,
                timeoutMs,
                includeSocialMeta,
                preflight.MergedHeaders,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<ProbeAnalysis> AnalyzeCoreAsync(
        string url,
        int timeoutMs,
        bool includeSocialMeta,
        IReadOnlyDictionary<string, string>? requestHeaders,
        CancellationToken cancellationToken)
    {
        var privacy = PrivacyClassifier.Classify(url);
        var fetch = await fetcher.FetchAsync(
            url,
            timeoutMs,
            requestHeaders: requestHeaders,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (fetch.IsPdf || ContentFormatDetector.IsPdfUrl(url))
        {
            return BuildPdfAnalysis(url, fetch, privacy);
        }

        if (fetch.FailureCode is not null && fetch.HtmlSample is null)
        {
            return new ProbeAnalysis
            {
                Ok = false,
                Url = url,
                FinalUrl = fetch.FinalUrl,
                RedirectChain = MapRedirectChain(fetch.RedirectChain),
                Privacy = privacy,
                FailureCode = FailureCodeStrings.Normalize(fetch.FailureCode),
                LatencyMs = fetch.LatencyMs,
                StatusCode = fetch.StatusCode,
            };
        }

        var httpFailure = FailureCodeStrings.FromHttpStatus(fetch.StatusCode);
        var classification = fetch.HtmlSample is not null ? HtmlProbeClassifier.Classify(fetch) : null;
        var tier = DomainTierRegistry.TryResolve(url);
        if (classification is not null && tier is not null)
        {
            var tierSignals = DomainTierRegistry.ApplyTierHints(url, classification.Signals);
            var riskFlags = classification.RiskFlags;
            if (!tierSignals.LikelyLoginRequired)
            {
                riskFlags = riskFlags.Where(f => f != "login_required").ToArray();
            }

            classification = new PageClassification
            {
                PageClass = tierSignals.PageClass,
                Signals = tierSignals,
                RiskFlags = riskFlags,
                VisibleTextRatio = classification.VisibleTextRatio,
                ScriptDensity = classification.ScriptDensity,
                Challenge = classification.Challenge,
            };
        }

        if (httpFailure is not null)
        {
            return new ProbeAnalysis
            {
                Ok = false,
                Url = url,
                FinalUrl = fetch.FinalUrl ?? url,
                RedirectChain = MapRedirectChain(fetch.RedirectChain),
                Privacy = privacy,
                DomainTier = tier?.TierId,
                Classification = classification,
                FailureCode = httpFailure,
                LatencyMs = fetch.LatencyMs,
                StatusCode = fetch.StatusCode,
                ContentType = fetch.ContentType,
                Challenge = classification?.Challenge,
            };
        }

        if (classification is null)
        {
            return new ProbeAnalysis
            {
                Ok = false,
                Url = url,
                FinalUrl = fetch.FinalUrl,
                RedirectChain = MapRedirectChain(fetch.RedirectChain),
                Privacy = privacy,
                FailureCode = FailureCodeStrings.Normalize(fetch.FailureCode) ?? "network_error",
                LatencyMs = fetch.LatencyMs,
                StatusCode = fetch.StatusCode,
            };
        }

        var recommendation = Recommend(classification.Signals, tier);

        SocialMeta? social = null;
        if (includeSocialMeta && fetch.HtmlSample is not null)
        {
            social = HtmlSocialMetaExtractor.Extract(fetch.HtmlSample, fetch.FinalUrl ?? url);
        }

        return new ProbeAnalysis
        {
            Ok = true,
            Url = url,
            FinalUrl = fetch.FinalUrl ?? url,
            RedirectChain = MapRedirectChain(fetch.RedirectChain),
            Privacy = privacy,
            DomainTier = tier?.TierId,
            Classification = classification,
            RecommendedBackend = recommendation.Backend,
            EstimatedLatencyMs = recommendation.EstimatedLatencyMs,
            LatencyMs = fetch.LatencyMs,
            StatusCode = fetch.StatusCode,
            ContentType = fetch.ContentType,
            SocialMeta = social,
            Challenge = classification.Challenge,
        };
    }

    private static string[]? MapRedirectChain(IReadOnlyList<string>? chain) =>
        chain is { Count: > 0 } ? chain.ToArray() : null;

    private static (string Backend, int EstimatedLatencyMs) Recommend(ProbeSignals signals, DomainTierMatch? tier)
    {
        if (signals.LikelyLoginRequired)
        {
            return ("none", 0);
        }

        if (tier?.TierId == "anti_bot_blogs" && signals.LikelyChallenge)
        {
            return ("none", 0);
        }

        if (tier?.HttpOnly == true && !signals.LikelyChallenge)
        {
            return ("http", 800);
        }

        if (signals.LikelyCookieConsent
            || signals.SpaShell
            || signals.RequiresJavascript
            || signals.VisibleTextRatio < 0.03)
        {
            return ("http_then_browser", 5000);
        }

        return ("http", 1200);
    }

    private static ProbeAnalysis BuildPdfAnalysis(string url, ProbeFetchResult fetch, PrivacyClassification privacy)
    {
        if (!fetch.Ok && fetch.FailureCode is not null)
        {
            return new ProbeAnalysis
            {
                Ok = false,
                Url = url,
                FinalUrl = fetch.FinalUrl,
                RedirectChain = MapRedirectChain(fetch.RedirectChain),
                Privacy = privacy,
                FailureCode = fetch.FailureCode,
                LatencyMs = fetch.LatencyMs,
            };
        }

        var signals = new ProbeSignals
        {
            PageClass = "pdf",
            RequiresJavascript = false,
            VisibleTextRatio = 1.0,
        };
        var classification = new PageClassification
        {
            PageClass = "pdf",
            Signals = signals,
            RiskFlags = [],
            VisibleTextRatio = 1.0,
            ScriptDensity = 0,
        };

        return new ProbeAnalysis
        {
            Ok = true,
            Url = url,
            FinalUrl = fetch.FinalUrl ?? url,
            RedirectChain = MapRedirectChain(fetch.RedirectChain),
            Privacy = privacy,
            Classification = classification,
            RecommendedBackend = "http",
            EstimatedLatencyMs = 0,
            LatencyMs = fetch.LatencyMs,
            StatusCode = fetch.StatusCode,
            ContentType = fetch.ContentType ?? "application/pdf",
        };
    }
}

public sealed class ProbeAnalysis
{
    public required bool Ok { get; init; }
    public required string Url { get; init; }
    public string? FinalUrl { get; init; }
    public required PrivacyClassification Privacy { get; init; }
    public PageClassification? Classification { get; init; }
    public string? RecommendedBackend { get; init; }
    public int EstimatedLatencyMs { get; init; }
    public int LatencyMs { get; init; }
    public int StatusCode { get; init; }
    public string? ContentType { get; init; }
    public string? FailureCode { get; init; }
    public string? DomainTier { get; init; }
    public SocialMeta? SocialMeta { get; init; }
    public string[]? RedirectChain { get; init; }
    public ChallengeHint? Challenge { get; init; }
}
