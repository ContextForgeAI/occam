using OccamMcp.Core.Abstractions;
using OccamMcp.Core.Routing;

namespace OccamMcp.Core.PostProcessors;

public sealed class ChallengePagePostProcessor : ITranscodePostProcessor
{
    public int Order => 100;

    // A genuine anti-bot / challenge interstitial carries almost no content. Above this much
    // extracted markdown we treat the page as real content and skip keyword challenge detection,
    // so an article that merely *mentions* Cloudflare / captcha / "429 Too Many Requests" / "rate
    // limit" (e.g. a page about HTTP status codes or web security) is not false-flagged. (Q-026)
    private const int ChallengeMaxMarkdownChars = 2_000;

    public TranscodeOutcome Process(TranscodeOutcome input, TranscodeContext ctx)
    {
        if (!input.Ok || string.IsNullOrWhiteSpace(input.Markdown))
        {
            return input;
        }

        if (input.Markdown!.Length > ChallengeMaxMarkdownChars)
        {
            return input;
        }

        if (!ChallengePageDetector.LooksLikeChallengePage(input.Markdown))
        {
            return input;
        }

        return input with
        {
            Ok = false,
            FailureCode = "captcha_or_challenge",
            Message = "Occam extract hit an anti-bot or Cloudflare challenge page.",
        };
    }
}
