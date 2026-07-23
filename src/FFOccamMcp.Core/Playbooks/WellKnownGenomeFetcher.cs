using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using OccamMcp.Core.Routing;

namespace OccamMcp.Core.Playbooks;

public sealed record WellKnownGenomeFetchResult(
    bool Ok,
    string WellKnownUrl,
    string? RawJson,
    string? FailureCode,
    int LatencyMs,
    bool CacheHit);

public sealed class WellKnownGenomeFetcher(IHttpClientFactory httpClientFactory)
{
    private const int MaxBytes = 32 * 1024;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    public WellKnownGenomeFetchResult Fetch(string pageUrl, string host, int timeoutMs = 8000)
    {
        var wellKnownUrl = BuildWellKnownUrl(pageUrl, host);
        if (wellKnownUrl is null)
        {
            return new WellKnownGenomeFetchResult(false, "", null, "invalid_host", 0, false);
        }

        var privacy = PrivacyClassifier.Classify(wellKnownUrl);
        if (privacy.IsPrivateHost)
        {
            return new WellKnownGenomeFetchResult(false, wellKnownUrl, null, "private_url_blocked", 0, false);
        }

        if (_cache.TryGetValue(host, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return new WellKnownGenomeFetchResult(
                cached.RawJson is not null,
                wellKnownUrl,
                cached.RawJson,
                cached.FailureCode,
                0,
                true);
        }

        var started = Environment.TickCount64;
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(Math.Clamp(timeoutMs, 2000, 30_000)));
            var client = httpClientFactory.CreateClient("playbook.wellKnownGenome");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (compatible; OccamMcp/0.8; +agent-genome)");

            using var request = new HttpRequestMessage(HttpMethod.Get, wellKnownUrl);
            using var response = client.Send(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            var latency = (int)Math.Min(int.MaxValue, Environment.TickCount64 - started);

            if (!response.IsSuccessStatusCode)
            {
                var code = $"http_{(int)response.StatusCode}";
                StoreCache(host, null, code);
                return new WellKnownGenomeFetchResult(false, wellKnownUrl, null, code, latency, false);
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (!contentType.Contains("json", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(contentType))
            {
                StoreCache(host, null, "not_json");
                return new WellKnownGenomeFetchResult(false, wellKnownUrl, null, "not_json", latency, false);
            }

            using var stream = response.Content.ReadAsStream();
            using var reader = new StreamReader(stream);
            var body = reader.ReadToEnd();
            if (body.Length > MaxBytes)
            {
                body = body[..MaxBytes];
            }

            if (!TryValidateSiteGenome(body, host))
            {
                StoreCache(host, null, "invalid_manifest");
                return new WellKnownGenomeFetchResult(false, wellKnownUrl, null, "invalid_manifest", latency, false);
            }

            StoreCache(host, body, null);
            return new WellKnownGenomeFetchResult(true, wellKnownUrl, body, null, latency, false);
        }
        catch (TaskCanceledException)
        {
            var latency = (int)Math.Min(int.MaxValue, Environment.TickCount64 - started);
            StoreCache(host, null, "timeout");
            return new WellKnownGenomeFetchResult(false, wellKnownUrl, null, "timeout", latency, false);
        }
        catch
        {
            var latency = (int)Math.Min(int.MaxValue, Environment.TickCount64 - started);
            return new WellKnownGenomeFetchResult(false, wellKnownUrl, null, "network_error", latency, false);
        }
    }

    internal static string? BuildWellKnownUrl(string pageUrl, string host)
    {
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            return null;
        }

        var builder = new UriBuilder(uri.Scheme, host, uri.Port, "/.well-known/agent-genome.v1.json");
        return builder.Uri.ToString();
    }

    internal static bool TryValidateSiteGenome(string json, string host)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("schema_version", out var versionEl)
                || string.IsNullOrWhiteSpace(versionEl.GetString())
                || !versionEl.GetString()!.StartsWith("1.", StringComparison.Ordinal))
            {
                return false;
            }

            if (!root.TryGetProperty("hosts", out var hostsEl) || hostsEl.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var normalized = PlaybookDocument.NormalizeHost(host);
            foreach (var entry in hostsEl.EnumerateArray())
            {
                var pattern = entry.GetString();
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    continue;
                }

                if (PlaybookDocument.HostMatches(normalized, [pattern]))
                {
                    return true;
                }
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal void ClearCacheForTests() => _cache.Clear();

    private void StoreCache(string host, string? rawJson, string? failureCode) =>
        _cache[host] = new CacheEntry(rawJson, failureCode, DateTimeOffset.UtcNow.Add(CacheTtl));

    private sealed record CacheEntry(string? RawJson, string? FailureCode, DateTimeOffset ExpiresAt);
}
