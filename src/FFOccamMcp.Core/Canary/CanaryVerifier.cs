namespace OccamMcp.Core.Canary;

/// <summary>
/// The verdict state machine: turns a sentinel an agent claims to have read into one of the four
/// <see cref="CanaryVerdict"/> states.
/// </summary>
/// <remarks>
/// <para>Precedence is fixed and total (PROBE_PROTOCOL.md §5):</para>
/// <list type="number">
/// <item><description>unparseable or unmatched claim → <see cref="CanaryVerdict.Hallucinated"/>;</description></item>
/// <item><description>authentic but never issued by this host → <see cref="CanaryVerdict.ReplaySuspect"/>;</description></item>
/// <item><description>authentic, issued, inside the fresh window → <see cref="CanaryVerdict.ReadVerified"/>;</description></item>
/// <item><description>authentic, issued, older than the fresh window → <see cref="CanaryVerdict.ReadStale"/>.</description></item>
/// </list>
/// <para>
/// Authenticity is checked before freshness, and provenance before both, so a valid-looking value
/// can never be upgraded to <see cref="CanaryVerdict.ReadVerified"/> just because it is recent.
/// </para>
/// </remarks>
public sealed class CanaryVerifier
{
    private readonly CanarySecret _secret;
    private readonly CanaryIssueLog _issueLog;
    private readonly CanaryOptions _options;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates a verifier over the process secret and its issuance log.</summary>
    /// <param name="secret">Process-lifetime key material.</param>
    /// <param name="issueLog">Issuance records used to tell a real read from a replay.</param>
    /// <param name="options">Active canary options.</param>
    /// <param name="timeProvider">Clock source; injectable so bucket transitions are testable.</param>
    public CanaryVerifier(
        CanarySecret secret,
        CanaryIssueLog issueLog,
        CanaryOptions options,
        TimeProvider? timeProvider = null)
    {
        _secret = secret ?? throw new ArgumentNullException(nameof(secret));
        _issueLog = issueLog ?? throw new ArgumentNullException(nameof(issueLog));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Verifies a claim against every bucket inside the recognised window.
    /// </summary>
    /// <param name="sessionId">Session the claim belongs to.</param>
    /// <param name="claimedSentinel">Value the agent reported; may be quoted or padded with whitespace.</param>
    /// <returns>The verdict plus which bucket matched, if any.</returns>
    public CanaryVerification Verify(string? sessionId, string? claimedSentinel)
    {
        var now = _timeProvider.GetUtcNow();
        var currentBucket = CanarySentinel.BucketFor(now, _options.BucketSeconds);

        if (!CanarySentinel.IsValidSessionId(sessionId, _options.MaxSessionIdLength))
        {
            return new CanaryVerification(
                CanaryVerdict.Hallucinated,
                sessionId ?? string.Empty,
                currentBucket,
                MatchedBucket: null,
                BucketDistance: null,
                Detail: "Session id is missing or not an accepted token; no sentinel could have been issued for it.");
        }

        var candidate = CanarySentinel.Normalize(claimedSentinel);
        if (candidate is null)
        {
            return new CanaryVerification(
                CanaryVerdict.Hallucinated,
                sessionId!,
                currentBucket,
                MatchedBucket: null,
                BucketDistance: null,
                Detail: "No sentinel was reported, or the reported value cannot be a sentinel.");
        }

        var matched = FindMatchingBucket(sessionId!, candidate, currentBucket);
        if (matched is null)
        {
            return new CanaryVerification(
                CanaryVerdict.Hallucinated,
                sessionId!,
                currentBucket,
                MatchedBucket: null,
                BucketDistance: null,
                Detail: $"Reported sentinel matches no bucket within the recognised window " +
                        $"({_options.StaleBucketHorizon} buckets back).");
        }

        var matchedBucket = matched.Value;
        var distance = (int)(currentBucket - matchedBucket);

        if (!_issueLog.WasIssued(sessionId!, matchedBucket))
        {
            return new CanaryVerification(
                CanaryVerdict.ReplaySuspect,
                sessionId!,
                currentBucket,
                matchedBucket,
                distance,
                Detail: "Sentinel is authentic but this host has no record of issuing it; " +
                        "the value reached the agent by an unlogged path.");
        }

        if (Math.Abs(distance) <= _options.FreshBucketTolerance)
        {
            return new CanaryVerification(
                CanaryVerdict.ReadVerified,
                sessionId!,
                currentBucket,
                matchedBucket,
                distance,
                Detail: "Sentinel is authentic, was issued by this host, and is inside the fresh window.");
        }

        var bucketWord = Math.Abs(distance) == 1 ? "bucket" : "buckets";
        return new CanaryVerification(
            CanaryVerdict.ReadStale,
            sessionId!,
            currentBucket,
            matchedBucket,
            distance,
            Detail: $"Sentinel is authentic and was issued by this host, but {distance} {bucketWord} ago; " +
                    "the read is real yet no longer current.");
    }

    /// <summary>
    /// Scans the recognised window newest-first and returns the matching bucket.
    /// </summary>
    /// <remarks>
    /// The scan starts <see cref="CanaryOptions.FreshBucketTolerance"/> buckets in the future to
    /// absorb a client clock running ahead, then walks back to the stale horizon. Work is bounded by
    /// configuration, not by input, so a claim cannot make verification expensive.
    /// </remarks>
    private long? FindMatchingBucket(string sessionId, string candidate, long currentBucket)
    {
        var newest = currentBucket + _options.FreshBucketTolerance;
        var oldest = currentBucket - _options.StaleBucketHorizon;

        for (var bucket = newest; bucket >= oldest; bucket--)
        {
            var expected = _secret.DeriveSentinel(bucket, sessionId, _options.SentinelBytes);
            if (CanarySentinel.FixedTimeEquals(expected, candidate))
            {
                return bucket;
            }
        }

        return null;
    }
}
