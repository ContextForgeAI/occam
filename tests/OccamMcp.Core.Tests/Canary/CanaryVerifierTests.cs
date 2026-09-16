using OccamMcp.Core.Canary;
using OccamMcp.Core.Time;
using Xunit;

namespace OccamMcp.Core.Tests.Canary;

/// <summary>
/// The verdict state machine. Every branch of <see cref="CanaryVerdict"/> is asserted, together with
/// the precedence rules that stop a valid-looking value from being upgraded to
/// <see cref="CanaryVerdict.ReadVerified"/> (PROBE_PROTOCOL.md §5).
/// </summary>
public sealed class CanaryVerifierTests
{
    private const long BucketAlignedUnixSeconds = 1_800_000_000;
    private const string SessionId = "verify-session";

    private static (CanaryService Service, ManualClock Clock, CanarySecret Secret, CanaryOptions Options) NewFixture(
        CanaryOptions? options = null)
    {
        var resolved = options ?? new CanaryOptions();
        var clock = ManualClock.AtUnixSeconds(BucketAlignedUnixSeconds);
        var secret = CanarySecret.FromRootKey(CanaryCliVerbs.FixedRootKey());
        var service = new CanaryService(secret, resolved, logger: null, timeProvider: clock);
        return (service, clock, secret, resolved);
    }

    [Fact]
    public void FreshIssuedSentinel_IsReadVerified()
    {
        var (service, _, secret, _) = NewFixture();
        using (secret)
        using (service)
        {
            var issue = service.Issue(SessionId);

            var result = service.Verify(SessionId, issue.Sentinel);

            Assert.Equal(CanaryVerdict.ReadVerified, result.Verdict);
            Assert.Equal(CanaryVerdictStrings.ReadVerified, result.VerdictWire);
            Assert.True(result.IsReadEvidence);
            Assert.Equal(0, result.BucketDistance);
            Assert.Equal(issue.Bucket, result.MatchedBucket);
        }
    }

    [Fact]
    public void SentinelFromThePreviousBucket_StaysVerifiedInsideTheTolerance()
    {
        var (service, clock, secret, options) = NewFixture();
        using (secret)
        using (service)
        {
            var issue = service.Issue(SessionId);
            clock.AdvanceBuckets(options.FreshBucketTolerance, options.BucketSeconds);

            var result = service.Verify(SessionId, issue.Sentinel);

            Assert.Equal(CanaryVerdict.ReadVerified, result.Verdict);
            Assert.Equal(options.FreshBucketTolerance, result.BucketDistance);
        }
    }

    [Fact]
    public void SentinelFromAClockRunningAhead_StaysVerified()
    {
        // A client one bucket in the future must not be punished for skew.
        var (service, clock, secret, options) = NewFixture();
        using (secret)
        using (service)
        {
            clock.AdvanceBuckets(1, options.BucketSeconds);
            var issue = service.Issue(SessionId);
            clock.AdvanceBuckets(-1, options.BucketSeconds);

            var result = service.Verify(SessionId, issue.Sentinel);

            Assert.Equal(CanaryVerdict.ReadVerified, result.Verdict);
            Assert.Equal(-1, result.BucketDistance);
        }
    }

    [Fact]
    public void SentinelPastTheFreshWindow_IsReadStale()
    {
        var (service, clock, secret, options) = NewFixture();
        using (secret)
        using (service)
        {
            var issue = service.Issue(SessionId);
            clock.AdvanceBuckets(options.FreshBucketTolerance + 1, options.BucketSeconds);

            var result = service.Verify(SessionId, issue.Sentinel);

            Assert.Equal(CanaryVerdict.ReadStale, result.Verdict);
            Assert.True(result.IsReadEvidence);
            Assert.Equal(options.FreshBucketTolerance + 1, result.BucketDistance);
        }
    }

    [Fact]
    public void SentinelAtTheEdgeOfTheStaleHorizon_IsStillReadStale()
    {
        var (service, clock, secret, options) = NewFixture();
        using (secret)
        using (service)
        {
            var issue = service.Issue(SessionId);
            clock.AdvanceBuckets(options.StaleBucketHorizon, options.BucketSeconds);

            Assert.Equal(CanaryVerdict.ReadStale, service.Verify(SessionId, issue.Sentinel).Verdict);
        }
    }

    [Fact]
    public void SentinelBeyondTheStaleHorizon_IsHallucinated()
    {
        var (service, clock, secret, options) = NewFixture();
        using (secret)
        using (service)
        {
            var issue = service.Issue(SessionId);
            clock.AdvanceBuckets(options.StaleBucketHorizon + 1, options.BucketSeconds);

            var result = service.Verify(SessionId, issue.Sentinel);

            // Past the horizon the host simply has no opinion, and the protocol reports the weakest
            // verdict rather than inventing a fifth state.
            Assert.Equal(CanaryVerdict.Hallucinated, result.Verdict);
            Assert.Null(result.MatchedBucket);
            Assert.False(result.IsReadEvidence);
        }
    }

