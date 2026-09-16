using Microsoft.Extensions.Options;
using OccamMcp.Core.Configuration;

namespace OccamMcp.Core.Canary;

/// <summary>
/// Tunables for the proof-of-read canary (PROBE_PROTOCOL.md §3). Defaults are the normative
/// protocol values; every field is validated by <see cref="CanaryOptionsValidator"/> so a bad
/// operator override fails at startup instead of silently weakening the proof.
/// </summary>
public sealed class CanaryOptions
{
    /// <summary>Normative time-bucket width. <c>bucket = floor(unixSeconds / BucketSeconds)</c>.</summary>
    public const int DefaultBucketSeconds = 300;

    /// <summary>Width of a time bucket in seconds.</summary>
    public int BucketSeconds { get; set; } = DefaultBucketSeconds;

    /// <summary>
    /// Buckets on either side of the current one that still count as <c>READ_VERIFIED</c>.
    /// Absorbs clock skew and a read that straddles a bucket boundary.
    /// </summary>
    public int FreshBucketTolerance { get; set; } = 1;

    /// <summary>
    /// Oldest bucket distance still recognised as a genuine (but stale) read. Beyond this the
    /// sentinel is treated as unknown, which keeps the verifier's work bounded.
    /// </summary>
    public int StaleBucketHorizon { get; set; } = 24;

    /// <summary>
    /// Truncation length of the HMAC-SHA256 tag, in bytes. 16 bytes (128-bit) is the floor;
    /// 32 keeps the full tag. See PROBE_PROTOCOL.md §7.3 for the forgery/echo-fidelity trade-off.
    /// </summary>
    public int SentinelBytes { get; set; } = 32;

    /// <summary>How long an issuance record stays in the ring buffer (replay detection window).</summary>
    public int IssueLogRetentionHours { get; set; } = 24;

    /// <summary>Hard cap on issuance records held in memory; oldest are overwritten first.</summary>
    public int IssueLogCapacity { get; set; } = 8192;

    /// <summary>Requests allowed per rate-limit key per window.</summary>
    public int RateLimitRequestsPerWindow { get; set; } = 30;

    /// <summary>Rate-limit window width in seconds.</summary>
    public int RateLimitWindowSeconds { get; set; } = 60;

    /// <summary>Hard cap on distinct rate-limit keys tracked; prevents unbounded growth under spray.</summary>
    public int RateLimitMaxTrackedKeys { get; set; } = 8192;

    /// <summary>Maximum accepted session id length, in UTF-16 chars.</summary>
    public int MaxSessionIdLength { get; set; } = 128;

    /// <summary>Maximum stored user-agent length; longer values are truncated before logging.</summary>
    public int MaxUserAgentLength { get; set; } = 256;

    /// <summary>
    /// Binds overrides from <c>OCCAM_CANARY_*</c> environment variables. Out-of-range values are
    /// clamped with a stderr note by <see cref="OccamEnvironment"/> rather than failing silently.
    /// </summary>
    public static CanaryOptions ReadFromEnvironment() => new()
    {
        BucketSeconds = OccamEnvironment.GetInt(
            "OCCAM_CANARY_BUCKET_SECONDS", DefaultBucketSeconds, min: 30, max: 3_600),
        FreshBucketTolerance = OccamEnvironment.GetInt(
            "OCCAM_CANARY_FRESH_TOLERANCE", 1, min: 0, max: 8),
        StaleBucketHorizon = OccamEnvironment.GetInt(
            "OCCAM_CANARY_STALE_HORIZON", 24, min: 1, max: 512),
        SentinelBytes = OccamEnvironment.GetInt(
            "OCCAM_CANARY_SENTINEL_BYTES", 32, min: 16, max: 32),
        IssueLogRetentionHours = OccamEnvironment.GetInt(
            "OCCAM_CANARY_ISSUE_LOG_HOURS", 24, min: 1, max: 168),
        IssueLogCapacity = OccamEnvironment.GetInt(
            "OCCAM_CANARY_ISSUE_LOG_CAPACITY", 8_192, min: 256, max: 1_048_576),
        RateLimitRequestsPerWindow = OccamEnvironment.GetInt(
            "OCCAM_CANARY_RATE_LIMIT", 30, min: 1, max: 10_000),
        RateLimitWindowSeconds = OccamEnvironment.GetInt(
            "OCCAM_CANARY_RATE_WINDOW_SECONDS", 60, min: 1, max: 3_600),
    };
}

