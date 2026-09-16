using OccamMcp.Core.Canary;
using Xunit;

namespace OccamMcp.Core.Tests.Canary;

/// <summary>
/// Key handling and derivation: determinism (which is what makes verification possible without
/// storing sentinels), separation between sessions and buckets, and the guarantee that key material
/// cannot leak through a string conversion or survive disposal.
/// </summary>
public sealed class CanarySecretTests
{
    private static byte[] FixedKey() => CanaryCliVerbs.FixedRootKey();

    [Fact]
    public void DeriveSentinel_IsDeterministicForTheSameInputs()
    {
        using var first = CanarySecret.FromRootKey(FixedKey());
        using var second = CanarySecret.FromRootKey(FixedKey());

        Assert.Equal(
            first.DeriveSentinel(42, "session-a"),
            second.DeriveSentinel(42, "session-a"));
    }

    [Fact]
    public void DeriveSentinel_DiffersPerSession()
    {
        using var secret = CanarySecret.FromRootKey(FixedKey());

        Assert.NotEqual(
            secret.DeriveSentinel(42, "session-a"),
            secret.DeriveSentinel(42, "session-b"));
    }

    [Fact]
    public void DeriveSentinel_DiffersPerBucket()
    {
        using var secret = CanarySecret.FromRootKey(FixedKey());

        Assert.NotEqual(
            secret.DeriveSentinel(42, "session-a"),
            secret.DeriveSentinel(43, "session-a"));
    }

    [Fact]
    public void DeriveSentinel_DiffersPerSecret()
    {
        var otherKey = FixedKey();
        otherKey[0] ^= 0xFF;

        using var a = CanarySecret.FromRootKey(FixedKey());
        using var b = CanarySecret.FromRootKey(otherKey);

        Assert.NotEqual(a.DeriveSentinel(42, "s"), b.DeriveSentinel(42, "s"));
    }

    [Fact]
    public void Create_ProducesAnIndependentKeyEveryTime()
    {
        using var a = CanarySecret.Create();
        using var b = CanarySecret.Create();

        Assert.NotEqual(a.DeriveSentinel(1, "s"), b.DeriveSentinel(1, "s"));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(32)]
    public void DeriveSentinel_TagLengthDrivesEncodedLength(int sentinelBytes)
    {
        using var secret = CanarySecret.FromRootKey(FixedKey());

        var sentinel = secret.DeriveSentinel(1, "s", sentinelBytes);

        // Unpadded base64url: ceil(bytes * 4 / 3).
        Assert.Equal((int)Math.Ceiling(sentinelBytes * 4 / 3.0), sentinel.Length);
    }

    [Fact]
    public void DeriveSentinel_TruncatedTagIsAPrefixOfTheFullTag()
    {
        using var secret = CanarySecret.FromRootKey(FixedKey());

        var full = secret.DeriveSentinel(1, "s", 32);
        var truncated = secret.DeriveSentinel(1, "s", 16);

        // 16 bytes encode to 22 base64url chars with no partial-byte boundary, so the shorter tag is
        // a literal prefix. This is what lets an operator shorten the sentinel without changing the
        // derivation.
        Assert.StartsWith(truncated[..21], full, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(33)]
    public void DeriveSentinel_RejectsTagLengthsOutsideTheProtocolRange(int sentinelBytes)
    {
        using var secret = CanarySecret.FromRootKey(FixedKey());

        Assert.Throws<ArgumentOutOfRangeException>(() => secret.DeriveSentinel(1, "s", sentinelBytes));
    }

    [Fact]
    public void DeriveSentinel_RejectsEmptySession()
    {
        using var secret = CanarySecret.FromRootKey(FixedKey());

        Assert.Throws<ArgumentException>(() => secret.DeriveSentinel(1, string.Empty));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(33)]
    public void FromRootKey_RequiresExactlyThirtyTwoBytes(int length)
    {
        Assert.Throws<ArgumentException>(() => CanarySecret.FromRootKey(new byte[length]));
    }

    [Fact]
    public void HashIdentifier_IsStableAndPeppered()
    {
        using var a = CanarySecret.FromRootKey(FixedKey());
        using var b = CanarySecret.Create();

        var underA = a.HashIdentifier("192.0.2.10");

        Assert.Equal(underA, a.HashIdentifier("192.0.2.10"));
        Assert.NotEqual(underA, a.HashIdentifier("192.0.2.11"));
        // A different process secret yields a different digest, so log records are not correlatable
        // across restarts and cannot be matched against a precomputed IP table.
        Assert.NotEqual(underA, b.HashIdentifier("192.0.2.10"));
        Assert.StartsWith("h1:", underA, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HashIdentifier_MapsMissingValuesToASentinelToken(string? value)
    {
        using var secret = CanarySecret.FromRootKey(FixedKey());

        Assert.Equal("none", secret.HashIdentifier(value));
    }

    [Fact]
    public void HashIdentifier_DoesNotEmbedTheRawValue()
    {
        using var secret = CanarySecret.FromRootKey(FixedKey());

        Assert.DoesNotContain("192.0.2.10", secret.HashIdentifier("192.0.2.10"), StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_NeverExposesKeyMaterial()
    {
        using var secret = CanarySecret.FromRootKey(FixedKey());

        var text = secret.ToString();

        Assert.Equal("CanarySecret(redacted)", text);
        Assert.DoesNotContain("00010203", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dispose_StopsFurtherDerivation()
    {
        var secret = CanarySecret.FromRootKey(FixedKey());
        secret.Dispose();

        Assert.Throws<ObjectDisposedException>(() => secret.DeriveSentinel(1, "s"));
        Assert.Throws<ObjectDisposedException>(() => secret.HashIdentifier("1.2.3.4"));
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var secret = CanarySecret.FromRootKey(FixedKey());

        secret.Dispose();
        secret.Dispose();
    }

    [Fact]
    public void CanarySecret_ExposesNoPublicReadableState()
    {
        // Guards against a future refactor adding a property that a serializer would happily pick up.
        var readable = typeof(CanarySecret)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        Assert.Empty(readable);
    }
}
