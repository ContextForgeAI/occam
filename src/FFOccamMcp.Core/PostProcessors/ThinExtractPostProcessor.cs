using OccamMcp.Core.Abstractions;
using OccamMcp.Core.Access;
using OccamMcp.Core.Routing;

namespace OccamMcp.Core.PostProcessors;

public sealed class ThinExtractPostProcessor : ITranscodePostProcessor
{
    public int Order => 200;

    public TranscodeOutcome Process(TranscodeOutcome input, TranscodeContext ctx)
    {
        if (!input.Ok || string.IsNullOrWhiteSpace(input.Markdown))
        {
            return input;
        }

        if (ExtractQualityEvaluator.LooksLikeErrorShell(input.Markdown))
        {
            var evidence = AccessEvidenceAdapters.FromTranscode(
                input.Access,
                input.Markdown,
                ctx.Url,
                input.FinalUrl,
                input.StatusCode);
            var assessment = AccessClassifier.Classify(evidence);
            return input with
            {
                Ok = false,
                FailureCode = "render_error",
                Message = FailureCodeStrings.FormatTranscodeMessage("render_error", input.StatusCode),
                Confidence = 0,
                Quality = null,
                AccessAssessment = assessment,
            };
        }

        if (!ExtractQualityEvaluator.LooksLikeThinExtract(input.Markdown))
        {
            return input;
        }

        return input with
        {
            Ok = false,
            FailureCode = "thin_extract",
            Message = "Occam extract returned suspiciously little content (possible promo banner or wrong region).",
        };
    }
}
