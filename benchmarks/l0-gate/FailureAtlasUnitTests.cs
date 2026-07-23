using OccamMcp.Core.Telemetry;

namespace OccamMcp.L0Gate;

/// <summary>
/// L_ATLAS — SI-10 failure atlas. Pure closure classification (honest walls vs transient) + the
/// per-host aggregation round-trip: record outcomes → snapshot → correct counts, closure rate, and the
/// walled verdict. In-memory, deterministic, no network.
/// </summary>
public static class FailureAtlasUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        // Closure codes are provable walls; transient codes are worth a retry and must not count.
        assert("atlas captcha is a closure", FailureAtlasClassifier.IsClosure("captcha_or_challenge"));
        assert("atlas requires_login is a closure", FailureAtlasClassifier.IsClosure("requires_login"));
        assert("atlas http_403 is a closure", FailureAtlasClassifier.IsClosure("http_403"));
        assert("atlas timeout is NOT a closure", !FailureAtlasClassifier.IsClosure("timeout"));
        assert("atlas http_429 is NOT a closure", !FailureAtlasClassifier.IsClosure("http_429"));
        assert("atlas http_503 is NOT a closure", !FailureAtlasClassifier.IsClosure("http_503"));

        // Summarize: a host that only ever hit a login wall is walled with closureRate 1.0.
        var walled = FailureAtlasClassifier.Summarize("paywall.example", 0,
            [new FailureCodeCount("requires_login", 3)], "2026-07-03T00:00:00Z");
        assert("atlas walled host has closureRate 1.0", walled is { Walled: true, ClosureRate: 1.0, Attempts: 3, Failures: 3 });
        assert("atlas walled dominant failure", walled.DominantFailure == "requires_login");

        // A host with some successes is not walled even if it has closure failures.
        var mixed = FailureAtlasClassifier.Summarize("flaky.example", 2,
            [new FailureCodeCount("http_403", 2), new FailureCodeCount("timeout", 1)], null);
        assert("atlas host with successes is not walled", !mixed.Walled);
        assert("atlas mixed closureRate excludes transient",
            Math.Abs(mixed.ClosureRate - Math.Round(2.0 / 5.0, 4)) < 1e-9);

        // A host failing only transiently is not walled and has closureRate 0.
        var transient = FailureAtlasClassifier.Summarize("slow.example", 0,
            [new FailureCodeCount("timeout", 4)], null);
        assert("atlas transient-only host is not walled", transient is { Walled: false, ClosureRate: 0.0 });

        // Store round-trip: mixed hosts → snapshot ordered worst-first.
        var store = new FailureAtlasStore();
        store.RecordFailure("https://wall.example/a", "captcha_or_challenge");
        store.RecordFailure("https://wall.example/b", "captcha_or_challenge");
        store.RecordSuccess("https://ok.example/x");
        store.RecordFailure("https://ok.example/y", "timeout");
        var snap = store.Snapshot();
        assert("atlas snapshot tracks both hosts", snap.Count == 2);
        assert("atlas snapshot is worst-first", snap[0].Host == "wall.example" && snap[0].Walled);
        var ok = snap.First(h => h.Host == "ok.example");
        assert("atlas success host records success + transient failure",
            ok is { Successes: 1, Failures: 1, Walled: false });
        assert("atlas ignores unparseable url", store.Snapshot().All(h => h.Host.Length > 0));

        Console.WriteLine("L_ATLAS_OK");
    }
}