/// <summary>
/// Startup validation for <see cref="CanaryOptions"/>. Rejects configurations that would break the
/// verdict state machine (for example a stale horizon inside the fresh window) or drop the tag below
/// 128 bits of forgery resistance.
/// </summary>
public sealed class CanaryOptionsValidator : IValidateOptions<CanaryOptions>
{
    /// <summary>Lowest tag length that still resists online forgery (PROBE_PROTOCOL.md §7.3).</summary>
    public const int MinSentinelBytes = 16;

    /// <summary>Full HMAC-SHA256 tag length.</summary>
    public const int MaxSentinelBytes = 32;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, CanaryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();

        if (options.BucketSeconds is < 30 or > 3_600)
        {
            failures.Add($"{nameof(options.BucketSeconds)} must be in [30..3600]; got {options.BucketSeconds}.");
        }

        if (options.FreshBucketTolerance is < 0 or > 8)
        {
            failures.Add($"{nameof(options.FreshBucketTolerance)} must be in [0..8]; got {options.FreshBucketTolerance}.");
        }

        if (options.StaleBucketHorizon <= options.FreshBucketTolerance)
        {
            failures.Add(
                $"{nameof(options.StaleBucketHorizon)} ({options.StaleBucketHorizon}) must exceed " +
                $"{nameof(options.FreshBucketTolerance)} ({options.FreshBucketTolerance}), otherwise no bucket can ever be stale.");
        }

        if (options.StaleBucketHorizon > 512)
        {
            failures.Add($"{nameof(options.StaleBucketHorizon)} must be <= 512; got {options.StaleBucketHorizon}.");
        }

        if (options.SentinelBytes is < MinSentinelBytes or > MaxSentinelBytes)
        {
            failures.Add(
                $"{nameof(options.SentinelBytes)} must be in [{MinSentinelBytes}..{MaxSentinelBytes}]; got {options.SentinelBytes}.");
        }

        if (options.IssueLogRetentionHours is < 1 or > 168)
        {
            failures.Add($"{nameof(options.IssueLogRetentionHours)} must be in [1..168]; got {options.IssueLogRetentionHours}.");
        }

        if (options.IssueLogCapacity < 256)
        {
            failures.Add($"{nameof(options.IssueLogCapacity)} must be >= 256; got {options.IssueLogCapacity}.");
        }

        if (options.RateLimitRequestsPerWindow < 1)
        {
            failures.Add($"{nameof(options.RateLimitRequestsPerWindow)} must be >= 1; got {options.RateLimitRequestsPerWindow}.");
        }

        if (options.RateLimitWindowSeconds is < 1 or > 3_600)
        {
            failures.Add($"{nameof(options.RateLimitWindowSeconds)} must be in [1..3600]; got {options.RateLimitWindowSeconds}.");
        }

        if (options.RateLimitMaxTrackedKeys < 64)
        {
            failures.Add($"{nameof(options.RateLimitMaxTrackedKeys)} must be >= 64; got {options.RateLimitMaxTrackedKeys}.");
        }

        if (options.MaxSessionIdLength is < 8 or > 4_096)
        {
            failures.Add($"{nameof(options.MaxSessionIdLength)} must be in [8..4096]; got {options.MaxSessionIdLength}.");
        }

        if (options.MaxUserAgentLength is < 16 or > 8_192)
        {
            failures.Add($"{nameof(options.MaxUserAgentLength)} must be in [16..8192]; got {options.MaxUserAgentLength}.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
