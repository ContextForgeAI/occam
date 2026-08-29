using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Backends.Managed;

/// <summary>
/// Wayback Machine recovery (<c>OCCAM_MANAGED_PROVIDER=archive</c>). Keyless.
/// Looks up the closest snapshot, fetches original bytes (<c>id_</c>), and emits rough Markdown.
/// Not a full HTML fidelity extract — last-rung recovery only.
/// </summary>
public sealed partial class ArchiveWaybackProvider : IManagedProvider
{
    public string Name => "archive";
    public bool RequiresApiKey => false;

    public ExtractRunResult Fetch(HttpClient client, string url, string? apiKey, string? baseUrl, CancellationToken cancellationToken)
    {
        _ = apiKey;
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var availabilityRoot = string.IsNullOrWhiteSpace(baseUrl)
            ? "https://archive.org"
            : baseUrl.TrimEnd('/');
        try
        {
            var availableUrl =
                $"{availabilityRoot}/wayback/available?url={Uri.EscapeDataString(url)}";
            using var availReq = new HttpRequestMessage(HttpMethod.Get, availableUrl);
            using var availResp = client.Send(availReq, cancellationToken);
            if (!availResp.IsSuccessStatusCode)
            {
                return ManagedResults.Failure(Name, (int)availResp.StatusCode, ManagedElapsed.Ms(started));
            }

            using var availStream = availResp.Content.ReadAsStream(cancellationToken);
            using var availDoc = JsonDocument.Parse(availStream);
            if (!TryReadSnapshotUrl(availDoc.RootElement, out var snapshotUrl) || snapshotUrl is null)
            {
                return new ExtractRunResult(
                    false, null, ManagedResults.BackendName(Name), "extraction_failed",
                    ManagedElapsed.Ms(started), url, false);
            }

            var originalUrl = ToIdentitySnapshotUrl(snapshotUrl);
            using var pageReq = new HttpRequestMessage(HttpMethod.Get, originalUrl);
            pageReq.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,text/plain,*/*");
            using var pageResp = client.Send(pageReq, cancellationToken);
            if (!pageResp.IsSuccessStatusCode)
            {
                return ManagedResults.Failure(Name, (int)pageResp.StatusCode, ManagedElapsed.Ms(started));
            }

            using var pageStream = pageResp.Content.ReadAsStream(cancellationToken);
            using var reader = new StreamReader(pageStream, Encoding.UTF8);
            var body = reader.ReadToEnd();
            var markdown = HtmlToRoughMarkdown(body);
            return ManagedResults.FromMarkdown(Name, markdown, originalUrl, ManagedElapsed.Ms(started));
        }
        catch (Exception ex)
        {
            return ManagedResults.Exception(Name, ex, ManagedElapsed.Ms(started));
        }
    }

    internal static bool TryReadSnapshotUrl(JsonElement root, out string? snapshotUrl)
    {
        snapshotUrl = null;
        if (!root.TryGetProperty("archived_snapshots", out var snaps)
            || !snaps.TryGetProperty("closest", out var closest))
        {
            return false;
        }

        if (closest.TryGetProperty("available", out var available)
            && available.ValueKind is JsonValueKind.False or JsonValueKind.Null)
        {
            return false;
        }

        if (!closest.TryGetProperty("url", out var urlEl) || urlEl.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        snapshotUrl = urlEl.GetString();
        return !string.IsNullOrWhiteSpace(snapshotUrl);
    }

    /// <summary>Insert <c>id_</c> so Wayback returns the original document without chrome.</summary>
    internal static string ToIdentitySnapshotUrl(string snapshotUrl)
    {
        // https://web.archive.org/web/20240101120000/https://example.com/
        // → https://web.archive.org/web/20240101120000id_/https://example.com/
        var marker = "/web/";
        var idx = snapshotUrl.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return snapshotUrl;
        }

        var after = idx + marker.Length;
        var slash = snapshotUrl.IndexOf('/', after);
        if (slash < 0)
        {
            return snapshotUrl;
        }

        var stamp = snapshotUrl[after..slash];
        if (stamp.Contains("id_", StringComparison.Ordinal))
        {
            return snapshotUrl;
        }

        return snapshotUrl[..after] + stamp + "id_" + snapshotUrl[slash..];
    }

    internal static string HtmlToRoughMarkdown(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return "";
        }

        var s = ScriptStyleRegex().Replace(html, " ");
        s = TagRegex().Replace(s, " ");
        s = System.Net.WebUtility.HtmlDecode(s);
        s = WhitespaceRegex().Replace(s, " ").Trim();
        return s.Length > 200_000 ? s[..200_000] : s;
    }

    [GeneratedRegex(@"<script[\s\S]*?</script>|<style[\s\S]*?</style>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ScriptStyleRegex();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
