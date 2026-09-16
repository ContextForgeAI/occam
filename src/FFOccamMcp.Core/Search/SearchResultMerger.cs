namespace OccamMcp.Core.Search;

/// <summary>
/// Merges multi-provider search hits: dedup by normalized URL, rank by how many
/// providers returned the URL (desc), then first-seen order. Title/snippet from the
/// earliest hit in provider-priority order.
/// </summary>
internal static class SearchResultMerger
{
    public static IReadOnlyList<SearchResultItem> Merge(
        IReadOnlyList<SearchOutcome> successes,
        int maxResults)
    {
        ArgumentNullException.ThrowIfNull(successes);
        if (maxResults < 1 || successes.Count == 0)
        {
            return [];
        }

        // key → (item, hitCount, firstSeenOrdinal)
        var map = new Dictionary<string, Entry>(StringComparer.Ordinal);
        var ordinal = 0;

        foreach (var outcome in successes)
        {
            if (!outcome.Ok)
            {
                continue;
            }

            foreach (var item in outcome.Results)
            {
                var key = SearchUrlNormalizer.Normalize(item.Url);
                if (key is null)
                {
                    continue;
                }

                if (map.TryGetValue(key, out var existing))
                {
                    map[key] = existing with { HitCount = existing.HitCount + 1 };
                }
                else
                {
                    map[key] = new Entry(item, HitCount: 1, FirstSeen: ordinal++);
                }
            }
        }

        return map.Values
            .OrderByDescending(e => e.HitCount)
            .ThenBy(e => e.FirstSeen)
            .Take(maxResults)
            .Select(e => e.Item)
            .ToArray();
    }

    private readonly record struct Entry(SearchResultItem Item, int HitCount, int FirstSeen);
}
