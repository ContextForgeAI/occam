using OccamMcp.Core.Knowledge.Legacy;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Knowledge.Extraction;

/// <summary>
/// The single adapter that turns a successful raw extract into an
/// <see cref="ExtractedKnowledgeBundle"/>. Pure / fail-closed / no network / no codec / no planner.
/// </summary>
public static class ExtractedKnowledgeAdapter
{
    /// <summary>
    /// Adapt a successful post-processed (but not yet compiled) outcome. Returns null when Ok=false.
    /// </summary>
    public static ExtractedKnowledgeBundle? TryAdapt(
        string requestUrl,
        TranscodeOutcome outcome,
        DateTimeOffset? retrievedAt = null)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (!outcome.Ok)
        {
            return null;
        }

        var surface = SourceSurface.Markdown(outcome.Markdown ?? string.Empty);
        var document = WorkerKnowledgeMapper.FromExtract(outcome.Blocks, outcome.Tables);
        document = SurfaceSpanAttacher.Attach(document, surface.Text);

        var canonical = TranscodeToCanonical.TryAdaptAcquisition(
            requestUrl,
            outcome.FinalUrl,
            outcome.Backend,
            outcome.Markdown,
            outcome.Blocks,
            outcome.Tables,
            outcome.Meta,
            contentHash: null, // final contentHash is bound after codec output
            playbookId: outcome.PlaybookId,
            playbookVersion: outcome.PlaybookVersion,
            retrievedAt: retrievedAt);

        return new ExtractedKnowledgeBundle(
            surface,
            document,
            canonical,
            outcome.FinalUrl,
            outcome.Backend,
            outcome.Blocks,
            outcome.Tables,
            outcome.Chunks,
            outcome.MediaRefs,
            outcome.Feed,
            outcome.Meta,
            outcome.Screenshot,
            outcome.BrowserProvisioned);
    }
}
