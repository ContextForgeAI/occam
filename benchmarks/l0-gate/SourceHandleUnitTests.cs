using OccamMcp.Core.Digest;
using OccamMcp.Core.Handles;
using OccamMcp.Core.Routing;

namespace OccamMcp.L0Gate;

internal static class SourceHandleUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        assert("handle syntax S1", SourceHandleSyntax.IsHandle("S1"));
        assert("handle syntax S20", SourceHandleSyntax.IsHandle("s20"));
        assert("handle syntax S21 rejected", !SourceHandleSyntax.IsHandle("S21"));
        assert("handle syntax durable", SourceHandleSyntax.IsHandle("H00000001"));
        assert("handle syntax url rejected", !SourceHandleSyntax.IsHandle("https://example.com/"));

        var now = DateTimeOffset.Parse("2026-09-05T12:00:00Z");
        var store = new SourceHandleStore(() => now, TimeSpan.FromHours(1));
        var first = store.RememberSearch("q1", [("A", "https://a.example/"), ("B", "https://b.example/")]);
        assert("handle remember count", first.Count == 2 && first[0].Handle == "H00000001");
        assert("handle remember alias", first[0].Alias == "S1" && first[1].Alias == "S2");

        assert(
            "handle resolve S1 first search",
            store.TryBind("S1", out var s1, out _, out _) && s1 == "https://a.example/");
        assert(
            "handle resolve durable first search",
            store.TryBind("H00000001", out var h1, out _, out _) && h1 == "https://a.example/");
        assert(
            "handle bind pass-through url",
            store.TryBind("https://keep.example/", out var passthrough, out _, out _)
            && passthrough == "https://keep.example/");

        var second = store.RememberSearch("q2", [("C", "https://c.example/")]);
        assert("handle second search remaps S1", second[0].Handle == "H00000003");
        assert(
            "handle S1 is latest-search only",
            store.TryBind("S1", out var latest, out _, out _) && latest == "https://c.example/");
        assert(
            "handle durable survives later search",
            store.TryBind("H00000001", out var durable, out _, out _) && durable == "https://a.example/");
        assert(
            "handle unknown S2 after remap",
            !store.TryBind("S2", out _, out var unknownCode, out var unknownMessage)
            && unknownCode == "unknown_handle"
            && unknownMessage is not null
            && unknownMessage.Contains("live aliases", StringComparison.Ordinal));

        now = now.AddHours(2);
        assert(
            "handle expired durable is stale",
            !store.TryBind("H00000001", out _, out var staleCode, out var staleMessage)
            && staleCode == "stale_handle"
            && staleMessage is not null
            && staleMessage.Contains("raw url", StringComparison.Ordinal));

        var lruNow = DateTimeOffset.Parse("2026-09-05T15:00:00Z");
        var lru = new SourceHandleStore(() => lruNow, TimeSpan.FromHours(1));
        for (var i = 0; i < SourceHandleStore.MaxEntries + 1; i++)
        {
            lru.RememberSearch($"q{i}", [("T", $"https://n{i}.example/")]);
        }

        assert("handle LRU cap", lru.Count == SourceHandleStore.MaxEntries);
        assert(
            "handle LRU evicts oldest as stale",
            !lru.TryBind("H00000001", out _, out var evicted, out _) && evicted == "stale_handle");
        assert(
            "handle LRU keeps newest",
            lru.TryBind("S1", out var newest, out _, out _)
            && newest == $"https://n{SourceHandleStore.MaxEntries}.example/");

        assert(
            "digest parser accepts S1",
            DigestUrlParser.TryParse("[\"S1\"]", out var handleEntries, out _)
            && handleEntries.Count == 1
            && handleEntries[0].Url == "S1");
        assert(
            "digest parser accepts durable handle",
            DigestUrlParser.TryParse("[\"H00000001\"]", out var durableEntries, out _)
            && durableEntries[0].Url == "H00000001");
        assert(
            "digest parser still rejects junk",
            !DigestUrlParser.TryParse("[\"not-a-url\"]", out _, out _));

        assert(
            "handle failure format stale",
            FailureCodeStrings.FormatTranscodeMessage("stale_handle", 0).Contains("expired", StringComparison.Ordinal));
        assert(
            "handle failure format unknown",
            FailureCodeStrings.FormatTranscodeMessage("unknown_handle", 0).Contains("raw url", StringComparison.Ordinal));
        assert("handle not retryable", !FailureCodeStrings.IsRetryable("stale_handle"));
    }
}
