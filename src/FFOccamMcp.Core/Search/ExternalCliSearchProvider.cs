using System.Text.Json;
using OccamMcp.Core.External;

namespace OccamMcp.Core.Search;

/// <summary>
/// Optional keyless search via an operator-supplied local CLI
/// (<c>OCCAM_SEARCH_PROVIDER=external_cli</c>). Binary is BYO — not bundled.
/// Set <c>OCCAM_EXTERNAL_SEARCH_PATH</c> or put <c>external_cli</c> on <c>PATH</c>.
/// </summary>
public sealed class ExternalCliSearchProvider : ISearchProvider
{
    public string Name => "external_cli";
    public bool RequiresApiKey => false;
    public bool RequiresBaseUrl => false;

    public async Task<SearchOutcome> SearchAsync(
        HttpClient client,
        string query,
        int maxResults,
        string? baseUrl,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        _ = client;
        _ = baseUrl;
        _ = apiKey;
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var bin = ExternalCli.ResolveBinary("OCCAM_EXTERNAL_SEARCH_PATH", "external_cli");
        if (bin is null)
        {
            return SearchOutcome.Failure(Name, "search_error", SearchElapsed.Ms(started));
        }

        var timeoutMs = OccamMcp.Core.Configuration.OccamEnvironment.GetInt(
            "OCCAM_SEARCH_TIMEOUT_MS", defaultValue: 20_000, min: 1_000, max: 120_000);
        try
        {
            var (exit, stdout, _) = await ExternalCli.RunAsync(
                bin,
                ["search", query, "--max-results", maxResults.ToString(), "--json"],
                timeoutMs,
                cancellationToken).ConfigureAwait(false);

            if (exit == -2)
            {
                return SearchOutcome.Failure(Name, "search_timeout", SearchElapsed.Ms(started));
            }

            if (exit != 0 || string.IsNullOrWhiteSpace(stdout))
            {
                return SearchOutcome.Failure(Name, "search_error", SearchElapsed.Ms(started));
            }

            var items = ParseResults(stdout, maxResults);
            return items.Count > 0
                ? SearchOutcome.Success(Name, items, SearchElapsed.Ms(started))
                : SearchOutcome.Failure(Name, "search_error", SearchElapsed.Ms(started));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return SearchOutcome.Failure(Name, "search_error", SearchElapsed.Ms(started));
        }
    }

    /// <summary>Tolerant parse of external CLI --json search envelopes (shape may evolve).</summary>
    internal static IReadOnlyList<SearchResultItem> ParseResults(string json, int maxResults)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            JsonElement array;
            if (root.ValueKind == JsonValueKind.Array)
            {
                array = root;
            }
            else if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
            {
                array = results;
            }
            else if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                array = items;
            }
            else
            {
                return [];
            }

            var list = new List<SearchResultItem>();
            foreach (var el in array.EnumerateArray())
            {
                if (list.Count >= maxResults)
                {
                    break;
                }

                var url = ReadString(el, "url") ?? ReadString(el, "link") ?? ReadString(el, "href");
                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                var title = ReadString(el, "title") ?? ReadString(el, "name") ?? url;
                var snippet = ReadString(el, "snippet") ?? ReadString(el, "content") ?? ReadString(el, "description");
                list.Add(new SearchResultItem(SearchElapsed.Trim(title) ?? "", url.Trim(), SearchElapsed.Trim(snippet)));
            }

            return list;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? ReadString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
}
