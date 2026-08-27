using System.Text.Json;

namespace OccamMcp.Core.BrowserActions;

public sealed record BrowserInteractWorkerResult(
    bool Ok,
    string? Markdown,
    string? Backend,
    string? Failure,
    string? Message,
    string? FinalUrl,
    int LatencyMs,
    int StepsRun,
    int? FailedIndex,
    JsonElement? ActionTrace = null)
{
    public static BrowserInteractWorkerResult FromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var ok = root.TryGetProperty("ok", out var okEl) && okEl.ValueKind == JsonValueKind.True;
            var markdown = root.TryGetProperty("markdown", out var md) && md.ValueKind == JsonValueKind.String
                ? md.GetString()
                : null;
            var backend = root.TryGetProperty("backend", out var be) && be.ValueKind == JsonValueKind.String
                ? be.GetString()
                : null;
            var failure = root.TryGetProperty("failure", out var f) && f.ValueKind == JsonValueKind.String
                ? f.GetString()
                : null;
            var message = root.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String
                ? msg.GetString()
                : null;
            string? finalUrl = null;
            if (root.TryGetProperty("url", out var urlEl) && urlEl.ValueKind == JsonValueKind.Object
                && urlEl.TryGetProperty("final", out var fin) && fin.ValueKind == JsonValueKind.String)
            {
                finalUrl = fin.GetString();
            }

            var latency = root.TryGetProperty("latency_ms", out var lat) && lat.ValueKind == JsonValueKind.Number
                ? lat.GetInt32()
                : 0;
            var steps = root.TryGetProperty("steps_run", out var st) && st.ValueKind == JsonValueKind.Number
                ? st.GetInt32()
                : root.TryGetProperty("interaction_steps_run", out var st2) && st2.ValueKind == JsonValueKind.Number
                    ? st2.GetInt32()
                    : 0;
            int? failedIndex = root.TryGetProperty("failed_index", out var fi) && fi.ValueKind == JsonValueKind.Number
                ? fi.GetInt32()
                : null;
            JsonElement? trace = root.TryGetProperty("action_trace", out var tr)
                ? tr.Clone()
                : null;
            return new BrowserInteractWorkerResult(
                ok, markdown, backend, failure, message, finalUrl, latency, steps, failedIndex, trace);
        }
        catch
        {
            return new BrowserInteractWorkerResult(
                false, null, null, "extraction_failed", "invalid worker JSON", null, 0, 0, null);
        }
    }
}
