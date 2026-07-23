using System.ComponentModel;
using System.Text.Json;
using OccamMcp.Core.Batch;
using ModelContextProtocol.Server;

namespace OccamMcp.Core.Tools;

/// <summary>
/// Thin MCP front to the in-process batch engine (fire-and-forget async transcode). Opt-in: the
/// host must set <c>OCCAM_BATCH_MCP=1</c>, which registers the job store + background processor and
/// these three tools. Submit returns a <c>job_id</c> immediately; poll status, then page results.
/// </summary>
internal static class OccamBatchToolSupport
{
    public static string SerializeError(string code, string message) =>
        JsonSerializer.Serialize(
            new BatchErrorResponse(new BatchFailureInfo(code, message)),
            BatchJsonContext.Default.BatchErrorResponse);

    /// <summary>Parses a URL list given as a JSON array or a newline/comma/semicolon/space-delimited string.</summary>
    public static string[] ParseUrls(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var trimmed = raw.Trim();
        if (trimmed.StartsWith('['))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<string>();
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        var s = el.ValueKind == JsonValueKind.String ? el.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            list.Add(s.Trim());
                        }
                    }
                    return [.. list];
                }
            }
            catch (JsonException)
            {
                // Fall through to delimiter splitting.
            }
        }

        return trimmed
            .Split(['\n', '\r', ',', ';', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

[McpServerToolType]
public sealed class OccamBatchSubmitTool(IBatchJobService batch)
{
    [McpServerTool(Name = "occam_batch_submit"), Description("Submit a fire-and-forget batch of URLs for asynchronous transcode. Returns job_id immediately (state queued); poll occam_batch_status, then page occam_batch_results. Opt-in — host must set OCCAM_BATCH_MCP=1.")]
    public string Submit(
        [Description("URLs to transcode: JSON array or newline/comma/semicolon-separated list.")] string urls,
        [Description("Backend policy: http, browser, or http_then_browser.")] string backend_policy = "http_then_browser",
        [Description("Optional focus keywords for the per-URL fit_markdown prune.")] string? focus_query = null,
        [Description("Optional per-URL output token budget (minimum 128).")] int? max_tokens = null,
        [Description("BM25-style paragraph prune per URL. Default true.")] bool fit_markdown = true,
        [Description("Optional session profile id applied to every URL in the batch.")] string? session_profile = null,
        [Description("Playbook merge policy: off or auto. Default auto.")] string playbook_policy = "auto",
        [Description("Optional idempotency key — re-submitting the same key within 24h returns the existing job instead of a new one.")] string? idempotency_key = null,
        [Description("HTTP oversize handling: fail (default) or partial.")] string on_oversize = "fail")
    {
        var request = new BatchSubmitRequest
        {
            Urls = OccamBatchToolSupport.ParseUrls(urls),
            BackendPolicy = backend_policy,
            FocusQuery = focus_query,
            MaxTokens = max_tokens,
            FitMarkdown = fit_markdown,
            SessionProfile = session_profile,
            PlaybookPolicy = playbook_policy,
            IdempotencyKey = idempotency_key,
            OnOversize = on_oversize,
        };

        var (response, error) = batch.Submit(request);
        return error is not null
            ? OccamBatchToolSupport.SerializeError(error.Code, error.Message)
            : JsonSerializer.Serialize(response!, BatchJsonContext.Default.BatchSubmitResponse);
    }
}

[McpServerToolType]
public sealed class OccamBatchStatusTool(IBatchJobService batch)
{
    [McpServerTool(Name = "occam_batch_status"), Description("Poll a batch job's state (queued/running/done/failed) and progress counts. Opt-in — host must set OCCAM_BATCH_MCP=1.")]
    public string Status(
        [Description("Job id returned by occam_batch_submit.")] string job_id)
    {
        var (response, error) = batch.GetStatus(job_id);
        return error is not null
            ? OccamBatchToolSupport.SerializeError(error.Code, error.Message)
            : JsonSerializer.Serialize(response!, BatchJsonContext.Default.BatchStatusResponse);
    }
}

[McpServerToolType]
public sealed class OccamBatchResultsTool(IBatchJobService batch)
{
    [McpServerTool(Name = "occam_batch_results"), Description("Page completed results for a batch job. Returns items[] and next_cursor (null when exhausted). Opt-in — host must set OCCAM_BATCH_MCP=1.")]
    public string Results(
        [Description("Job id returned by occam_batch_submit.")] string job_id,
        [Description("Pagination cursor (0 to start; pass the prior next_cursor to continue).")] int cursor = 0,
        [Description("Max items to return (1–200, default 50).")] int limit = 50)
    {
        var (response, error) = batch.GetResults(job_id, cursor, limit);
        return error is not null
            ? OccamBatchToolSupport.SerializeError(error.Code, error.Message)
            : JsonSerializer.Serialize(response!, BatchJsonContext.Default.BatchResultsResponse);
    }
}
