using OccamMcp.Core.Routing;

namespace OccamMcp.Core.Caching;

/// <summary>
/// Decides whether a transcode request may use the opt-in response cache. The cache returns real
/// prior extracts, so it is trust-model compatible — but it must never persist private content or
/// anything tied to a session profile. When this returns false the tool behaves exactly as it does
/// with no cache configured (no read, no write, no error).
/// </summary>
public static class TranscodeCacheEligibility
{
    public static bool IsCacheable(string url, string? sessionProfile, string? ifNoneMatch, int? cacheTtlS)
    {
        // Off by default: omitted or non-positive TTL means the caller did not opt in.
        if (cacheTtlS is not > 0)
        {
            return false;
        }

        // Never cache anything tied to a session profile — it may carry authenticated content.
        if (!string.IsNullOrWhiteSpace(sessionProfile))
        {
            return false;
        }

        // AF-6 differential reads and the cache are disjoint features; don't combine them.
        if (!string.IsNullOrWhiteSpace(ifNoneMatch))
        {
            return false;
        }

        // Never cache private/RFC1918/localhost targets — reuse the canonical privacy classifier.
        var privacy = PrivacyClassifier.Classify(url);
        if (privacy.IsPrivateHost || privacy.BlockReason == ProbeFailureKind.InvalidArguments)
        {
            return false;
        }

        return true;
    }
}
