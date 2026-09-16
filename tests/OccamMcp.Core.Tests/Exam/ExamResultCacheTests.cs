using OccamMcp.Core.Exam;
using OccamMcp.Core.Time;
using Xunit;

namespace OccamMcp.Core.Tests.Exam;

/// <summary>
/// Exam-result caching. The two failures that matter are opposite: serving a result past its TTL
/// (authorising a surface on stale evidence) and growing without bound (the key includes a
/// caller-influenced session id).
/// </summary>
public sealed class ExamResultCacheTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    private static ExamResult StrongResult(DateTimeOffset at) =>
        new(4, AgentTier.Strong, [], at, "exam");

    private static ExamSubject Subject(string session = "session-1") =>
        ExamSubject.Create("cursor/1.0", "model-x", session);

    [Fact]
    public void StoredResultIsRetrievable()
    {
        var clock = new ManualClock(Start);
        var cache = new ExamResultCache(timeProvider: clock);

        cache.Store(Subject(), StrongResult(Start));

        Assert.True(cache.TryGet(Subject(), out var result));
        Assert.Equal(AgentTier.Strong, result.Tier);
    }

    [Fact]
    public void ResultExpiresAfterTheTtl()
    {
        var clock = new ManualClock(Start);
        var cache = new ExamResultCache(ttlHours: 1, timeProvider: clock);
        cache.Store(Subject(), StrongResult(Start));

        clock.Advance(TimeSpan.FromMinutes(61));

        Assert.False(cache.TryGet(Subject(), out _));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void ResultSurvivesRightUpToTheTtlBoundary()
    {
        var clock = new ManualClock(Start);
        var cache = new ExamResultCache(ttlHours: 1, timeProvider: clock);
        cache.Store(Subject(), StrongResult(Start));

        clock.Advance(TimeSpan.FromMinutes(59));

        Assert.True(cache.TryGet(Subject(), out _));
    }

    [Fact]
    public void ReStoringExtendsTheLifetime()
    {
        // Regression guard: an earlier implementation tracked expiry by insertion order, so a
        // re-stored entry kept its original queue position and could be served after its new TTL or
        // block the sweep behind it. Expiry is now checked per entry.
        var clock = new ManualClock(Start);
        var cache = new ExamResultCache(ttlHours: 1, timeProvider: clock);
        cache.Store(Subject(), StrongResult(Start));

        clock.Advance(TimeSpan.FromMinutes(50));
        cache.Store(Subject(), StrongResult(clock.GetUtcNow()));
        clock.Advance(TimeSpan.FromMinutes(20));

        Assert.True(cache.TryGet(Subject(), out _));

        clock.Advance(TimeSpan.FromMinutes(45));

        Assert.False(cache.TryGet(Subject(), out _));
    }

    [Fact]
    public void ExpiredEntriesBehindALiveOneAreStillDropped()
    {
        var clock = new ManualClock(Start);
        var cache = new ExamResultCache(ttlHours: 1, timeProvider: clock);

        cache.Store(Subject("early"), StrongResult(Start));
        clock.Advance(TimeSpan.FromMinutes(30));
        cache.Store(Subject("late"), StrongResult(clock.GetUtcNow()));

        // Keep "early" alive past "late" so queue order and expiry order disagree.
        clock.Advance(TimeSpan.FromMinutes(25));
        cache.Store(Subject("early"), StrongResult(clock.GetUtcNow()));
        clock.Advance(TimeSpan.FromMinutes(40));

        Assert.True(cache.TryGet(Subject("early"), out _));
        Assert.False(cache.TryGet(Subject("late"), out _));
    }

    [Fact]
    public void CapacityIsEnforced()
    {
        var clock = new ManualClock(Start);
        var cache = new ExamResultCache(capacity: 3, timeProvider: clock);

        for (var i = 0; i < 50; i++)
        {
            cache.Store(Subject($"session-{i}"), StrongResult(Start));
        }

        Assert.InRange(cache.Count, 1, 3);
    }

    [Fact]
    public void ReStoringTheSameSubjectDoesNotConsumeCapacity()
    {
        var clock = new ManualClock(Start);
        var cache = new ExamResultCache(capacity: 3, timeProvider: clock);

        for (var i = 0; i < 20; i++)
        {
            cache.Store(Subject(), StrongResult(Start));
        }

        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void UnknownSubjectFallsBackToTheDefaultTier()
    {
        var cache = new ExamResultCache(timeProvider: new ManualClock(Start));

        var result = cache.GetOrDefault(Subject("never-examined"));

        Assert.Equal(AgentTier.Medium, result.Tier);
        Assert.Equal("default", result.Source);
        Assert.Equal(0, result.Score);
    }

    [Fact]
    public void ClearDropsEverything()
    {
        var cache = new ExamResultCache(timeProvider: new ManualClock(Start));
        cache.Store(Subject(), StrongResult(Start));

        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.False(cache.TryGet(Subject(), out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveTtlIsRejected(int ttlHours)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExamResultCache(ttlHours: ttlHours));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NonPositiveCapacityIsRejected(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExamResultCache(capacity: capacity));
    }

    // ---- subject identity ----

    [Fact]
    public void AllThreeKeyComponentsMatter()
    {
        var clock = new ManualClock(Start);
        var cache = new ExamResultCache(timeProvider: clock);
        cache.Store(ExamSubject.Create("cursor/1.0", "model-x", "s1"), StrongResult(Start));

        // A different client, a different model behind the same client, or a different session must
        // each miss: none of them is the subject that was examined.
        Assert.False(cache.TryGet(ExamSubject.Create("other/1.0", "model-x", "s1"), out _));
        Assert.False(cache.TryGet(ExamSubject.Create("cursor/1.0", "model-y", "s1"), out _));
        Assert.False(cache.TryGet(ExamSubject.Create("cursor/1.0", "model-x", "s2"), out _));
        Assert.True(cache.TryGet(ExamSubject.Create("cursor/1.0", "model-x", "s1"), out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankKeyComponentsNormaliseToUnknown(string? value)
    {
        var subject = ExamSubject.Create(value, value, value);

        Assert.Equal("unknown", subject.ClientInfo);
        Assert.Equal("unknown", subject.ModelHint);
        Assert.Equal("unknown", subject.SessionId);
    }

    [Fact]
    public void SubjectComponentsAreTrimmedButCaseSensitive()
    {
        Assert.Equal(
            ExamSubject.Create("cursor/1.0", "m", "s"),
            ExamSubject.Create("  cursor/1.0  ", " m ", " s "));
        Assert.NotEqual(
            ExamSubject.Create("cursor/1.0", "m", "s"),
            ExamSubject.Create("Cursor/1.0", "m", "s"));
    }
}
