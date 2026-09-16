using OccamMcp.Core.Canary;
using OccamMcp.Core.Time;
using Xunit;

namespace OccamMcp.Core.Tests.Canary;

/// <summary>
/// Rate limiting on the probe and verify endpoints, including the bound on tracked keys — the key
/// derives from caller-controlled input, so an unbounded map would be the vulnerability.
/// </summary>
public sealed class CanaryRateLimiterTests
{
    private static CanaryRateLimiter NewLimiter(ManualClock clock, int permits, int windowSeconds = 60) =>
        new(
            new CanaryOptions { RateLimitRequestsPerWindow = permits, RateLimitWindowSeconds = windowSeconds },
            clock);

    [Fact]
    public void PermitsAreGrantedUpToTheConfiguredCeiling()
    {
        var clock = ManualClock.AtUnixSeconds(1_800_000_000);
        var limiter = NewLimiter(clock, permits: 3);

        Assert.Equal(2, limiter.TryAcquire("k").Remaining);
        Assert.Equal(1, limiter.TryAcquire("k").Remaining);
        Assert.Equal(0, limiter.TryAcquire("k").Remaining);

        var refused = limiter.TryAcquire("k");

        Assert.False(refused.Allowed);
        Assert.Equal(0, refused.Remaining);
    }

    [Fact]
    public void RefusalReportsAUsableRetryAfter()
    {
        var clock = ManualClock.AtUnixSeconds(1_800_000_000);
        var limiter = NewLimiter(clock, permits: 1, windowSeconds: 60);

        limiter.TryAcquire("k");
        clock.Advance(TimeSpan.FromSeconds(20));
        var refused = limiter.TryAcquire("k");

        Assert.False(refused.Allowed);
        Assert.InRange(refused.RetryAfter, TimeSpan.FromSeconds(39), TimeSpan.FromSeconds(41));
    }

    [Fact]
    public void WindowResetsAfterItElapses()
    {
        var clock = ManualClock.AtUnixSeconds(1_800_000_000);
        var limiter = NewLimiter(clock, permits: 1, windowSeconds: 60);

        Assert.True(limiter.TryAcquire("k").Allowed);
        Assert.False(limiter.TryAcquire("k").Allowed);

        clock.Advance(TimeSpan.FromSeconds(61));

        Assert.True(limiter.TryAcquire("k").Allowed);
    }

    [Fact]
    public void KeysHaveIndependentBudgets()
    {
        var clock = ManualClock.AtUnixSeconds(1_800_000_000);
        var limiter = NewLimiter(clock, permits: 1);

        Assert.True(limiter.TryAcquire("a").Allowed);
        Assert.False(limiter.TryAcquire("a").Allowed);
        Assert.True(limiter.TryAcquire("b").Allowed);
    }

    [Fact]
    public void TrackedKeysStayBoundedUnderASpray()
    {
        var clock = ManualClock.AtUnixSeconds(1_800_000_000);
        var limiter = new CanaryRateLimiter(
            new CanaryOptions
            {
                RateLimitRequestsPerWindow = 10,
                RateLimitWindowSeconds = 600,
                RateLimitMaxTrackedKeys = 64,
            },
            clock);

        for (var i = 0; i < 5_000; i++)
        {
            limiter.TryAcquire($"key-{i}");
        }

        Assert.InRange(limiter.TrackedKeys, 1, 64);
    }

    [Fact]
    public void ExpiredKeysAreReclaimedBeforeLiveOnesAreDropped()
    {
        var clock = ManualClock.AtUnixSeconds(1_800_000_000);
        var limiter = new CanaryRateLimiter(
            new CanaryOptions
            {
                RateLimitRequestsPerWindow = 5,
                RateLimitWindowSeconds = 60,
                RateLimitMaxTrackedKeys = 64,
            },
            clock);

        for (var i = 0; i < 64; i++)
        {
            limiter.TryAcquire($"stale-{i}");
        }

        clock.Advance(TimeSpan.FromSeconds(61));
        limiter.TryAcquire("fresh");

        // All the stale windows expired, so the fresh key did not have to displace a live one.
        Assert.Equal(1, limiter.TrackedKeys);
    }

    [Fact]
    public void BlankKeysAreRejected()
    {
        var limiter = NewLimiter(ManualClock.AtUnixSeconds(0), permits: 1);

        Assert.Throws<ArgumentNullException>(() => limiter.TryAcquire(null!));
        Assert.Throws<ArgumentException>(() => limiter.TryAcquire(string.Empty));
    }

    [Fact]
    public void ServiceScopesTheBudgetPerSessionAndClient()
    {
        var clock = ManualClock.AtUnixSeconds(1_800_000_000);
        using var secret = CanarySecret.FromRootKey(CanaryCliVerbs.FixedRootKey());
        using var service = new CanaryService(
            secret,
            new CanaryOptions { RateLimitRequestsPerWindow = 1 },
            logger: null,
            timeProvider: clock);

        Assert.True(service.TryAcquire("session-a", "203.0.113.1").Allowed);
        Assert.False(service.TryAcquire("session-a", "203.0.113.1").Allowed);
        // A different client on the same session, and the same client on a different session, both
        // keep their own budget.
        Assert.True(service.TryAcquire("session-a", "203.0.113.2").Allowed);
        Assert.True(service.TryAcquire("session-b", "203.0.113.1").Allowed);
    }

    [Fact]
    public void Constructor_RejectsMissingOptions()
    {
        Assert.Throws<ArgumentNullException>(() => new CanaryRateLimiter(null!));
    }
}
