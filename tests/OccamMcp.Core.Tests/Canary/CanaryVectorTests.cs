using System.Text.Json;
using OccamMcp.Core.Canary;
using Xunit;

namespace OccamMcp.Core.Tests.Canary;

/// <summary>
/// Guards the committed cross-platform vectors in <c>docs/testing/canary-vectors.json</c>.
/// </summary>
/// <remarks>
/// The vectors are the artefact behind the cross-platform claim: the same file is re-derived on
/// macOS, Linux and Windows and must match byte-for-byte. If this test fails, either the derivation
/// changed (which is a protocol break and needs a new <see cref="CanarySentinel.DomainLabel"/>) or a
/// platform disagrees, which is a portability bug.
/// </remarks>
public sealed class CanaryVectorTests
{
    private static string VectorPath()
    {
        // Copied next to the test assembly by the project file.
        var local = Path.Combine(AppContext.BaseDirectory, "canary-vectors.json");
        if (File.Exists(local))
        {
            return local;
        }

        // Fall back to walking up to the repo root, so the test also runs from an IDE working copy.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "testing", "canary-vectors.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("canary-vectors.json was not found next to the test assembly or in the repo.");
    }

    [Fact]
    public void CommittedVectorsAreReproducedExactly()
    {
        using var secret = CanarySecret.FromRootKey(CanaryCliVerbs.FixedRootKey());
        using var document = JsonDocument.Parse(File.ReadAllText(VectorPath()));

        var mismatches = new List<string>();
        var count = 0;
        foreach (var vector in document.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var sessionId = vector.GetProperty("sessionId").GetString()!;
            var bucket = vector.GetProperty("bucket").GetInt64();
            var sentinelBytes = vector.GetProperty("sentinelBytes").GetInt32();
            var expected = vector.GetProperty("sentinel").GetString()!;

            var actual = secret.DeriveSentinel(bucket, sessionId, sentinelBytes);
            count++;
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                mismatches.Add($"bucket={bucket} bytes={sentinelBytes} expected={expected} actual={actual}");
            }
        }

        Assert.True(count >= 8, $"expected the full vector set, found {count}");
        Assert.Empty(mismatches);
    }

    [Fact]
    public void VectorFileDeclaresTheProtocolTheBinaryImplements()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(VectorPath()));
        var root = document.RootElement;

        Assert.Equal(CanaryCliVerbs.ProtocolId, root.GetProperty("protocol").GetString());
        Assert.Equal(CanarySentinel.DomainLabel, root.GetProperty("domainLabel").GetString());
        Assert.Equal(CanarySentinel.SentinelKeyInfo, root.GetProperty("sentinelKeyInfo").GetString());
        Assert.Equal(
            Convert.ToHexString(CanaryCliVerbs.FixedRootKey()).ToLowerInvariant(),
            root.GetProperty("rootKeyHex").GetString());
    }

    [Fact]
    public void EmittedVectorsUseLfAndEndWithExactlyOneNewline()
    {
        // The file is published as a byte-identical cross-platform artefact, so its line endings and
        // its trailing byte are part of the contract. A shell redirect is still not byte-faithful on
        // Windows (PowerShell re-encodes), which is why the CLI offers --out.
        var emitted = CanaryCliVerbs.EmitVectors();

        Assert.DoesNotContain('\r', emitted);
        Assert.EndsWith("}\n", emitted, StringComparison.Ordinal);
    }

    [Fact]
    public void EmittedVectorsMatchTheCommittedFileByteForByte()
    {
        var emitted = CanaryCliVerbs.EmitVectors();
        var committed = File.ReadAllText(VectorPath()).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Equal(committed, emitted);
    }

    [Fact]
    public void EmittedVectorsRoundTripThroughTheVerifier()
    {
        var emitted = CanaryCliVerbs.EmitVectors();
        var path = Path.Combine(Path.GetTempPath(), $"canary-vectors-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, emitted);

            Assert.Equal(0, CanaryCliVerbs.Vectors(["canary", "vectors", "--verify", path]));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TamperedVectorsAreRejected()
    {
        var tampered = CanaryCliVerbs.EmitVectors()
            .Replace("mENHHl1pbVogGlYUtGkyvSXzZosi-jQiJ6kpN5jmQhw", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", StringComparison.Ordinal);
        var path = Path.Combine(Path.GetTempPath(), $"canary-vectors-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, tampered);

            Assert.Equal(1, CanaryCliVerbs.Vectors(["canary", "vectors", "--verify", path]));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MissingVectorFileIsAUsageErrorNotACrash()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}.json");

        Assert.Equal(2, CanaryCliVerbs.Vectors(["canary", "vectors", "--verify", missing]));
    }

    [Fact]
    public void SelftestPassesInProcess()
    {
        // The same assertions the shipped `occam canary selftest` verb runs on a bare machine.
        Assert.Equal(0, CanaryCliVerbs.Selftest());
    }

    [Fact]
    public async Task SmokeVerbPassesInProcess()
    {
        Assert.Equal(0, await CanaryCliVerbs.SmokeAsync(["canary", "smoke"]));
    }

    [Fact]
    public void UnknownSubverbIsAUsageError()
    {
        Assert.True(CanaryCliVerbs.TryRun(["canary", "nonsense"], out var exitCode));
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public void UnrelatedVerbsAreNotClaimed()
    {
        Assert.False(CanaryCliVerbs.TryRun(["verify"], out _));
        Assert.False(CanaryCliVerbs.TryRun([], out _));
    }
}
