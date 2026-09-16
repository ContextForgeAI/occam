using Microsoft.Extensions.Options;
using OccamMcp.Core.Canary;
using Xunit;

namespace OccamMcp.Core.Tests.Canary;

/// <summary>
/// Options validation. A canary whose configuration silently contradicts itself produces verdicts
/// that look authoritative and are not, so the validator fails startup instead.
/// </summary>
public sealed class CanaryOptionsTests
{
    private static ValidateOptionsResult Validate(CanaryOptions options) =>
        new CanaryOptionsValidator().Validate(name: null, options);

    [Fact]
    public void DefaultsAreTheNormativeProtocolValues()
    {
        var options = new CanaryOptions();

        Assert.Equal(300, options.BucketSeconds);
        Assert.Equal(1, options.FreshBucketTolerance);
        Assert.Equal(24, options.StaleBucketHorizon);
        Assert.Equal(32, options.SentinelBytes);
        Assert.Equal(24, options.IssueLogRetentionHours);
        Assert.True(Validate(options).Succeeded);
    }

    [Fact]
    public void StaleHorizonInsideTheFreshWindowIsRejected()
    {
        // Otherwise no bucket could ever be stale and READ_STALE would be unreachable.
        var result = Validate(new CanaryOptions { FreshBucketTolerance = 4, StaleBucketHorizon = 4 });

        Assert.True(result.Failed);
        Assert.Contains("must exceed", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(8)]
    [InlineData(33)]
    public void TagLengthsOutsideTheProtocolRangeAreRejected(int sentinelBytes)
    {
        Assert.True(Validate(new CanaryOptions { SentinelBytes = sentinelBytes }).Failed);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    public void TagLengthsInsideTheProtocolRangeAreAccepted(int sentinelBytes)
    {
        Assert.True(Validate(new CanaryOptions { SentinelBytes = sentinelBytes }).Succeeded);
    }

    [Theory]
    [InlineData(29)]
    [InlineData(3_601)]
    public void BucketWidthsOutsideTheSupportedRangeAreRejected(int bucketSeconds)
    {
        Assert.True(Validate(new CanaryOptions { BucketSeconds = bucketSeconds }).Failed);
    }

    [Fact]
    public void AllFailuresAreReportedTogether()
    {
        var result = Validate(new CanaryOptions
        {
            BucketSeconds = 1,
            SentinelBytes = 1,
            RateLimitRequestsPerWindow = 0,
            MaxSessionIdLength = 1,
        });

        Assert.True(result.Failed);
        Assert.True(result.Failures!.Count() >= 4, "validator should not stop at the first problem");
    }

    [Fact]
    public void ValidatorRejectsANullInstance()
    {
        Assert.Throws<ArgumentNullException>(() => Validate(null!));
    }

    [Fact]
    public void EnvironmentBindingFallsBackToDefaultsWhenUnset()
    {
        // Local research/canary runs often leave OCCAM_CANARY_* in the process environment.
        // Snapshot and clear the binding keys so this asserts the unset install path.
        string[] keys =
        [
            "OCCAM_CANARY_BUCKET_SECONDS",
            "OCCAM_CANARY_FRESH_TOLERANCE",
            "OCCAM_CANARY_STALE_HORIZON",
            "OCCAM_CANARY_SENTINEL_BYTES",
            "OCCAM_CANARY_ISSUE_LOG_HOURS",
            "OCCAM_CANARY_ISSUE_LOG_CAPACITY",
            "OCCAM_CANARY_RATE_LIMIT",
            "OCCAM_CANARY_RATE_WINDOW_SECONDS",
        ];
        var prior = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            prior[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, null);
        }

        try
        {
            var options = CanaryOptions.ReadFromEnvironment();

            Assert.True(Validate(options).Succeeded);
            Assert.Equal(CanaryOptions.DefaultBucketSeconds, options.BucketSeconds);
        }
        finally
        {
            foreach (var (key, value) in prior)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
