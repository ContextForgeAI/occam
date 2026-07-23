using OccamMcp.Core.Abstractions;
using OccamMcp.Core.PostProcessors;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Routing;

public enum OccamBackendPolicy
{
    Http,
    Browser,
    HttpThenBrowser,
}

public static class OccamBackendPolicyParser
{
    public static bool TryParse(string? value, out OccamBackendPolicy policy)
    {
        policy = OccamBackendPolicy.HttpThenBrowser;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "http":
                policy = OccamBackendPolicy.Http;
                return true;
            case "browser":
                policy = OccamBackendPolicy.Browser;
                return true;
            case "http_then_browser":
            case "http-then-browser":
                policy = OccamBackendPolicy.HttpThenBrowser;
                return true;
            default:
                return false;
        }
    }
}

public sealed class OccamRouter
{
    private readonly IExtractBackend? _http;
    private readonly IExtractBackend? _browser;
    private readonly Backends.IManagedExtractBackend? _managed;

    public OccamRouter(IEnumerable<IExtractBackend> backends, Backends.IManagedExtractBackend? managed = null)
    {
        foreach (var backend in backends)
        {
            switch (backend.Name)
            {
                case "http":
                    _http = backend;
                    break;
                case "browser":
                    _browser = backend;
                    break;
            }
        }

        _managed = managed;
    }

    public async ValueTask<TranscodeOutcome> TranscodeAsync(string url, OccamBackendPolicy policy, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!AreBackendsReady(policy))
        {
            return new TranscodeOutcome(
                false,
                null,
                null,
                null,
                "workers_unavailable",
                "Occam workers are not ready.");
        }

