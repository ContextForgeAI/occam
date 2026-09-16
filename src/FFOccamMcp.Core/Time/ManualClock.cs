namespace OccamMcp.Core.Time;

/// <summary>
/// A <see cref="TimeProvider"/> whose "now" is set by the caller.
/// </summary>
/// <remarks>
/// <para>
/// Used by every time-driven subsystem that has to be asserted without sleeping: canary bucket
/// transitions and stale horizons, rate-limit windows, issuance retention, and exam-result expiry.
/// Asserting those against the system clock would make the suite both slow and flaky, and the
/// two-hour stale horizon would be untestable outright.
/// </para>
/// <para>
/// This lives in the product assembly rather than a test-helper package (ADR-0014) so the shipped
/// <c>occam canary selftest</c> and <c>occam exam selftest</c> verbs can prove the same transitions
/// on a machine that has no test runner installed — which is what makes the cross-platform evidence
/// in <c>docs/testing/</c> reproducible by someone else.
/// </para>
/// </remarks>
public sealed class ManualClock : TimeProvider
{
    private long _utcTicks;

    /// <summary>Creates a clock pinned to <paramref name="start"/>.</summary>
    /// <param name="start">Initial instant.</param>
    public ManualClock(DateTimeOffset start) => _utcTicks = start.UtcTicks;

    /// <summary>Creates a clock pinned to a given Unix instant.</summary>
    /// <param name="unixSeconds">Initial instant as Unix seconds.</param>
    /// <returns>A clock reading exactly that instant.</returns>
    public static ManualClock AtUnixSeconds(long unixSeconds) =>
        new(DateTimeOffset.FromUnixTimeSeconds(unixSeconds));

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _utcTicks), TimeSpan.Zero);

    /// <summary>Moves the clock forward, or backward for skew tests.</summary>
    /// <param name="delta">Amount to advance.</param>
    public void Advance(TimeSpan delta) => Interlocked.Add(ref _utcTicks, delta.Ticks);

    /// <summary>Moves the clock by whole time buckets.</summary>
    /// <param name="buckets">Number of buckets to skip; may be negative.</param>
    /// <param name="bucketSeconds">Bucket width in seconds.</param>
    public void AdvanceBuckets(int buckets, int bucketSeconds) =>
        Advance(TimeSpan.FromSeconds((long)buckets * bucketSeconds));
}
