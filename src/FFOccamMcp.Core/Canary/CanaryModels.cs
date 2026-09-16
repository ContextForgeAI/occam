namespace OccamMcp.Core.Canary;

/// <summary>
/// Outcome of checking a sentinel an agent claims to have read. The four states are exhaustive and
/// mutually exclusive; precedence is fixed by <see cref="CanaryVerifier"/> and documented in
/// PROBE_PROTOCOL.md §5.
/// </summary>
public enum CanaryVerdict
{
    /// <summary>
    /// The claim does not match any sentinel inside the recognised window. Either the value was
    /// invented, or the page was never actually read. This is the default for unparseable input.
    /// </summary>
    Hallucinated = 0,

    /// <summary>
    /// The claim matches a sentinel issued for the current bucket (± the fresh tolerance) and an
    /// issuance record exists. The strongest verdict the protocol can produce.
    /// </summary>
    ReadVerified = 1,

    /// <summary>
    /// The claim matches a genuine sentinel, but from an older bucket inside the stale horizon.
    /// The read happened — just not now. Typical for cached context replayed into a later turn.
    /// </summary>
    ReadStale = 2,

    /// <summary>
    /// The claim is cryptographically valid, yet this host never recorded issuing it. The value
    /// therefore arrived by some path other than a logged fetch: an out-of-band relay, a shared
    /// transcript, or an issuance record that was evicted or never written.
    /// </summary>
    ReplaySuspect = 3,
}

/// <summary>Stable wire spellings for <see cref="CanaryVerdict"/>.</summary>
public static class CanaryVerdictStrings
{
    /// <summary>Wire value for <see cref="CanaryVerdict.ReadVerified"/>.</summary>
    public const string ReadVerified = "READ_VERIFIED";

    /// <summary>Wire value for <see cref="CanaryVerdict.ReadStale"/>.</summary>
    public const string ReadStale = "READ_STALE";

    /// <summary>Wire value for <see cref="CanaryVerdict.Hallucinated"/>.</summary>
    public const string Hallucinated = "HALLUCINATED";

    /// <summary>Wire value for <see cref="CanaryVerdict.ReplaySuspect"/>.</summary>
    public const string ReplaySuspect = "REPLAY_SUSPECT";

    /// <summary>Maps a verdict to its wire spelling.</summary>
    /// <param name="verdict">Verdict to convert.</param>
    /// <returns>The stable uppercase wire token.</returns>
    public static string ToWire(CanaryVerdict verdict) => verdict switch
    {
        CanaryVerdict.ReadVerified => ReadVerified,
        CanaryVerdict.ReadStale => ReadStale,
        CanaryVerdict.ReplaySuspect => ReplaySuspect,
        _ => Hallucinated,
    };

    /// <summary>Parses a wire verdict token. Unknown values return false.</summary>
    public static bool TryParse(string? value, out CanaryVerdict verdict)
    {
        verdict = CanaryVerdict.Hallucinated;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim().ToUpperInvariant())
        {
            case ReadVerified:
                verdict = CanaryVerdict.ReadVerified;
                return true;
            case ReadStale:
                verdict = CanaryVerdict.ReadStale;
                return true;
            case ReplaySuspect:
                verdict = CanaryVerdict.ReplaySuspect;
                return true;
            case Hallucinated:
                verdict = CanaryVerdict.Hallucinated;
                return true;
            default:
                return false;
        }
    }
}

/// <summary>
/// A sentinel handed out for one (session, bucket) pair, together with the page that embeds it.
/// </summary>
/// <param name="SessionId">Session the sentinel is bound to.</param>
/// <param name="Bucket">Time bucket the sentinel was derived for.</param>
/// <param name="Sentinel">Encoded sentinel value embedded in the page.</param>
/// <param name="IssuedAt">Instant the sentinel was produced, UTC.</param>
/// <param name="ExpiresAt">Instant after which the sentinel stops being <c>READ_VERIFIED</c>.</param>
public readonly record struct CanaryIssue(
    string SessionId,
    long Bucket,
    string Sentinel,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Result of verifying an agent's claim.
/// </summary>
/// <param name="Verdict">Which of the four states the claim landed in.</param>
/// <param name="SessionId">Session the claim was checked against.</param>
/// <param name="CurrentBucket">Bucket the verifier considered "now".</param>
/// <param name="MatchedBucket">Bucket whose sentinel matched, or <c>null</c> when nothing matched.</param>
/// <param name="BucketDistance">
/// How many buckets back the match was (0 = current). <c>null</c> when nothing matched.
/// </param>
/// <param name="Detail">Short, non-sensitive explanation suitable for an agent-facing message.</param>
public readonly record struct CanaryVerification(
    CanaryVerdict Verdict,
    string SessionId,
    long CurrentBucket,
    long? MatchedBucket,
    int? BucketDistance,
    string Detail)
{
    /// <summary>Wire spelling of <see cref="Verdict"/>.</summary>
    public string VerdictWire => CanaryVerdictStrings.ToWire(Verdict);

    /// <summary>
    /// Whether the claim is evidence of a real read (verified or stale). Deliberately <c>false</c>
    /// for <see cref="CanaryVerdict.ReplaySuspect"/>: the value is authentic but its provenance is not.
    /// </summary>
    public bool IsReadEvidence => Verdict is CanaryVerdict.ReadVerified or CanaryVerdict.ReadStale;
}

/// <summary>
/// One audit record for an issued sentinel. Contains no raw client address and no sentinel value —
/// only what replay detection and rate accounting need (PROBE_PROTOCOL.md §6).
/// </summary>
/// <param name="SessionId">Session the sentinel was issued for.</param>
/// <param name="Bucket">Bucket the sentinel was derived for.</param>
/// <param name="HashedClient">Peppered digest of the client identifier, or <c>"none"</c>.</param>
/// <param name="IssuedAt">Issue instant, UTC.</param>
/// <param name="UserAgent">Truncated user-agent string, or <c>null</c> when absent.</param>
public readonly record struct CanaryIssueRecord(
    string SessionId,
    long Bucket,
    string HashedClient,
    DateTimeOffset IssuedAt,
    string? UserAgent);
