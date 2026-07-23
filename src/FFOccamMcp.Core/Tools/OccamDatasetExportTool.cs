using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using OccamMcp.Core.Dataset;
using OccamMcp.Core.Routing;

namespace OccamMcp.Core.Tools;

/// <summary>
/// SI-17 — dataset export: turn a list of URLs into a signed, auditable dataset. Each URL is transcoded
/// (with json_blocks) and gets its own signed receipt; the whole set is bound by a single manifest
/// signature over the Merkle root of the per-row leaves. Verifiable two ways — each row via
/// occam_verify, and the set via the manifest (root reconstructs from the rows + one signature). A
/// provenance primitive for building auditable RAG corpora / evaluation sets.
/// </summary>
[McpServerToolType]
public sealed class OccamDatasetExportTool(IDatasetExportService datasetExportService)
{
    private const int MaxUrls = 20;

    [McpServerTool(Name = "occam_dataset_export"), Description("Build a signed, auditable dataset from a set of URLs (1-20): each row is transcoded with its own signed receipt, and one manifest signature covers the Merkle root of all rows - so the set is tamper-evident and verifiable per-row (occam_verify) and per-set. Use to hand off an auditable corpus for RAG / eval / provenance. Response: { ok, manifest:{ v, createdAt, rowCount, manifestRoot, keyId, alg, sig }, rows:[{ url, finalUrl, ok, contentHash?, blockMerkleRoot?, failureCode?, rowLeaf, receipt? }], timestamp }. Params: urls (JSON array), backend_policy, session_profile.")]
    public async Task<string> Export(
        [Description("JSON array of HTTP/HTTPS URLs to export (1-20).")] string urls,
        [Description("Backend policy applied to every URL: http, browser, or http_then_browser.")] string backend_policy = "http_then_browser",
        [Description("Optional session profile id applied to every URL.")] string? session_profile = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(urls))
        {
            return Fail("invalid_arguments", "urls must be a non-empty JSON array of URL strings.");
        }

        if (!OccamBackendPolicyParser.TryParse(backend_policy, out var policy))
        {
            return Fail("invalid_arguments", "backend_policy must be http, browser, or http_then_browser.");
        }

        string[]? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize(urls, OccamDatasetJsonContext.Default.StringArray);
        }
        catch (JsonException ex)
        {
            return Fail("invalid_arguments", $"urls is not valid JSON: {ex.Message}");
        }

        var cleaned = parsed?
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u.Trim())
            .ToArray() ?? [];

        if (cleaned.Length == 0)
        {
            return Fail("invalid_arguments", "urls must be a non-empty JSON array of URL strings.");
        }

        if (cleaned.Length > MaxUrls)
        {
            return Fail("invalid_arguments", $"too many urls ({cleaned.Length}); max {MaxUrls} per export.");
        }

        var response = await datasetExportService.ExportAsync(cleaned, policy, session_profile, cancellationToken);
        return JsonSerializer.Serialize(response, OccamDatasetJsonContext.Default.OccamDatasetExportResponse);
    }

    private static string Fail(string code, string message) =>
        JsonSerializer.Serialize(
            new OccamDatasetExportFailureResponse(false, new OccamDatasetExportFailureInfo(code, message), DateTimeOffset.UtcNow.ToString("O")),
            OccamDatasetJsonContext.Default.OccamDatasetExportFailureResponse);
}
