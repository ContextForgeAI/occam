using OccamMcp.Core.External;
using OccamMcp.Core.Workers;
using System.Text.Json;

namespace OccamMcp.Core.Backends.Managed;

/// <summary>
/// Optional Donsetch acquire adapter (<c>OCCAM_MANAGED_PROVIDER=donsetch</c>).
/// Spawns local <c>donsetch fetch URL --json</c>; Occam still owns materialization/trust.
/// Operator must install Donsetch separately (AGPL); never bundled.
/// </summary>
public sealed class DonsetchManagedProvider : IManagedProvider
{
    public string Name => "donsetch";
    public bool RequiresApiKey => false;

    public ExtractRunResult Fetch(HttpClient client, string url, string? apiKey, string? baseUrl, CancellationToken cancellationToken)
    {
        _ = client;
        _ = apiKey;
        _ = baseUrl;
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var bin = ExternalCli.ResolveBinary("OCCAM_DONSETCH_PATH", "donsetch");
        if (bin is null)
        {
            return new ExtractRunResult(
                false, null, ManagedResults.BackendName(Name), "managed_error",
                ManagedElapsed.Ms(started), url, false);
        }

        var timeoutMs = OccamMcp.Core.Configuration.OccamEnvironment.GetInt(
            "OCCAM_MANAGED_TIMEOUT_MS", defaultValue: 60_000, min: 1_000, max: 180_000);
        try
        {
            var (exit, stdout, _) = ExternalCli.RunAsync(
                bin,
                ["fetch", url, "--json", "--quiet"],
                timeoutMs,
                cancellationToken).GetAwaiter().GetResult();

            if (exit == -2)
            {
                return new ExtractRunResult(
                    false, null, ManagedResults.BackendName(Name), "timeout",
                    ManagedElapsed.Ms(started), url, true);
            }

            if (exit != 0)
            {
                return new ExtractRunResult(
                    false, null, ManagedResults.BackendName(Name), "managed_error",
                    ManagedElapsed.Ms(started), url, false);
            }

            var markdown = ExtractMarkdown(stdout);
            return ManagedResults.FromMarkdown(Name, markdown, url, ManagedElapsed.Ms(started));
        }
        catch (Exception ex)
        {
            return ManagedResults.Exception(Name, ex, ManagedElapsed.Ms(started));
        }
    }

    internal static string? ExtractMarkdown(string stdout)
    {
        var trimmed = stdout.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.StartsWith('{'))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(trimmed);
                var root = doc.RootElement;
                foreach (var key in new[] { "markdown", "content", "text", "body" })
                {
                    if (root.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String)
                    {
                        return p.GetString();
                    }
                }

                if (root.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Object)
                {
                    foreach (var key in new[] { "markdown", "content", "text" })
                    {
                        if (result.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String)
                        {
                            return p.GetString();
                        }
                    }
                }
            }
            catch (System.Text.Json.JsonException)
            {
                return trimmed;
            }
        }

        return trimmed;
    }
}
