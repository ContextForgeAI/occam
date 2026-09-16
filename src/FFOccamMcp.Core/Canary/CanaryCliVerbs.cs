using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using OccamMcp.Core.Time;

namespace OccamMcp.Core.Canary;

/// <summary>
/// The <c>occam canary</c> verb family: run the probe server, prove the state machine, and prove
/// that sentinel derivation is byte-identical across platforms.
/// </summary>
/// <remarks>
/// These verbs are the evidence-producing surface behind <c>docs/testing/</c>. They deliberately
/// need neither a test runner nor the Node worker tree, so the same checks run on a bare machine
/// that only has the AOT binary.
/// </remarks>
public static class CanaryCliVerbs
{
    /// <summary>Marker printed by a passing <c>canary selftest</c>.</summary>
    public const string SelftestOkMarker = "CANARY_SELFTEST_OK";

    /// <summary>Marker printed by a passing <c>canary vectors --verify</c>.</summary>
    public const string VectorsOkMarker = "CANARY_VECTORS_OK";

    /// <summary>Marker printed by a passing <c>canary smoke</c>.</summary>
    public const string SmokeOkMarker = "CANARY_SMOKE_OK";

    /// <summary>Protocol id recorded in emitted vector files.</summary>
    public const string ProtocolId = "occam-canary-v1";

    private const string VectorSessionId = "vector-session";

    /// <summary>
    /// Dispatches a <c>canary</c> sub-verb.
    /// </summary>
    /// <param name="args">Full process arguments, starting at the <c>canary</c> token.</param>
    /// <param name="exitCode">0 on success, 1 on a failed check, 2 on usage error.</param>
    /// <returns><c>true</c> when the verb was handled here.</returns>
    public static bool TryRun(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0 || !string.Equals(args[0], "canary", StringComparison.Ordinal))
        {
            return false;
        }

