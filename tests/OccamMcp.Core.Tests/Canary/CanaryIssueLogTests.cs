using OccamMcp.Core.Canary;
using OccamMcp.Core.Time;
using Xunit;

namespace OccamMcp.Core.Tests.Canary;

/// <summary>
/// The issuance ring buffer: it must answer "did we hand this out?" correctly, and it must stay
/// bounded in both time and size, because it is fed by unauthenticated requests.
/// </summary>
public sealed class CanaryIssueLogTests
{
    private static CanaryIssueRecord Record(string sessionId, long bucket, DateTimeOffset at) =>
        new(sessionId, bucket, "h1:deadbeef", at, "test-agent");

    [Fact]
    public void WasIssued_FindsARecordedPair()
    {
        var clock = ManualClock.AtUnixSeconds(1_800_000_000);
        var log = new CanaryIssueLog(new CanaryOptions(), clock);

        log.Record(Record("s", 10, clock.GetUtcNow()));

        Assert.True(log.WasIssued("s", 10));
        Assert.False(log.WasIssued("s", 11));
        Assert.False(log.WasIssued("other", 10));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WasIssued_IsFalseForABlankSession(string? sessionId)
    {
        var log = new CanaryIssueLog(new CanaryOptions());

        Assert.False(log.WasIssued(sessionId!, 1));
    }

    [Fact]
    public void Records_ExpireAfterTheRetentionWindow()
    {
        var clock = ManualClock.AtUnixSeconds(1_800_000_000);
        var log = new CanaryIssueLog(new CanaryOptions { IssueLogRetentionHours = 1 }, clock);

        log.Record(Record("s", 10, clock.GetUtcNow()));
        Assert.True(log.WasIssued("s", 10));

        clock.Advance(TimeSpan.FromMinutes(61));

        Assert.False(log.WasIssued("s", 10));
        Assert.Equal(0, log.Count);
    }

    [Fact]
    public void Records_SurviveRightUpToTheRetentionBoundary()
    {
        var clock = ManualClock.AtUnixSeconds(1_800_000_000);
        var log = new CanaryIssueLog(new CanaryOptions { IssueLogRetentionHours = 1 }, clock);

        log.Record(Record("s", 10, clock.GetUtcNow()));
        clock.Advance(TimeSpan.FromMinutes(59));

        Assert.True(log.WasIssued("s", 10));
    }

    [Fact]
    public void OldestRecordsAreEvictedOnceCapacityIsReached()
    {
        var clock = ManualClock.AtUnixSeconds(1_800_000_000);
        var log = new CanaryIssueLog(new CanaryOptions { IssueLogCapacity = 256 }, clock);

        for (var i = 0; i < 300; i++)
        {
            log.Record(Record("s", i, clock.GetUtcNow()));
        }

        Assert.Equal(256, log.Count);
        Assert.False(log.WasIssued("s", 0));
        Assert.True(log.WasIssued("s", 299));
    }

    [Fact]
    public void RepeatedIssuanceOfTheSamePairSurvivesPartialEviction()
    {
        // The index is reference counted: evicting one of two records for the same (session, bucket)
        // must not make the pair look un-issued.
        var clock = ManualClock.AtUnixSeconds(1_800_000_000);
        var log = new CanaryIssueLog(new CanaryOptions { IssueLogCapacity = 256 }, clock);

        log.Record(Record("s", 7, clock.GetUtcNow()));
        log.Record(Record("s", 7, clock.GetUtcNow()));
        for (var i = 0; i < 255; i++)
        {
            log.Record(Record("filler", i, clock.GetUtcNow()));
        }

        // Exactly one of the two duplicates has been pushed out.
        Assert.True(log.WasIssued("s", 7));

        log.Record(Record("filler", 999, clock.GetUtcNow()));

        Assert.False(log.WasIssued("s", 7));
    }

    [Fact]
    public void Snapshot_IsOrderedOldestFirstAndIndependent()
    {
        var clock = ManualClock.AtUnixSeconds(1_800_000_000);
        var log = new CanaryIssueLog(new CanaryOptions(), clock);

        log.Record(Record("s", 1, clock.GetUtcNow()));
        clock.Advance(TimeSpan.FromSeconds(1));
        log.Record(Record("s", 2, clock.GetUtcNow()));

        var snapshot = log.Snapshot();
        log.Record(Record("s", 3, clock.GetUtcNow()));

        Assert.Equal(2, snapshot.Count);
        Assert.Equal(1, snapshot[0].Bucket);
        Assert.Equal(2, snapshot[1].Bucket);
    }

    [Fact]
    public void Snapshot_CarriesNoRawClientAddress()
    {
        var clock = ManualClock.AtUnixSeconds(1_800_000_000);
        using var secret = CanarySecret.FromRootKey(CanaryCliVerbs.FixedRootKey());
        using var service = new CanaryService(secret, new CanaryOptions(), logger: null, timeProvider: clock);

        service.Issue("audit-session", clientIdentifier: "198.51.100.7", userAgent: "probe/1");

        var record = Assert.Single(service.IssueLog.Snapshot());
        Assert.DoesNotContain("198.51.100.7", record.HashedClient, StringComparison.Ordinal);
        Assert.StartsWith("h1:", record.HashedClient, StringComparison.Ordinal);
    }

    [Fact]
    public void ControlCharactersInUserAgentAreNeutralised()
    {
        // A header containing newlines must not be able to forge extra log lines.
        var clock = ManualClock.AtUnixSeconds(1_800_000_000);
        using var secret = CanarySecret.FromRootKey(CanaryCliVerbs.FixedRootKey());
        using var service = new CanaryService(secret, new CanaryOptions(), logger: null, timeProvider: clock);

        service.Issue("audit-session", userAgent: "evil\r\ncanary_issued session=spoofed");

        var record = Assert.Single(service.IssueLog.Snapshot());
        Assert.NotNull(record.UserAgent);
        Assert.DoesNotContain('\n', record.UserAgent);
        Assert.DoesNotContain('\r', record.UserAgent);
    }

    [Fact]
    public void OverlongUserAgentIsTruncated()
    {
        var clock = ManualClock.AtUnixSeconds(1_800_000_000);
        var options = new CanaryOptions { MaxUserAgentLength = 32 };
        using var secret = CanarySecret.FromRootKey(CanaryCliVerbs.FixedRootKey());
        using var service = new CanaryService(secret, options, logger: null, timeProvider: clock);

        service.Issue("audit-session", userAgent: new string('u', 500));

        var record = Assert.Single(service.IssueLog.Snapshot());
        Assert.Equal(32, record.UserAgent!.Length);
    }

    [Fact]
    public void Constructor_RejectsMissingOptions()
    {
        Assert.Throws<ArgumentNullException>(() => new CanaryIssueLog(null!));
    }
}
