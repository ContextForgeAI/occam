using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OccamMcp.Core.Canary;

/// <summary>
/// Entry point for the proof-of-read canary: mints session ids and sentinels, records issuance, and
/// verifies what an agent claims to have read.
/// </summary>
/// <remarks>
/// The sentinel value itself is never logged. A log line containing one would let anybody who can
/// read logs produce a <see cref="CanaryVerdict.ReadVerified"/> without reading anything, which
/// would defeat the whole mechanism. Records keep the session id, the bucket, a peppered client
/// digest and a truncated user-agent — enough to detect replay and abuse, and nothing more
/// (PROBE_PROTOCOL.md §6).
/// </remarks>
public sealed class CanaryService : IDisposable
{
    private const int SessionIdEntropyBytes = 16;

    private readonly CanarySecret _secret;
    private readonly CanaryOptions _options;
    private readonly CanaryIssueLog _issueLog;
    private readonly CanaryRateLimiter _rateLimiter;
    private readonly CanaryVerifier _verifier;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CanaryService> _logger;
    private readonly bool _ownsSecret;
    private bool _disposed;

    /// <summary>
    /// Creates a service with a freshly generated process secret. This is the composition-root path.
    /// </summary>
    /// <param name="options">Active canary options.</param>
    /// <param name="logger">Structured log sink; defaults to a no-op logger.</param>
    /// <param name="timeProvider">Clock source; injectable for tests.</param>
    public CanaryService(
        CanaryOptions options,
        ILogger<CanaryService>? logger = null,
        TimeProvider? timeProvider = null)
        : this(CanarySecret.Create(), options, logger, timeProvider, ownsSecret: true)
    {
    }

    /// <summary>
    /// Creates a service over a caller-supplied secret, which the caller keeps ownership of. Used by
    /// tests and by the published test vectors, where the key must be fixed.
    /// </summary>
    /// <param name="secret">Key material to use.</param>
    /// <param name="options">Active canary options.</param>
    /// <param name="logger">Structured log sink; defaults to a no-op logger.</param>
    /// <param name="timeProvider">Clock source; injectable for tests.</param>
    public CanaryService(
        CanarySecret secret,
        CanaryOptions options,
        ILogger<CanaryService>? logger = null,
        TimeProvider? timeProvider = null)
        : this(secret, options, logger, timeProvider, ownsSecret: false)
    {
    }

    private CanaryService(
        CanarySecret secret,
        CanaryOptions options,
        ILogger<CanaryService>? logger,
        TimeProvider? timeProvider,
        bool ownsSecret)
    {
        _secret = secret ?? throw new ArgumentNullException(nameof(secret));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<CanaryService>.Instance;
        _ownsSecret = ownsSecret;
        _issueLog = new CanaryIssueLog(options, _timeProvider);
        _rateLimiter = new CanaryRateLimiter(options, _timeProvider);
        _verifier = new CanaryVerifier(secret, _issueLog, options, _timeProvider);
    }

    /// <summary>Options this service was built with.</summary>
    public CanaryOptions Options => _options;

    /// <summary>Issuance audit trail, exposed for diagnostics and tests.</summary>
    public CanaryIssueLog IssueLog => _issueLog;

    /// <summary>
    /// Generates a fresh, unguessable session id. One id per agent session keeps sentinels from
    /// being interchangeable between sessions.
    /// </summary>
    /// <returns>A 128-bit random id in unpadded base64url.</returns>
    public static string NewSessionId() =>
        CanarySentinel.Encode(RandomNumberGenerator.GetBytes(SessionIdEntropyBytes));

    /// <summary>Consumes one rate-limit permit for a caller.</summary>
    /// <param name="sessionId">Session being served.</param>
    /// <param name="clientIdentifier">Raw client identifier (for example a remote IP); hashed before use.</param>
    /// <returns>The rate-limit decision.</returns>
    public CanaryRateDecision TryAcquire(string sessionId, string? clientIdentifier)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        return _rateLimiter.TryAcquire($"{sessionId}|{_secret.HashIdentifier(clientIdentifier)}");
    }

    /// <summary>
    /// Derives the sentinel for the current bucket and records the issuance.
    /// </summary>
    /// <param name="sessionId">Session to bind the sentinel to.</param>
    /// <param name="clientIdentifier">Raw client identifier; hashed before it reaches any record.</param>
    /// <param name="userAgent">Client user-agent; truncated and stripped of control characters.</param>
    /// <returns>The issued sentinel with its validity window.</returns>
    /// <exception cref="ArgumentException"><paramref name="sessionId"/> is not an accepted token.</exception>
    public CanaryIssue Issue(string sessionId, string? clientIdentifier = null, string? userAgent = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!CanarySentinel.IsValidSessionId(sessionId, _options.MaxSessionIdLength))
        {
            throw new ArgumentException(
                $"Session id must be 1..{_options.MaxSessionIdLength} chars of [A-Za-z0-9._-].", nameof(sessionId));
        }

        var now = _timeProvider.GetUtcNow();
        var bucket = CanarySentinel.BucketFor(now, _options.BucketSeconds);
        var sentinel = _secret.DeriveSentinel(bucket, sessionId, _options.SentinelBytes);
        var bucketLength = TimeSpan.FromSeconds(_options.BucketSeconds);
        var bucketStart = DateTimeOffset.FromUnixTimeSeconds(bucket * _options.BucketSeconds);
        var expiresAt = bucketStart + bucketLength * (_options.FreshBucketTolerance + 1);

        var hashedClient = _secret.HashIdentifier(clientIdentifier);
        var record = new CanaryIssueRecord(
            sessionId,
            bucket,
            hashedClient,
            now,
            SanitizeUserAgent(userAgent, _options.MaxUserAgentLength));
        _issueLog.Record(record);

        _logger.LogInformation(
            "canary_issued session={SessionId} bucket={Bucket} client={HashedClient} userAgent={UserAgent} retained={Retained}",
            sessionId,
            bucket,
            hashedClient,
            record.UserAgent ?? "none",
            _issueLog.Count);

        return new CanaryIssue(sessionId, bucket, sentinel, now, expiresAt);
    }

    /// <summary>Verifies a claimed sentinel and logs the verdict.</summary>
    /// <param name="sessionId">Session the claim belongs to.</param>
    /// <param name="claimedSentinel">Value the agent reported.</param>
    /// <returns>The verdict and matching bucket, if any.</returns>
    public CanaryVerification Verify(string? sessionId, string? claimedSentinel)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var verification = _verifier.Verify(sessionId, claimedSentinel);

        _logger.LogInformation(
            "canary_verified session={SessionId} verdict={Verdict} currentBucket={CurrentBucket} matchedBucket={MatchedBucket} distance={BucketDistance}",
            verification.SessionId,
            verification.VerdictWire,
            verification.CurrentBucket,
            verification.MatchedBucket,
            verification.BucketDistance);

        return verification;
    }

    /// <summary>Disposes the secret when this instance created it.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_ownsSecret)
        {
            _secret.Dispose();
        }
    }

    /// <summary>
    /// Truncates a user-agent and drops control characters, so a hostile header cannot forge log
    /// structure (newline injection) or bloat a record.
    /// </summary>
    private static string? SanitizeUserAgent(string? userAgent, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        var span = userAgent.AsSpan(0, Math.Min(userAgent.Length, maxLength));
        var buffer = new char[span.Length];
        var written = 0;
        foreach (var c in span)
        {
            buffer[written++] = char.IsControl(c) ? ' ' : c;
        }

        return new string(buffer, 0, written).Trim();
    }
}
