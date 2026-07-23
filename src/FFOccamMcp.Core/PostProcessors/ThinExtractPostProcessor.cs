using OccamMcp.Core.Abstractions;
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
