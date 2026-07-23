using OccamMcp.Core.Configuration;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Services;

/// <summary>Caps concurrent per-URL transcodes inside occam_digest.</summary>
internal static class DigestParallelism
{
    public static int ResolveMaxParallel(OccamBackendPolicy policy, int urlCount)
    {
        var boundedCount = Math.Clamp(urlCount, 1, DigestService.MaxUrlsCap);
        if (boundedCount <= 1)
        {
            return 1;
        }

        if (string.Equals(Environment.GetEnvironmentVariable("OCCAM_DIGEST_PARALLEL"), "0", StringComparison.Ordinal))
        {
            return 1;
        }

        if (int.TryParse(Environment.GetEnvironmentVariable("OCCAM_DIGEST_MAX_PARALLEL"), out var configured))
        {
            return Math.Clamp(configured, 1, DigestService.MaxUrlsCap);
        }

        var browserMaxParallel = BrowserConcurrencyLimiter.ResolveMaxParallel();
        return policy switch
        {
            OccamBackendPolicy.Http => Math.Min(4, boundedCount),
            OccamBackendPolicy.Browser => Math.Min(browserMaxParallel, boundedCount),
            _ => Math.Min(browserMaxParallel, boundedCount),
        };
    }
}