        return policy switch
        {
            OccamBackendPolicy.Http =>
                TranscodeOutcomeMapper.FromExtractRun(await _http!.ExtractAsync(url, cancellationToken).ConfigureAwait(false)),
            OccamBackendPolicy.Browser =>
                TranscodeOutcomeMapper.FromExtractRun(await _browser!.ExtractAsync(url, cancellationToken).ConfigureAwait(false)),
            OccamBackendPolicy.HttpThenBrowser =>
                await TranscodeHttpThenBrowserAsync(url, cancellationToken).ConfigureAwait(false),
            _ => new TranscodeOutcome(false, null, null, null, "invalid_policy", "Unknown backend policy."),
        };
    }

    // Require only the backend(s) the policy actually uses — an http-only request must not be blocked by a
    // browser backend that isn't ready. (Harmless today since both IsReady == workerPaths.IsConfigured, but
    // correct once BrowserExtractBackend.IsReady becomes honest, e.g. checks for a chromium install.)
    private bool AreBackendsReady(OccamBackendPolicy policy) => policy switch
    {
        OccamBackendPolicy.Http => _http is { IsReady: true },
        OccamBackendPolicy.Browser => _browser is { IsReady: true },
        _ => _http is { IsReady: true } && _browser is { IsReady: true },
    };

    private async ValueTask<TranscodeOutcome> TranscodeHttpThenBrowserAsync(string url, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Records each attempt's outcome in the recovery log and captures a one-time browser
        // auto-provision from whichever attempt performed it, so browserProvisioned survives even when
        // the returned result isn't the attempt that installed the browser.
        var attempts = new List<TranscodeAttempt>();
        WorkerBrowserProvisionedInfo? provisioned = null;
        ExtractRunResult Record(ExtractRunResult r, string? escalationReason = null)
        {
            provisioned ??= r.BrowserProvisioned;
            var usable = IsSuccessfulExtract(r);
            attempts.Add(new TranscodeAttempt(
                Backend: r.Backend ?? "http",
                Ok: r.Ok,
                LatencyMs: r.LatencyMs,
                TransportOk: r.Ok,
                Usable: usable,
                FailureCode: usable ? null : ResolveAttemptFailure(r),
                EscalationReason: escalationReason));
            return r;
        }

        TranscodeOutcome Finish(ExtractRunResult r) =>
            TranscodeOutcomeMapper.FromExtractRun(r) with
            {
                Recovery = attempts.ToArray(),
                BrowserProvisioned = r.BrowserProvisioned ?? provisioned,
            };

        var http = Record(await _http!.ExtractAsync(url, cancellationToken).ConfigureAwait(false));
        if (IsSuccessfulExtract(http))
        {
            return Finish(http);
        }

        // A definitive terminal HTTP status (404/410) means the resource does not exist — a browser
        // render cannot resurrect it, and on some 404 pages the browser worker throws, masking the
        // authoritative http_404 as a generic extraction_failed (TypeError). Short-circuit so the agent
        // gets the real status + the correct "stop / fix the URL" guidance and no pointless heal hint.
        if (IsTerminalHttpFailure(http))
        {
            return Finish(http);
        }

        if (DomainTierRegistry.IsPublicReferencePage(url))
        {
            return Finish(http);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var browser = Record(
            await _browser!.ExtractAsync(url, cancellationToken).ConfigureAwait(false),
            EscalationReasonFor(http));
        if (IsSuccessfulExtract(browser))
        {
            return Finish(browser);
        }

        // Package 3: last-resort escalation to a managed provider (Firecrawl/Jina/…) when both
        // local backends fail on an opted-in host. Off by default; never reached unless configured.
        if (_managed is not null && _managed.ShouldAttempt(url))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var managed = Record(
                await _managed.ExtractAsync(url, cancellationToken).ConfigureAwait(false),
                EscalationReasonFor(browser));
            if (IsSuccessfulExtract(managed))
            {
                return Finish(managed);
            }
        }

        // Neither local attempt produced usable content. Choose by informativeness so a specific code
        // (http_403, captcha_or_challenge, …) isn't masked by the other attempt's generic
        // extraction_failed — and so a raw ok-but-thin http result never outranks the browser attempt
        // (whose backend=browser must surface so the tool's browser-exhausted-thin STOP fires instead of
        // handing the agent a retry-browser hint for a browser it already tried).
        return Finish(ChooseRawFallback(http, browser));
    }

    // Above this much extracted markdown the page is real content, so skip the keyword challenge check — a
    // >2 KB article that merely mentions Cloudflare/captcha/"just a moment" is not an interstitial. Mirrors
    // ChallengePagePostProcessor's guard so the router and the post-processor no longer disagree (A5).
    private const int ChallengeContentThreshold = 2000;

    // A raw extract is a genuine success only when it is ok, has a body, isn't a thin stub, and isn't a
    // challenge interstitial. The thin check unifies the flagship occam_transcode cascade with the router's
    // (B1): occam_transcode used to split http_then_browser into http-then-browser in the tool and apply its
    // own post-compile thin trigger — now every caller escalates raw-thin http to the browser here, once.
    private static bool IsSuccessfulExtract(ExtractRunResult result) =>
        result.Ok
        && !string.IsNullOrWhiteSpace(result.Markdown)
        && !ExtractQualityEvaluator.LooksLikeThinExtract(result.Markdown)
        && (result.Markdown!.Length > ChallengeContentThreshold
            || !ChallengePageDetector.LooksLikeChallengePage(result.Markdown));

    // Neither attempt succeeded. Rank each raw result: a hard failure by its resolved code's
    // informativeness; a raw ok-but-thin result as a LOW rank (== thin_extract) so it can't mask a specific
    // http failure yet still beats a browser timeout. Tie → browser, so backend=browser surfaces.
    // Cases: http-thin(60) vs browser-thin(60) → browser; http-403(100) vs browser-thin(60) → http;
    // http-thin(60) vs browser-captcha(90) → browser; http-thin(60) vs browser-timeout(50) → http.
    private static ExtractRunResult ChooseRawFallback(ExtractRunResult http, ExtractRunResult browser) =>
        RawRank(browser) >= RawRank(http) ? browser : http;

    private static int RawRank(ExtractRunResult result) =>
        result.Ok && !string.IsNullOrWhiteSpace(result.Markdown)
            ? FailureRanking.Informativeness("thin_extract")
            : FailureRanking.Informativeness(
                FailureCodeStrings.ResolveTranscodeFailure(result.Failure, result.StatusCode));

    // http_then_browser should not escalate a definitive "resource gone" status to the browser.
    private static bool IsTerminalHttpFailure(ExtractRunResult result) =>
        result.StatusCode is 404 or 410
        || FailureCodeStrings.ResolveTranscodeFailure(result.Failure, result.StatusCode) is "http_404" or "http_410";

    private static string ResolveAttemptFailure(ExtractRunResult result)
    {
        if (result.Ok && !string.IsNullOrWhiteSpace(result.Markdown))
        {
            if (ExtractQualityEvaluator.LooksLikeThinExtract(result.Markdown))
            {
                return "thin_extract";
            }

            if (result.Markdown!.Length <= ChallengeContentThreshold
                && ChallengePageDetector.LooksLikeChallengePage(result.Markdown))
            {
                return "captcha_or_challenge";
            }

            return "unusable_extract";
        }

        return FailureCodeStrings.ResolveTranscodeFailure(result.Failure, result.StatusCode);
    }

    private static string EscalationReasonFor(ExtractRunResult prior) =>
        ResolveAttemptFailure(prior);
}
