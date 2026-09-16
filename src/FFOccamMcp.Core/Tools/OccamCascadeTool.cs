using System.ComponentModel;
using OccamMcp.Core.Cascade;
using ModelContextProtocol.Server;

namespace OccamMcp.Core.Tools;

/// <summary>
/// Progressive-disclosure page reader: one tool with <c>url</c>, optional <c>task</c> and
/// <c>budget</c>. Internally runs playbook → HTTP → browser → focus/budget → receipt with
/// per-step timeouts and graceful degradation (ADR-0017).
/// </summary>
[McpServerToolType]
public sealed class OccamCascadeTool(CascadeService cascade)
{
    [McpServerTool(Name = "occam"), Description(
        "Read a web page as compact Markdown via the Occam cascade (playbook → HTTP → browser → focus → receipt). " +
        "Pass only `url` for a default read. Optional `task` focuses the extract; optional `budget` caps tokens. " +
        "On failure `ok:false` means the page content is UNKNOWN — never guess it. " +
        "`mode=advanced` still uses the cascade but notes that specialised tools live on wider profiles.")]
    public async Task<string> Occam(
        [Description("[core] Absolute HTTP(S) URL to read (required).")] string url,
        [Description("[tokens] What you need from the page — mapped to focus_query + fit_markdown.")] string? task = null,
        [Description("[tokens] Token budget for the response (min 128). Mapped to max_tokens.")] int? budget = null,
        [Description("[core] auto (default cascade) or advanced (same extract; hints at specialised tools).")] string mode = "auto",
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await cascade.RunAsync(
            new CascadeRequest(url, task, budget, mode),
            cancellationToken).ConfigureAwait(false);
        return CascadeResponseMapper.Serialize(result);
    }
}