        var sub = args.Length >= 2 ? args[1] : "help";
        switch (sub)
        {
            case "selftest":
                exitCode = Selftest();
                return true;
            case "vectors":
                exitCode = Vectors(args);
                return true;
            case "smoke":
                exitCode = SmokeAsync(args).GetAwaiter().GetResult();
                return true;
            case "serve":
                exitCode = ServeAsync(args).GetAwaiter().GetResult();
                return true;
            default:
                PrintUsage();
                exitCode = 2;
                return true;
        }
    }

    /// <summary>
    /// Exercises every branch of the verdict state machine against a manual clock, plus the
    /// invariants that make the proof meaningful: determinism, session isolation, and the fact that
    /// a one-bucket shift changes the sentinel.
    /// </summary>
    /// <returns>0 when every assertion holds, otherwise 1.</returns>
    public static int Selftest()
    {
        var failures = new List<string>();
        var options = new CanaryOptions();
        var validation = new CanaryOptionsValidator().Validate(name: null, options);
        if (validation.Failed)
        {
            failures.Add($"default options rejected by validator: {validation.FailureMessage}");
        }

        // A bucket boundary: 1_800_000_000 is divisible by 300, so the clock starts at bucket start.
        const long BucketAlignedUnixSeconds = 1_800_000_000;
        var clock = ManualClock.AtUnixSeconds(BucketAlignedUnixSeconds);
        using var secret = CanarySecret.FromRootKey(FixedRootKey());
        using var service = new CanaryService(secret, options, logger: null, timeProvider: clock);

        const string SessionId = "selftest-session";
        var issue = service.Issue(SessionId, clientIdentifier: "127.0.0.1", userAgent: "occam-selftest/1");
        Console.Error.WriteLine($"[canary.selftest] issued bucket={issue.Bucket} sentinelChars={issue.Sentinel.Length}");

        Check(failures, "determinism: same inputs derive the same sentinel",
            secret.DeriveSentinel(issue.Bucket, SessionId) == secret.DeriveSentinel(issue.Bucket, SessionId));

        Check(failures, "session isolation: a different session derives a different sentinel",
            secret.DeriveSentinel(issue.Bucket, SessionId) != secret.DeriveSentinel(issue.Bucket, SessionId + "x"));

        Check(failures, "time binding: the next bucket derives a different sentinel",
            secret.DeriveSentinel(issue.Bucket, SessionId) != secret.DeriveSentinel(issue.Bucket + 1, SessionId));

        Check(failures, "READ_VERIFIED for a fresh, issued sentinel",
            service.Verify(SessionId, issue.Sentinel).Verdict == CanaryVerdict.ReadVerified);

        Check(failures, "READ_VERIFIED tolerates surrounding quotes and whitespace",
            service.Verify(SessionId, $"  \"{issue.Sentinel}\"  ").Verdict == CanaryVerdict.ReadVerified);

        Check(failures, "HALLUCINATED for an invented value",
            service.Verify(SessionId, "ZmFrZS1zZW50aW5lbC12YWx1ZQ").Verdict == CanaryVerdict.Hallucinated);

        Check(failures, "HALLUCINATED for an empty claim",
            service.Verify(SessionId, claimedSentinel: null).Verdict == CanaryVerdict.Hallucinated);

        Check(failures, "HALLUCINATED when the sentinel belongs to another session",
            service.Verify("other-session", issue.Sentinel).Verdict == CanaryVerdict.Hallucinated);

        // A sentinel that is cryptographically valid but was never handed out by this host.
        var unissued = secret.DeriveSentinel(issue.Bucket, "never-served-session", options.SentinelBytes);
        Check(failures, "REPLAY_SUSPECT for an authentic but unissued sentinel",
            service.Verify("never-served-session", unissued).Verdict == CanaryVerdict.ReplaySuspect);

        // Walk past the fresh tolerance: the read is still real, but no longer current.
        clock.AdvanceBuckets(options.FreshBucketTolerance + 1, options.BucketSeconds);
        var stale = service.Verify(SessionId, issue.Sentinel);
        Check(failures, "READ_STALE once the fresh window has passed",
            stale.Verdict == CanaryVerdict.ReadStale);
        Check(failures, "READ_STALE reports a positive bucket distance",
            stale.BucketDistance is > 0);

        // Walk past the stale horizon: the sentinel is no longer recognised at all.
        clock.AdvanceBuckets(options.StaleBucketHorizon + 1, options.BucketSeconds);
        Check(failures, "HALLUCINATED beyond the stale horizon",
            service.Verify(SessionId, issue.Sentinel).Verdict == CanaryVerdict.Hallucinated);

        Check(failures, "floor bucket arithmetic holds for pre-epoch instants",
            CanarySentinel.BucketFor(DateTimeOffset.FromUnixTimeSeconds(-1), 300) == -1);

        Check(failures, "bucket arithmetic is exact on a bucket boundary",
            CanarySentinel.BucketFor(DateTimeOffset.FromUnixTimeSeconds(600), 300) == 2);

        Check(failures, "rate limiter refuses once the window is exhausted", RateLimiterRefuses());

        Check(failures, "session ids reject characters that would need HTML escaping",
            !CanarySentinel.IsValidSessionId("<script>", 128));

        Check(failures, "disposed secret refuses to derive",
            DisposedSecretThrows());

        if (failures.Count > 0)
        {
            foreach (var failure in failures)
            {
                Console.Error.WriteLine($"[canary.selftest] FAIL {failure}");
            }

            Console.Out.WriteLine($"CANARY_SELFTEST_FAILED checks_failed={failures.Count}");
            return 1;
        }

        Console.Out.WriteLine(SelftestOkMarker);
        return 0;
    }

    /// <summary>
    /// Emits or verifies cross-platform test vectors. Emission is done once on a baseline platform;
    /// verification on another platform proves the derivation is byte-identical, which is the claim
    /// <c>docs/testing/RESULTS.md</c> makes.
    /// </summary>
    /// <param name="args">Verb arguments; expects <c>--emit</c> or <c>--verify</c>.</param>
    /// <returns>0 on success, 1 on mismatch, 2 on usage error.</returns>
    public static int Vectors(string[] args)
    {
        var emit = Array.IndexOf(args, "--emit") >= 0;
        var verifyIndex = Array.IndexOf(args, "--verify");
        var outIndex = Array.IndexOf(args, "--out");

        if (emit)
        {
            var json = EmitVectors();
            if (outIndex >= 0 && outIndex + 1 < args.Length)
            {
                var path = args[outIndex + 1];
                var directory = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                Console.Error.WriteLine($"[canary.vectors] wrote {path}");
            }
            else
            {
                // Write, not WriteLine: the payload already ends with LF, and WriteLine would append
                // Environment.NewLine on top — making `--emit > file` differ from `--emit --out file`
                // by a byte, and differ again between platforms. For a tool whose entire purpose is
                // byte-identical artefacts, that asymmetry is a defect.
                Console.Out.Write(json);
            }

            return 0;
        }

        if (verifyIndex >= 0 && verifyIndex + 1 < args.Length)
        {
            return VerifyVectors(args[verifyIndex + 1]);
        }

        PrintUsage();
        return 2;
    }

    /// <summary>
    /// End-to-end HTTP check: start the probe server, fetch a probe document, pull the sentinel out
    /// of it, and adjudicate both a truthful and an invented claim over the wire.
    /// </summary>
    /// <param name="args">Verb arguments; accepts <c>--port</c>.</param>
    /// <returns>0 when the full round trip behaves, otherwise 1.</returns>
    public static async Task<int> SmokeAsync(string[] args)
    {
        var port = ReadIntFlag(args, "--port", defaultValue: 0);
        var options = CanaryOptions.ReadFromEnvironment();
        using var service = new CanaryService(options);
        await using var host = new CanaryProbeServerHost(service, port);

        var listenUrl = await host.StartAsync().ConfigureAwait(false);
        Console.Error.WriteLine($"[canary.smoke] listening on {listenUrl}");

        var sessionId = CanaryService.NewSessionId();
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        var failures = new List<string>();
        var probeUrl = host.ProbeUrl(sessionId);
        using var probeResponse = await client.GetAsync(probeUrl).ConfigureAwait(false);
        var html = await probeResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

        Check(failures, "probe document returns 200", probeResponse.IsSuccessStatusCode);
        Check(failures, "probe document is not cacheable",
            probeResponse.Headers.CacheControl?.NoStore == true);

        var sentinel = ExtractMetaSentinel(html);
        Check(failures, "probe document carries an occam-sentinel meta tag", sentinel is not null);
        if (sentinel is null)
        {
            Console.Error.WriteLine("[canary.smoke] no sentinel in document; aborting");
            Console.Out.WriteLine("CANARY_SMOKE_FAILED reason=no_sentinel");
            return 1;
        }

        Check(failures, "sentinel also appears in the document body",
            html.Contains(sentinel, StringComparison.Ordinal));

        var verifiedJson = await client.GetStringAsync(host.VerifyUrl(sessionId, sentinel)).ConfigureAwait(false);
        var verifiedVerdict = ReadJsonString(verifiedJson, "verdict");
        Check(failures, $"truthful claim verifies (got {verifiedVerdict})",
            verifiedVerdict == CanaryVerdictStrings.ReadVerified);

        var bogusJson = await client.GetStringAsync(
            host.VerifyUrl(sessionId, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")).ConfigureAwait(false);
        var bogusVerdict = ReadJsonString(bogusJson, "verdict");
        Check(failures, $"invented claim is rejected (got {bogusVerdict})",
            bogusVerdict == CanaryVerdictStrings.Hallucinated);

        Console.Error.WriteLine($"[canary.smoke] verify(truthful)={verifiedJson}");
        Console.Error.WriteLine($"[canary.smoke] verify(invented)={bogusJson}");

        if (failures.Count > 0)
        {
            foreach (var failure in failures)
            {
                Console.Error.WriteLine($"[canary.smoke] FAIL {failure}");
            }

            Console.Out.WriteLine($"CANARY_SMOKE_FAILED checks_failed={failures.Count}");
            return 1;
        }

        Console.Out.WriteLine(SmokeOkMarker);
        return 0;
    }

    /// <summary>Runs the probe server until shutdown.</summary>
    /// <param name="args">Verb arguments; accepts <c>--port</c> and <c>--bind</c>.</param>
    /// <returns>0 on clean shutdown.</returns>
    public static async Task<int> ServeAsync(string[] args)
    {
        var port = ReadIntFlag(args, "--port", defaultValue: 8971);
        var bind = ReadStringFlag(args, "--bind", defaultValue: "127.0.0.1");
        var options = CanaryOptions.ReadFromEnvironment();
        using var service = new CanaryService(options);
        await using var host = new CanaryProbeServerHost(service, port, bind);

        var url = await host.StartAsync().ConfigureAwait(false);
        var sessionId = CanaryService.NewSessionId();
        Console.Error.WriteLine($"canary_probe_listening: {url}");
        Console.Error.WriteLine($"canary_probe_example: {host.ProbeUrl(sessionId)}");
        await host.WaitForShutdownAsync().ConfigureAwait(false);
        return 0;
    }

    /// <summary>Root key used for published vectors. Fixed, public, and never used in production.</summary>
    /// <returns>32 bytes with value equal to index.</returns>
    public static byte[] FixedRootKey()
    {
        var key = new byte[CanarySecret.RootKeyBytes];
        for (var i = 0; i < key.Length; i++)
        {
            key[i] = (byte)i;
        }

        return key;
    }

    /// <summary>The (sessionId, bucket, tagLength) triples covered by the published vectors.</summary>
    /// <returns>Vector inputs, in emission order.</returns>
    public static IReadOnlyList<(string SessionId, long Bucket, int SentinelBytes)> VectorInputs() =>
    [
        (VectorSessionId, 0L, 32),
        (VectorSessionId, 1L, 32),
        (VectorSessionId, -1L, 32),
        (VectorSessionId, 6_000_000L, 32),
        (VectorSessionId, 6_000_000L, 16),
        ("a", 0L, 32),
        ("session.with_dots-and-dashes", 123_456L, 32),
        (new string('z', 128), 999L, 32),
    ];

    /// <summary>Emits the published vectors as canonical JSON.</summary>
    /// <returns>UTF-8 JSON text, newline terminated.</returns>
    public static string EmitVectors()
    {
        using var secret = CanarySecret.FromRootKey(FixedRootKey());
        var buffer = new ArrayBufferWriter<byte>(2_048);
        // NewLine is pinned to LF: the default follows Environment.NewLine, which made the emitted
        // vector file differ between Windows and Unix even though every sentinel matched. The file is
        // a cross-platform artefact, so its bytes have to be platform-independent too.
        var writerOptions = new JsonWriterOptions { Indented = true, NewLine = "\n" };
        using (var writer = new Utf8JsonWriter(buffer, writerOptions))
        {
            writer.WriteStartObject();
            writer.WriteString("protocol", ProtocolId);
            writer.WriteString("domainLabel", CanarySentinel.DomainLabel);
            writer.WriteString("sentinelKeyInfo", CanarySentinel.SentinelKeyInfo);
            writer.WriteString("rootKeyHex", Convert.ToHexString(FixedRootKey()).ToLowerInvariant());
            writer.WriteString("encoding", "base64url-unpadded");
            writer.WriteStartArray("vectors");
            foreach (var (sessionId, bucket, sentinelBytes) in VectorInputs())
            {
                writer.WriteStartObject();
                writer.WriteString("sessionId", sessionId);
                writer.WriteNumber("bucket", bucket);
                writer.WriteNumber("sentinelBytes", sentinelBytes);
                writer.WriteString("sentinel", secret.DeriveSentinel(bucket, sessionId, sentinelBytes));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan) + "\n";
    }

    private static int VerifyVectors(string path)
    {
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"[canary.vectors] file not found: {path}");
            return 2;
        }

        using var secret = CanarySecret.FromRootKey(FixedRootKey());
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        if (root.TryGetProperty("protocol", out var protocol) && protocol.GetString() != ProtocolId)
        {
            Console.Error.WriteLine(
                $"[canary.vectors] protocol mismatch: file says '{protocol.GetString()}', binary implements '{ProtocolId}'");
            return 1;
        }

        var mismatches = 0;
        var checked_ = 0;
        foreach (var vector in root.GetProperty("vectors").EnumerateArray())
        {
            var sessionId = vector.GetProperty("sessionId").GetString() ?? string.Empty;
            var bucket = vector.GetProperty("bucket").GetInt64();
            var sentinelBytes = vector.GetProperty("sentinelBytes").GetInt32();
            var expected = vector.GetProperty("sentinel").GetString() ?? string.Empty;
            var actual = secret.DeriveSentinel(bucket, sessionId, sentinelBytes);
            checked_++;

            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                mismatches++;
                Console.Error.WriteLine(
                    $"[canary.vectors] MISMATCH session='{Preview(sessionId)}' bucket={bucket} bytes={sentinelBytes} " +
                    $"expected={expected} actual={actual}");
            }
        }

        Console.Error.WriteLine($"[canary.vectors] checked={checked_} mismatches={mismatches}");
        if (mismatches > 0)
        {
            Console.Out.WriteLine($"CANARY_VECTORS_FAILED mismatches={mismatches}");
            return 1;
        }

        Console.Out.WriteLine($"{VectorsOkMarker} checked={checked_}");
        return 0;
    }

    private static bool RateLimiterRefuses()
    {
        var options = new CanaryOptions { RateLimitRequestsPerWindow = 2, RateLimitWindowSeconds = 60 };
        var clock = ManualClock.AtUnixSeconds(1_800_000_000);
        var limiter = new CanaryRateLimiter(options, clock);

        if (!limiter.TryAcquire("k").Allowed || !limiter.TryAcquire("k").Allowed)
        {
            return false;
        }

        if (limiter.TryAcquire("k").Allowed)
        {
            return false;
        }

        // A separate key has its own budget, and the window resets on rollover.
        if (!limiter.TryAcquire("other").Allowed)
        {
            return false;
        }

        clock.Advance(TimeSpan.FromSeconds(61));
        return limiter.TryAcquire("k").Allowed;
    }

    private static bool DisposedSecretThrows()
    {
        var secret = CanarySecret.FromRootKey(FixedRootKey());
        secret.Dispose();
        try
        {
            secret.DeriveSentinel(0, "s");
            return false;
        }
        catch (ObjectDisposedException)
        {
            return true;
        }
    }

    internal static string? ExtractMetaSentinel(string html)
    {
        var marker = $"name=\"{CanaryPage.MetaName}\" content=\"";
        var index = html.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        var start = index + marker.Length;
        var end = html.IndexOf('"', start);
        return end > start ? html[start..end] : null;
    }

    private static string? ReadJsonString(string json, string property)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty(property, out var value) ? value.GetString() : null;
    }

    private static void Check(List<string> failures, string description, bool condition)
    {
        Console.Error.WriteLine($"[canary] {(condition ? "ok  " : "FAIL")} {description}");
        if (!condition)
        {
            failures.Add(description);
        }
    }

    private static int ReadIntFlag(string[] args, string flag, int defaultValue)
    {
        var index = Array.IndexOf(args, flag);
        if (index < 0 || index + 1 >= args.Length)
        {
            return defaultValue;
        }

        return int.TryParse(args[index + 1], CultureInfo.InvariantCulture, out var parsed) ? parsed : defaultValue;
    }

    private static string ReadStringFlag(string[] args, string flag, string defaultValue)
    {
        var index = Array.IndexOf(args, flag);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : defaultValue;
    }

    private static string Preview(string value) =>
        value.Length <= 24 ? value : value[..24] + $"…(+{value.Length - 24})";

    private static void PrintUsage()
    {
        Console.Error.WriteLine("""
            occam canary <subverb>

              selftest                 prove the verdict state machine and its invariants
              vectors --emit [--out P] emit cross-platform sentinel test vectors
              vectors --verify P       re-derive vectors from P and require a byte-identical match
              smoke [--port N]         end-to-end HTTP round trip: issue, read, verify, reject
              serve [--port N] [--bind A]  run the probe server until shutdown

            Markers on stdout: CANARY_SELFTEST_OK / CANARY_VECTORS_OK / CANARY_SMOKE_OK.
            """);
    }
}
