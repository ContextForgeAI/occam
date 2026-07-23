using OccamMcp.Core.Access;
using OccamMcp.Core.Abstractions;
using OccamMcp.Core.Routing;

namespace OccamMcp.Core.PostProcessors;

public sealed class RequiresLoginPostProcessor : ITranscodePostProcessor
{
    public int Order => 150;

    public TranscodeOutcome Process(TranscodeOutcome input, TranscodeContext ctx)
    {
        if (!string.IsNullOrWhiteSpace(ctx.Options.SessionProfile))
        {
            return input;
        }

        if (!input.Ok || string.IsNullOrWhiteSpace(input.Markdown))
        {
            return input;
        }

        var evidence = AccessEvidenceAdapters.FromTranscode(
            input.Access,
            input.Markdown,
            ctx.Url,
            input.FinalUrl,
            input.StatusCode);
        var assessment = AccessClassifier.Classify(evidence);
        input = input with { AccessAssessment = assessment };
        if (assessment.RequiresLogin)
        {
            return LoginFailure(input);
        }

        return input;
    }

    private static TranscodeOutcome LoginFailure(TranscodeOutcome input) =>
        input with
        {
            Ok = false,
            FailureCode = "requires_login",
            Message = "Page likely requires login and no session_profile was provided.",
        };
}