    [Fact]
    public void AuthenticButUnissuedSentinel_IsReplaySuspect()
    {
        var (service, _, secret, options) = NewFixture();
        using (secret)
        using (service)
        {
            var currentBucket = CanarySentinel.BucketFor(
                DateTimeOffset.FromUnixTimeSeconds(BucketAlignedUnixSeconds), options.BucketSeconds);
            // Derived with the real key, but never served by this host.
            var unissued = secret.DeriveSentinel(currentBucket, "unserved-session", options.SentinelBytes);

            var result = service.Verify("unserved-session", unissued);

            Assert.Equal(CanaryVerdict.ReplaySuspect, result.Verdict);
            // Authenticity is not provenance: a replay suspect is explicitly not read evidence.
            Assert.False(result.IsReadEvidence);
            Assert.Equal(currentBucket, result.MatchedBucket);
        }
    }

    [Fact]
    public void ProvenanceIsCheckedBeforeFreshness()
    {
        // An unissued sentinel inside the fresh window must report REPLAY_SUSPECT, not READ_VERIFIED.
        var (service, _, secret, options) = NewFixture();
        using (secret)
        using (service)
        {
            var bucket = CanarySentinel.BucketFor(
                DateTimeOffset.FromUnixTimeSeconds(BucketAlignedUnixSeconds), options.BucketSeconds);
            var unissued = secret.DeriveSentinel(bucket, "fresh-but-unissued", options.SentinelBytes);

            Assert.Equal(CanaryVerdict.ReplaySuspect, service.Verify("fresh-but-unissued", unissued).Verdict);
        }
    }

    [Fact]
    public void InventedSentinel_IsHallucinated()
    {
        var (service, _, secret, _) = NewFixture();
        using (secret)
        using (service)
        {
            service.Issue(SessionId);

            var result = service.Verify(SessionId, "ZmFrZS1zZW50aW5lbC12YWx1ZS1oZXJlLW5vdC1yZWFs");

            Assert.Equal(CanaryVerdict.Hallucinated, result.Verdict);
            Assert.Null(result.MatchedBucket);
            Assert.Null(result.BucketDistance);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("I did not see a sentinel")]
    public void MissingOrProseClaim_IsHallucinated(string? claimed)
    {
        var (service, _, secret, _) = NewFixture();
        using (secret)
        using (service)
        {
            service.Issue(SessionId);

            Assert.Equal(CanaryVerdict.Hallucinated, service.Verify(SessionId, claimed).Verdict);
        }
    }

    [Fact]
    public void SentinelIsNotTransferableBetweenSessions()
    {
        var (service, _, secret, _) = NewFixture();
        using (secret)
        using (service)
        {
            var issue = service.Issue(SessionId);
            service.Issue("other-session");

            Assert.Equal(CanaryVerdict.Hallucinated, service.Verify("other-session", issue.Sentinel).Verdict);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("<script>alert(1)</script>")]
    public void InvalidSessionId_IsHallucinatedRatherThanAnException(string? sessionId)
    {
        // The session id arrives from a URL path, so hostile input must produce a verdict, not a throw.
        var (service, _, secret, _) = NewFixture();
        using (secret)
        using (service)
        {
            var result = service.Verify(sessionId, "anything");

            Assert.Equal(CanaryVerdict.Hallucinated, result.Verdict);
        }
    }

    [Fact]
    public void QuotedAndPaddedClaims_StillVerify()
    {
        var (service, _, secret, _) = NewFixture();
        using (secret)
        using (service)
        {
            var issue = service.Issue(SessionId);

            Assert.Equal(
                CanaryVerdict.ReadVerified,
                service.Verify(SessionId, $"  \"{issue.Sentinel}\"\n").Verdict);
        }
    }

    [Fact]
    public void Issue_RejectsAnUnacceptableSessionId()
    {
        var (service, _, secret, _) = NewFixture();
        using (secret)
        using (service)
        {
            Assert.Throws<ArgumentException>(() => service.Issue("bad session id"));
        }
    }

    [Fact]
    public void Issue_ReportsTheFreshWindowItPromises()
    {
        var (service, _, secret, options) = NewFixture();
        using (secret)
        using (service)
        {
            var issue = service.Issue(SessionId);

            var expected = TimeSpan.FromSeconds(options.BucketSeconds * (options.FreshBucketTolerance + 1));
            Assert.Equal(expected, issue.ExpiresAt - DateTimeOffset.FromUnixTimeSeconds(BucketAlignedUnixSeconds));
        }
    }

    [Fact]
    public void ShortenedTagLength_StillDrivesTheFullStateMachine()
    {
        var (service, clock, secret, options) = NewFixture(new CanaryOptions { SentinelBytes = 16 });
        using (secret)
        using (service)
        {
            var issue = service.Issue(SessionId);
            Assert.Equal(22, issue.Sentinel.Length);
            Assert.Equal(CanaryVerdict.ReadVerified, service.Verify(SessionId, issue.Sentinel).Verdict);

            clock.AdvanceBuckets(options.FreshBucketTolerance + 1, options.BucketSeconds);
            Assert.Equal(CanaryVerdict.ReadStale, service.Verify(SessionId, issue.Sentinel).Verdict);
        }
    }

    [Fact]
    public void VerifierRejectsNullDependencies()
    {
        var options = new CanaryOptions();
        using var secret = CanarySecret.FromRootKey(CanaryCliVerbs.FixedRootKey());
        var log = new CanaryIssueLog(options);

        Assert.Throws<ArgumentNullException>(() => new CanaryVerifier(null!, log, options));
        Assert.Throws<ArgumentNullException>(() => new CanaryVerifier(secret, null!, options));
        Assert.Throws<ArgumentNullException>(() => new CanaryVerifier(secret, log, null!));
    }
}
