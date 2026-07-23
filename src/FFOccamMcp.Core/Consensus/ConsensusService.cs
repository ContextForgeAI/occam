using OccamMcp.Core.Receipts;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Tools;

namespace OccamMcp.Core.Consensus;

public interface IConsensusService
{
    ValueTask<(OccamCrosscheckSuccessResponse? Success, string? FailureCode, string? Message)> CrosscheckAsync(
        string url,
        IReadOnlyList<OccamBackendPolicy> backends,
        string? sessionProfile,
        string? focusQuery,
        CancellationToken cancellationToken);
}

/// <summary>
/// SI-14 (local foundation): extract one URL through several vantage points and report whether the
/// witnesses agree. Each vantage produces a signed receipt (so the verdict is re-derivable by anyone
/// from the receipts — no separate consensus signature needed). Backends give the primary cloaking
/// axis (bare http vs full browser); a supplied session_profile adds an anon-vs-authed axis.
/// </summary>
public sealed class ConsensusService(TranscodePipeline pipeline, ReceiptSigner signer) : IConsensusService
{
    public async ValueTask<(OccamCrosscheckSuccessResponse? Success, string? FailureCode, string? Message)> CrosscheckAsync(
        string url,
        IReadOnlyList<OccamBackendPolicy> backends,
        string? sessionProfile,
        string? focusQuery,
        CancellationToken cancellationToken)
    {
        var effectiveSigner = EffectiveSigner();
        var observations = new List<VantageObservation>();
        var vantageInfos = new List<OccamCrosscheckVantageInfo>();

        foreach (var backend in backends)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var backendLabel = BackendLabel(backend);
            await AddVantageAsync(observations, vantageInfos, backendLabel, url, backend, null, focusQuery, effectiveSigner, cancellationToken);

            // Session axis: an authed witness per backend, compared against the anonymous one.
            if (!string.IsNullOrWhiteSpace(sessionProfile))
            {
                await AddVantageAsync(observations, vantageInfos, backendLabel + "+session", url, backend, sessionProfile, focusQuery, effectiveSigner, cancellationToken);
            }
        }

        var verdict = ConsensusEvaluator.Evaluate(observations);
        var response = new OccamCrosscheckSuccessResponse(
            Ok: true,
            Url: url,
            Verdict: verdict.Verdict,
            Vantages: [.. vantageInfos],
            Divergence: [.. verdict.Pairs],
            Timestamp: DateTimeOffset.UtcNow.ToString("O"));

        return (response, null, null);
    }

    private async Task AddVantageAsync(
        List<VantageObservation> observations,
        List<OccamCrosscheckVantageInfo> vantageInfos,
        string label,
        string url,
        OccamBackendPolicy backend,
        string? sessionProfile,
        string? focusQuery,
        ReceiptSigner? effectiveSigner,
        CancellationToken cancellationToken)
    {
        var options = new OccamTranscodeOptions
        {
            JsonBlocks = true, // both backends must emit blocks so we can compare Merkle roots
            FitMarkdown = !string.IsNullOrWhiteSpace(focusQuery),
            FocusQuery = focusQuery,
            SessionProfile = sessionProfile,
            PlaybookPolicy = Playbooks.PlaybookPolicy.Off, // compare raw content, don't let a genome mask divergence
        };

        var outcome = await pipeline.TranscodeAsync(url, backend, options, cancellationToken);
        var backendName = outcome.Backend ?? BackendName(backend);

        if (outcome.Ok && !string.IsNullOrEmpty(outcome.Markdown))
        {
            (string Text, string? SourceSelector)[] blocks = outcome.Blocks is null
                ? []
                : [.. outcome.Blocks.Select(b => (b.Text, (string?)b.SourceSelector))];
            var contentHash = ReceiptCanonicalizer.ContentHash(outcome.Markdown);
            var root = MerkleTree.Root(blocks);
            var leaves = blocks.Length > 0 ? MerkleTree.LeafHashesHex(blocks) : null;
            var receipt = OccamTranscodeResponseBuilder.BuildReceipt(outcome, url, effectiveSigner)?.Signed;

            observations.Add(new VantageObservation(label, backendName, true, null, false, contentHash, root, leaves));
            vantageInfos.Add(new OccamCrosscheckVantageInfo(label, backendName, true, null, contentHash, root, receipt));
            return;
        }

        var code = outcome.FailureCode ?? "extraction_failed";
        var isWall = code is "captcha_or_challenge" or "requires_login" or "paywall"
            || outcome.StatusCode is 401 or 403 or 404 or 410;
        var negative = OccamTranscodeResponseBuilder.BuildNegativeReceipt(
            url, outcome.FinalUrl, backendName, code, outcome.StatusCode, effectiveSigner);

        observations.Add(new VantageObservation(label, backendName, false, code, isWall, null, null, null));
        vantageInfos.Add(new OccamCrosscheckVantageInfo(label, backendName, false, code, null, null, negative));
    }

    private static string BackendLabel(OccamBackendPolicy backend) => BackendName(backend);

    private static string BackendName(OccamBackendPolicy backend) =>
        backend == OccamBackendPolicy.Browser ? "browser" : "http";

    /// <summary>Receipt v1 signing is on by default; OCCAM_RECEIPTS=off|0|false → unsigned vantage receipts (verdict still computed).</summary>
    private ReceiptSigner? EffectiveSigner()
    {
        var v = Environment.GetEnvironmentVariable("OCCAM_RECEIPTS");
        var enabled = v is null
            || !(v.Equals("off", StringComparison.OrdinalIgnoreCase) || v == "0"
                 || v.Equals("false", StringComparison.OrdinalIgnoreCase));
        return enabled ? signer : null;
    }
}
