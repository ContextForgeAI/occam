namespace OccamMcp.Core.Routing;

/// <summary>Post-extraction detection of anti-bot / Cloudflare interstitial markdown.</summary>
public static class ChallengePageDetector
{
    public static bool LooksLikeChallengePage(string? markdown) =>
        ChallengeKindClassifier.Detect(markdown);
}
