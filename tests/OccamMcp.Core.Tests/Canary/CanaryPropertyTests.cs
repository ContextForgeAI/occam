using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using OccamMcp.Core.Canary;
using OccamMcp.Core.Time;

namespace OccamMcp.Core.Tests.Canary;

/// <summary>
/// Property-based checks over randomly generated inputs. Example-based tests confirm the cases we
/// thought of; these look for the ones we did not — in particular, any pair of distinct inputs that
/// collides onto the same sentinel, which would let one session's proof satisfy another's challenge.
/// </summary>
public sealed class CanaryPropertyTests
{
    private static readonly CanarySecret Secret = CanarySecret.FromRootKey(CanaryCliVerbs.FixedRootKey());

    [Property(MaxTest = 500)]
    public Property DistinctSessionsNeverShareASentinel(NonEmptyString first, NonEmptyString second, long bucket)
    {
        var a = first.Get;
        var b = second.Get;

        return (Secret.DeriveSentinel(bucket, a) != Secret.DeriveSentinel(bucket, b))
            .When(!string.Equals(a, b, StringComparison.Ordinal));
    }

    [Property(MaxTest = 500)]
    public Property DistinctBucketsNeverShareASentinel(NonEmptyString session, long first, long second)
    {
        var id = session.Get;

        return (Secret.DeriveSentinel(first, id) != Secret.DeriveSentinel(second, id))
            .When(first != second);
    }

    [Property(MaxTest = 500)]
    public bool DerivationIsDeterministic(NonEmptyString session, long bucket) =>
        Secret.DeriveSentinel(bucket, session.Get) == Secret.DeriveSentinel(bucket, session.Get);

    [Property(MaxTest = 500)]
    public Property MacFramingIsInjective(NonEmptyString first, NonEmptyString second, long firstBucket, long secondBucket)
    {
        var a = Convert.ToHexString(CanarySentinel.BuildMacInput(firstBucket, first.Get));
        var b = Convert.ToHexString(CanarySentinel.BuildMacInput(secondBucket, second.Get));

        return (a != b).When(
            firstBucket != secondBucket || !string.Equals(first.Get, second.Get, StringComparison.Ordinal));
    }

    [Property(MaxTest = 500)]
    public bool SentinelsAreAlwaysUrlSafe(NonEmptyString session, long bucket)
    {
        var sentinel = Secret.DeriveSentinel(bucket, session.Get);

        return sentinel.All(static c =>
            c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_');
    }

    [Property(MaxTest = 500)]
    public bool NormalizeIsIdempotent(string? claimed)
    {
        var once = CanarySentinel.Normalize(claimed);
        var twice = CanarySentinel.Normalize(once);

        return once is null ? twice is null : once == twice;
    }

    [Property(MaxTest = 500)]
    public bool NormalizeNeverLengthensAClaim(string? claimed)
    {
        var normalized = CanarySentinel.Normalize(claimed);

        return normalized is null || normalized.Length <= (claimed?.Length ?? 0);
    }

    [Property(MaxTest = 500)]
    public Property BucketArithmeticIsMonotonic(long firstSeconds, long secondSeconds)
    {
        // Constrain to the range DateTimeOffset can represent, then assert order is preserved.
        var a = Math.Clamp(firstSeconds, -62_135_596_800L, 253_402_300_799L);
        var b = Math.Clamp(secondSeconds, -62_135_596_800L, 253_402_300_799L);

        var bucketA = CanarySentinel.BucketFor(DateTimeOffset.FromUnixTimeSeconds(a), 300);
        var bucketB = CanarySentinel.BucketFor(DateTimeOffset.FromUnixTimeSeconds(b), 300);

        return (bucketA <= bucketB).When(a <= b);
    }

    [Property(MaxTest = 200)]
    public bool AnySentinelThisHostIssuedVerifies(NonEmptyString session, long bucketSeed)
    {
        var sessionId = Sanitize(session.Get);
        var unixSeconds = Math.Abs(bucketSeed % 2_000_000_000L);

        var clock = ManualClock.AtUnixSeconds(unixSeconds);
        using var secret = CanarySecret.Create();
        using var service = new CanaryService(secret, new CanaryOptions(), logger: null, timeProvider: clock);

        var issue = service.Issue(sessionId);

        return service.Verify(sessionId, issue.Sentinel).Verdict == CanaryVerdict.ReadVerified;
    }

    [Property(MaxTest = 200)]
    public bool NoInventedValueEverVerifies(NonEmptyString session, NonEmptyString claimed)
    {
        var sessionId = Sanitize(session.Get);

        var clock = ManualClock.AtUnixSeconds(1_800_000_000);
        using var secret = CanarySecret.Create();
        using var service = new CanaryService(secret, new CanaryOptions(), logger: null, timeProvider: clock);

        var issue = service.Issue(sessionId);
        if (string.Equals(CanarySentinel.Normalize(claimed.Get), issue.Sentinel, StringComparison.Ordinal))
        {
            return true; // astronomically unlikely, but a generated value may coincide
        }

        return service.Verify(sessionId, claimed.Get).Verdict != CanaryVerdict.ReadVerified;
    }

    /// <summary>Projects an arbitrary string onto the accepted session-id charset.</summary>
    private static string Sanitize(string raw)
    {
        var chars = raw
            .Where(static c =>
                c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_' or '.')
            .Take(64)
            .ToArray();

        return chars.Length == 0 ? "generated-session" : new string(chars);
    }
}
