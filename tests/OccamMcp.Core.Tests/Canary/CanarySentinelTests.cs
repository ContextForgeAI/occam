using System.Buffers.Binary;
using System.Text;
using OccamMcp.Core.Canary;
using Xunit;

namespace OccamMcp.Core.Tests.Canary;

/// <summary>
/// Wire-format rules: bucket arithmetic, canonical MAC framing, claim normalisation and session-id
/// acceptance. These are the parts a second implementation of PROBE_PROTOCOL.md must reproduce
/// byte-for-byte, so they are tested without any key material involved.
/// </summary>
public sealed class CanarySentinelTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(299, 0)]
    [InlineData(300, 1)]
    [InlineData(600, 2)]
    [InlineData(1_800_000_000, 6_000_000)]
    public void BucketFor_FloorsToBucketWidth(long unixSeconds, long expectedBucket)
    {
        var bucket = CanarySentinel.BucketFor(DateTimeOffset.FromUnixTimeSeconds(unixSeconds), 300);

        Assert.Equal(expectedBucket, bucket);
    }

    [Theory]
    [InlineData(-1, -1)]
    [InlineData(-300, -1)]
    [InlineData(-301, -2)]
    public void BucketFor_UsesFloorSemanticsBeforeTheEpoch(long unixSeconds, long expectedBucket)
    {
        // Integer division truncates toward zero, which would make bucket ordering non-monotonic for
        // a container reporting a pre-1970 clock. The protocol requires floor.
        var bucket = CanarySentinel.BucketFor(DateTimeOffset.FromUnixTimeSeconds(unixSeconds), 300);

        Assert.Equal(expectedBucket, bucket);
    }

    [Fact]
    public void BucketFor_IsMonotonicAcrossTheEpoch()
    {
        var previous = long.MinValue;
        for (var seconds = -1_200L; seconds <= 1_200L; seconds += 60)
        {
            var bucket = CanarySentinel.BucketFor(DateTimeOffset.FromUnixTimeSeconds(seconds), 300);
            Assert.True(bucket >= previous, $"bucket went backwards at t={seconds}");
            previous = bucket;
        }
    }

    [Fact]
    public void BucketFor_RejectsNonPositiveWidth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CanarySentinel.BucketFor(DateTimeOffset.UnixEpoch, 0));
    }

    [Fact]
    public void BuildMacInput_HasTheDocumentedLayout()
    {
        const long Bucket = 7;
        const string SessionId = "abc";

        var input = CanarySentinel.BuildMacInput(Bucket, SessionId);

        var label = Encoding.ASCII.GetBytes(CanarySentinel.DomainLabel);
        Assert.Equal(label.Length + 1 + sizeof(long) + sizeof(int) + 3, input.Length);
        Assert.Equal(label, input[..label.Length]);
        Assert.Equal(0x00, input[label.Length]);
        Assert.Equal(Bucket, BinaryPrimitives.ReadInt64BigEndian(input.AsSpan(label.Length + 1)));
        Assert.Equal(3, BinaryPrimitives.ReadInt32BigEndian(input.AsSpan(label.Length + 1 + sizeof(long))));
        Assert.Equal("abc", Encoding.UTF8.GetString(input[^3..]));
    }

    [Fact]
    public void BuildMacInput_IsInjectiveAcrossTheBucketSessionBoundary()
    {
        // Without the length prefix, ("1", bucket) and ("", bucket) style pairs could frame to the
        // same bytes. Assert the concatenation cannot be re-partitioned.
        var a = CanarySentinel.BuildMacInput(1, "23");
        var b = CanarySentinel.BuildMacInput(12, "3");

        Assert.NotEqual(Convert.ToHexString(a), Convert.ToHexString(b));
    }

    [Fact]
    public void BuildMacInput_RejectsEmptySession()
    {
        Assert.Throws<ArgumentException>(() => CanarySentinel.BuildMacInput(0, string.Empty));
    }

    [Theory]
    [InlineData("abc", "abc")]
    [InlineData("  abc  ", "abc")]
    [InlineData("\"abc\"", "abc")]
    [InlineData("'abc'", "abc")]
    [InlineData("`abc`", "abc")]
    [InlineData(" \"abc\" ", "abc")]
    public void Normalize_StripsWhatChatModelsAddAroundAValue(string claimed, string expected)
    {
        Assert.Equal(expected, CanarySentinel.Normalize(claimed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_RejectsBlankClaims(string? claimed)
    {
        Assert.Null(CanarySentinel.Normalize(claimed));
    }

    [Fact]
    public void Normalize_RejectsOverlongClaims()
    {
        var tooLong = new string('A', CanarySentinel.MaxEncodedLength + 1);

        Assert.Null(CanarySentinel.Normalize(tooLong));
    }

    [Fact]
    public void FixedTimeEquals_MatchesOnlyIdenticalEncodings()
    {
        Assert.True(CanarySentinel.FixedTimeEquals("abcDEF-_1", "abcDEF-_1"));
        Assert.False(CanarySentinel.FixedTimeEquals("abcDEF-_1", "abcDEF-_2"));
        Assert.False(CanarySentinel.FixedTimeEquals("abcDEF-_1", "abcDEF-_"));
        Assert.False(CanarySentinel.FixedTimeEquals("abcDEF-_1", string.Empty));
    }

    [Fact]
    public void FixedTimeEquals_IsCaseSensitive()
    {
        // base64url is case-significant: folding case would silently widen the accepted set.
        Assert.False(CanarySentinel.FixedTimeEquals("abc", "ABC"));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("A-Z_0.9")]
    [InlineData("vector-session")]
    public void IsValidSessionId_AcceptsTheDocumentedCharset(string sessionId)
    {
        Assert.True(CanarySentinel.IsValidSessionId(sessionId, 128));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<script>")]
    [InlineData("a b")]
    [InlineData("a/b")]
    [InlineData("a\nb")]
    [InlineData("héllo")]
    [InlineData("a\"b")]
    public void IsValidSessionId_RejectsAnythingNeedingEscaping(string? sessionId)
    {
        Assert.False(CanarySentinel.IsValidSessionId(sessionId, 128));
    }

    [Fact]
    public void IsValidSessionId_EnforcesTheLengthBound()
    {
        Assert.True(CanarySentinel.IsValidSessionId(new string('a', 128), 128));
        Assert.False(CanarySentinel.IsValidSessionId(new string('a', 129), 128));
    }

    [Fact]
    public void Encode_ProducesUnpaddedBase64Url()
    {
        var encoded = CanarySentinel.Encode([0xFF, 0xFE, 0xFD, 0xFC, 0xFB]);

        Assert.DoesNotContain('=', encoded);
        Assert.DoesNotContain('+', encoded);
        Assert.DoesNotContain('/', encoded);
    }
}
