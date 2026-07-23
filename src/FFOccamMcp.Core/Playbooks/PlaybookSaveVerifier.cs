using OccamMcp.Core.Routing;

namespace OccamMcp.Core.Playbooks;

public sealed class PlaybookSaveVerifier(TranscodePipeline pipeline)
{
    public async ValueTask<PlaybookVerifyResult> VerifyAsync(string verifyUrl, string playbookJson, PlaybookDocument document)
    {
        if (!Uri.TryCreate(verifyUrl, UriKind.Absolute, out var uri))
        {
            return Fail("invalid_url", "verify_url is not absolute.");
        }

        var verifyHost = PlaybookDocument.NormalizeHost(uri.Host);
        if (!PlaybookDocument.HostMatches(verifyHost, document.Hosts))
        {
            return Fail("playbook_schema_invalid", "verify_url host does not match playbook hosts[].");
        }

        var policy = ResolveBackendPolicy(document.PreferredBackend);

        using (PlaybookVerifyScope.Push(playbookJson))
        {
            var outcome = await pipeline.TranscodeAsync(verifyUrl, policy, CancellationToken.None);
            if (!outcome.Ok)
            {
                var code = outcome.FailureCode ?? "transcode_failed";
                if (code is "thin_extract" or "content_selectors_miss")
                {
                    return Fail("playbook_verify_failed", outcome.Message ?? code);
                }

                return Fail("playbook_verify_failed", $"Dry-run transcode failed: {code}");
            }

            var markdown = outcome.Markdown ?? string.Empty;
            if (markdown.Length < 100)
            {
                return Fail("playbook_verify_failed", "Dry-run markdown below pilot minimum length.");
            }

            var quality = QualityGate.AssessExtraction(markdown);
            var metrics = new PlaybookVerifyMetrics(
                quality.Score,
                quality.NoiseLeakage,
                quality.PassesGate,
                markdown.Length);

            if (!quality.PassesGate)
            {
                var failureCode = quality.Score < QualityGate.MinScore
                    ? "playbook_verify_low_score"
                    : "playbook_verify_high_noise";
                return new PlaybookVerifyResult(false, metrics, failureCode,
                    $"Draft playbook verify failed: score={quality.Score}, noise={quality.NoiseLeakage:F2}");
            }

            return new PlaybookVerifyResult(true, metrics, null, null);
        }
    }

    private static OccamBackendPolicy ResolveBackendPolicy(string? preferredBackend)
    {
        if (string.IsNullOrWhiteSpace(preferredBackend))
        {
            return OccamBackendPolicy.HttpThenBrowser;
        }

        return OccamBackendPolicyParser.TryParse(preferredBackend, out var policy)
            ? policy
            : OccamBackendPolicy.HttpThenBrowser;
    }

    private static PlaybookVerifyResult Fail(string code, string message) =>
        new(false, null, code, message);
}

public sealed record PlaybookVerifyResult(
    bool Ok,
    PlaybookVerifyMetrics? Metrics,
    string? FailureCode,
    string? Message);
