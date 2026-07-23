using System.ComponentModel;
using System.Text.Json;
using OccamMcp.Core.Playbooks;
using OccamMcp.Core.Receipts;
using ModelContextProtocol.Server;

namespace OccamMcp.Core.Tools;

[McpServerToolType]
public sealed class OccamPlaybookResolveTool(PlaybookSeedResolver playbookSeedResolver, ReceiptSigner localSigner)
{
    [McpServerTool(Name = "occam_playbook_resolve"), Description("Look up the saved extraction recipe (playbook/genome) for a URL or host: content selectors, knowledge_schema, agent_notes, and a signature trust status. Read-only - call before transcode/extract on a known site to use its tuned recipe.")]
    public string Resolve(
        [Description("HTTP or HTTPS URL, or bare hostname (e.g. nginx.org).")] string url,
        [Description("Playbook schema version to negotiate (default 1.0).")] string schema_version = "1.0",
        [Description("Export lessons[] from local tier only (max 10).")] bool include_lessons = false,
        [Description("Fetch https://{host}/.well-known/agent-genome.v1.json (default false).")] bool fetch_site_genome = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var options = new PlaybookResolveOptions(
            url,
            string.IsNullOrWhiteSpace(schema_version) ? "1.0" : schema_version.Trim(),
            include_lessons,
            fetch_site_genome);
        var result = playbookSeedResolver.ResolveExtended(options);
        if (result.Ok)
        {
            return JsonSerializer.Serialize(
                OccamPlaybookResolveResponseMapper.MapSuccess(result, InspectSignature(result.RawWinningPlaybookJson)),
                OccamPlaybookResolveJsonContext.Default.OccamPlaybookResolveSuccessResponse);
        }

        return JsonSerializer.Serialize(
            OccamPlaybookResolveResponseMapper.MapFailure(result),
            OccamPlaybookResolveJsonContext.Default.OccamPlaybookResolveFailureResponse);
    }

    // SI-08 consumer loop: verify the winning recipe's signature against the local key before it is
    // trusted. Returns null only when there is no winning JSON to inspect (defensive).
    private OccamPlaybookSignatureInfo? InspectSignature(string? rawWinningPlaybookJson)
    {
        if (string.IsNullOrWhiteSpace(rawWinningPlaybookJson))
        {
            return null;
        }

        var status = PlaybookSignature.Inspect(
            rawWinningPlaybookJson,
            localSigner.KeyId,
            localSigner.ExportPublicKeyPem());
        return new OccamPlaybookSignatureInfo(
            status.Present,
            status.Status,
            status.KeyId,
            status.Score,
            status.PassesGate);
    }
}
