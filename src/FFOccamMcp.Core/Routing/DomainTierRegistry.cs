using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Routing;

public sealed record DomainTierMatch(
    string TierId,
    string Label,
    bool HttpOnly,
    string? PageClassHint,
    string? QualityModeHint);

public static class DomainTierRegistry
{
    private static readonly object CacheLock = new();
    private static IReadOnlyList<DomainTierEntry>? _cache;

    public static DomainTierMatch? TryResolve(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var host = uri.Host.ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        foreach (var entry in LoadEntries())
        {
            if (!entry.Hosts.Any(h => MatchesHost(host, h)))
            {
                continue;
            }

            return new DomainTierMatch(
                entry.Id,
                entry.Label,
                entry.HttpOnly,
                entry.PageClassHint,
                entry.QualityModeHint);
        }

        return null;
    }

    public static bool IsLoginPath(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        // Match a login/signin path SEGMENT, not a substring — a substring match pre-fetch-rejects
        // legitimate content whose path merely contains the word (e.g. /blog/login-best-practices,
        // /docs/login-api, /signin-widget-tutorial). occam must not claim "requires login" about a page
        // it never fetched. A dedicated login route is its own path segment (/login, /account/login,
        // /login.php, /auth/sign-in), so an exact-segment (extension-stripped) test is the right signal.
        foreach (var segment in uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = segment;
            var dot = name.IndexOf('.', StringComparison.Ordinal);
            if (dot > 0)
            {
                name = name[..dot];
            }

            if (name.Equals("login", StringComparison.OrdinalIgnoreCase)
                || name.Equals("log-in", StringComparison.OrdinalIgnoreCase)
                || name.Equals("log_in", StringComparison.OrdinalIgnoreCase)
                || name.Equals("signin", StringComparison.OrdinalIgnoreCase)
                || name.Equals("sign-in", StringComparison.OrdinalIgnoreCase)
                || name.Equals("sign_in", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Public reference pages on tier-A docs hosts (module leaves, guides) — not login walls.</summary>
    public static bool IsTierADocsReferencePage(string url)
    {
        if (IsLoginPath(url) || TryResolve(url)?.TierId != "tier_a_docs")
        {
            return false;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.AbsolutePath.Contains("/docs/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Public spec/wiki/reference URLs — skip login-wall heuristics (P2-11b/c).</summary>
    public static bool IsPublicReferencePage(string url)
    {
        if (IsLoginPath(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (IsTierADocsReferencePage(url))
        {
            return true;
        }

        var host = uri.Host.ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        var path = uri.AbsolutePath;
        if (host is "rfc-editor.org" && path.Contains("/rfc/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return host.EndsWith("wikipedia.org", StringComparison.Ordinal)
            && path.Contains("/wiki/", StringComparison.OrdinalIgnoreCase);
    }

    public static ProbeSignals ApplyTierHints(string url, ProbeSignals signals)
    {
        var tier = TryResolve(url);
        if (tier is null || signals.LikelyChallenge)
        {
            return signals;
        }

        var loginPath = IsLoginPath(url);

        if (tier.TierId == "news_consent")
        {
            return CloneSignals(
                signals,
                pageClass: tier.PageClassHint ?? "news",
                likelyCookieConsent: true);
        }

        if (tier.TierId == "anti_bot_blogs")
        {
            var challenge = signals.LikelyChallenge
                || (signals.VisibleTextRatio < 0.04 && signals.HtmlBytes > 8_000);
            return CloneSignals(
                signals,
                pageClass: challenge ? "challenge" : tier.PageClassHint ?? "article",
                likelyChallenge: challenge);
        }

        if (!tier.HttpOnly)
        {
            if (tier.PageClassHint is not null)
            {
                return CloneSignals(signals, pageClass: tier.PageClassHint);
            }

            return signals;
        }

        return CloneSignals(
            signals,
            pageClass: tier.PageClassHint ?? signals.PageClass,
            requiresJavascript: signals.SpaShell,
            spaShell: signals.SpaShell,
            likelyLoginRequired: loginPath && signals.LikelyLoginRequired);
    }

    public static bool IsNewsConsentTier(string url) =>
        TryResolve(url)?.TierId == "news_consent";

    public static bool IsAntiBotBlogTier(string url) =>
        TryResolve(url)?.TierId == "anti_bot_blogs";

    /// <summary>Social hosts where HTTP HTML embeds challenge widgets but browser transcode often succeeds on public URLs.</summary>
    public static bool IsBrowserFriendlySocialHost(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        return host is "instagram.com" or "linkedin.com"
            || host.EndsWith(".instagram.com", StringComparison.Ordinal)
            || host.EndsWith(".linkedin.com", StringComparison.Ordinal);
    }

    public static bool ShouldSuppressProbeChallengeStop(
        string url,
        double visibleTextRatio,
        int proseChars) =>
        IsBrowserFriendlySocialHost(url)
        && proseChars >= 350;

    public static bool PreferHttpOnlyRoute(string url, ProbeSignals? probe)
    {
        var tier = TryResolve(url);
        if (tier?.HttpOnly != true)
        {
            return false;
        }

        if (probe?.LikelyChallenge == true || probe?.LikelyLoginRequired == true)
        {
            return false;
        }

        if (SpaShellDetector.IsStub(probe))
        {
            return false;
        }

        if (probe?.PageClass == "documentation" && probe.HtmlBytes > 0)
        {
            var ratioCutoff = IsMicrosoftLearnHost(url) ? 0.06 : 0.03;
            if (probe.VisibleTextRatio < ratioCutoff)
            {
                return false;
            }
        }

        return true;
    }

    private static ProbeSignals CloneSignals(
        ProbeSignals source,
        string? pageClass = null,
        bool? requiresJavascript = null,
        bool? spaShell = null,
        bool? likelyLoginRequired = null,
        bool? likelyCookieConsent = null,
        bool? likelyChallenge = null) =>
        new()
        {
            PageClass = pageClass ?? source.PageClass,
            RequiresJavascript = requiresJavascript ?? source.RequiresJavascript,
            SpaShell = spaShell ?? source.SpaShell,
            LikelyCookieConsent = likelyCookieConsent ?? source.LikelyCookieConsent,
            LikelyChallenge = likelyChallenge ?? source.LikelyChallenge,
            LikelyLoginRequired = likelyLoginRequired ?? source.LikelyLoginRequired,
            LikelyPaywall = source.LikelyPaywall,
            VisibleTextRatio = source.VisibleTextRatio,
            HtmlBytes = source.HtmlBytes,
        };

    private static bool IsMicrosoftLearnHost(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Host.Contains("learn.microsoft", StringComparison.OrdinalIgnoreCase);

    private static bool MatchesHost(string host, string pattern)
    {
        var p = pattern.ToLowerInvariant();
        if (p.StartsWith("www.", StringComparison.Ordinal))
        {
            p = p[4..];
        }

        return host == p || host.EndsWith($".{p}", StringComparison.Ordinal);
    }

    private static IReadOnlyList<DomainTierEntry> LoadEntries()
    {
        lock (CacheLock)
        {
            if (_cache is not null)
            {
                return _cache;
            }

            var entries = new List<DomainTierEntry>();
            foreach (var path in EnumerateConfigPaths())
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    var json = File.ReadAllText(path);
                    var doc = JsonSerializer.Deserialize(json, DomainTierJsonContext.Default.DomainTierDocument);
                    if (doc?.Tiers is not null)
                    {
                        entries.AddRange(doc.Tiers);
                    }
                }
                catch
                {
                    // skip invalid file
                }
            }

            _cache = entries;
            return _cache;
        }
    }

    private static IEnumerable<string> EnumerateConfigPaths()
    {
        var repo = WorkerPaths.TryGetRepoRoot();
        if (repo is not null)
        {
            yield return Path.Combine(repo, "profiles", "tiers", "domain-tier.v1.json");
        }

        var extra = Environment.GetEnvironmentVariable("OCCAM_DOMAIN_TIERS_PATH");
        if (!string.IsNullOrWhiteSpace(extra))
        {
            foreach (var part in extra.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                yield return part;
            }
        }
    }

#if OCCAM_GATE
    internal static void ClearCacheForTests() => _cache = null;
#endif
}

internal sealed class DomainTierEntry
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("http_only")]
    public bool HttpOnly { get; init; }

    [JsonPropertyName("page_class_hint")]
    public string? PageClassHint { get; init; }

    [JsonPropertyName("quality_mode_hint")]
    public string? QualityModeHint { get; init; }

    [JsonPropertyName("hosts")]
    public required string[] Hosts { get; init; }
}

internal sealed class DomainTierDocument
{
    [JsonPropertyName("tiers")]
    public DomainTierEntry[]? Tiers { get; init; }
}

[JsonSerializable(typeof(DomainTierDocument))]
internal partial class DomainTierJsonContext : JsonSerializerContext;
