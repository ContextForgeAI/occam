namespace OccamMcp.Core.Knowledge.Extraction;

/// <summary>
/// Host-side lossless bridge: attach <see cref="SurfaceSpan"/> offsets by locating each block's
/// text inside the opaque source surface. Deterministic / no network.
/// </summary>
public static class SurfaceSpanAttacher
{
    public static KnowledgeDocument Attach(KnowledgeDocument document, string surfaceText)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.IsEmpty || string.IsNullOrEmpty(surfaceText))
        {
            return document;
        }

        var cursor = 0;
        var blocks = new List<KnowledgeBlock>(document.Blocks.Count);
        foreach (var b in document.Blocks)
        {
            SurfaceSpan? span = null;
            if (!string.IsNullOrEmpty(b.Text))
            {
                span = SurfaceSpan.TryFind(surfaceText, b.Text, cursor);
                if (span is not null)
                {
                    cursor = span.End;
                }
                else
                {
                    // Retry from start once — blocks may be reordered relative to markdown.
                    span = SurfaceSpan.TryFind(surfaceText, b.Text, 0);
                    if (span is not null)
                    {
                        cursor = span.End;
                    }
                }
            }

            blocks.Add(b with { Span = span });
        }

        return document with { Blocks = blocks };
    }
}
