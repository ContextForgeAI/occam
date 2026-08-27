using OccamMcp.Core.Compile;

namespace OccamMcp.Core.Compile;

/// <summary>Builds a compact table-of-contents from <see cref="SectionIndex"/>.</summary>
public static class TocBuilder
{
    public sealed record TocEntry(int Level, string Heading, string? Anchor, int Ordinal);

    public static IReadOnlyList<TocEntry> Build(string markdown, int maxEntries = 64)
    {
        var index = SectionIndex.Build(markdown ?? string.Empty);
        if (index.Sections.Count == 0)
        {
            return [];
        }

        var cap = Math.Clamp(maxEntries, 1, 256);
        var list = new List<TocEntry>(Math.Min(cap, index.Sections.Count));
        foreach (var section in index.Sections)
        {
            if (list.Count >= cap)
            {
                break;
            }

            var anchor = section.AnchorIds.Count > 0 ? section.AnchorIds[0] : null;
            list.Add(new TocEntry(section.Level, section.Heading, anchor, section.Ordinal));
        }

        return list;
    }
}
