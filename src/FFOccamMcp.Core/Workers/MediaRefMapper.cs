namespace OccamMcp.Core.Workers;

public static class MediaRefMapper
{
    public static IReadOnlyList<MediaRefInfo> Map(WorkerMediaRefInfo[]? refs)
    {
        if (refs is null || refs.Length == 0)
        {
            return [];
        }

        var mapped = new List<MediaRefInfo>(refs.Length);
        foreach (var item in refs)
        {
            if (string.IsNullOrWhiteSpace(item.Url) || string.IsNullOrWhiteSpace(item.Kind))
            {
                continue;
            }

            mapped.Add(new MediaRefInfo(
                item.Url.Trim(),
                item.Kind.Trim().ToLowerInvariant(),
                string.IsNullOrWhiteSpace(item.Alt) ? null : item.Alt.Trim(),
                string.IsNullOrWhiteSpace(item.ContextHeading) ? null : item.ContextHeading.Trim(),
                string.IsNullOrWhiteSpace(item.SelectorHint) ? null : item.SelectorHint.Trim()));
        }

        return mapped;
    }
}
