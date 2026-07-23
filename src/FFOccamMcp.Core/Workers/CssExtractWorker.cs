using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Extract;

namespace OccamMcp.Core.Workers;

public sealed class CssExtractWorker(WorkerPaths paths)
{
    public CssExtractResult Extract(
        string url,
        FieldExtractionPlan plan,
        int timeoutMs = 45_000,
        bool browserFallback = false,
        string? headersFile = null)
    {
        var script = paths.CssExtractScript;
        if (string.IsNullOrWhiteSpace(script) || !File.Exists(script))
        {
            return CssExtractResult.Failure(url, "workers_unavailable", "CSS extract worker not configured.");
        }

        var fieldsFile = Path.Combine(Path.GetTempPath(), $"occam-fields-{Guid.NewGuid():N}.json");
        var started = Stopwatch.GetTimestamp();
        try
        {
            var wire = new CssExtractWireSpec
            {
                BaseSelector = plan.BaseSelector,
                Fields = plan.Fields.ToDictionary(
                    pair => pair.Key,
                    pair => new CssFieldWireSpec
                    {
                        Selector = pair.Value.Selector,
                        Attr = pair.Value.Attribute,
                        Multiple = pair.Value.Multiple,
                        Divide = pair.Value.Divide,
                    }),
            };
            var fieldsJson = JsonSerializer.Serialize(wire, CssExtractJsonContext.Default.CssExtractWireSpec);
            File.WriteAllText(fieldsFile, fieldsJson);

            var args = $"\"{script}\" \"{url}\" \"{fieldsFile}\"";
            if (!string.IsNullOrWhiteSpace(headersFile) && File.Exists(headersFile))
            {
                args += $" --headers-file=\"{headersFile}\"";
            }

            if (browserFallback)
            {
                args += " --browser-fallback";
            }

            var psi = new ProcessStartInfo
            {
                FileName = NodeRuntime.ResolveExecutable(),
                Arguments = NodeLaunchArguments.Build(browser: false, args),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            EgressProxyConfig.ApplyTo(psi);

            using var process = WorkerProcessGroup.Start(psi);
            if (process is null)
            {
                return CssExtractResult.Failure(url, "workers_unavailable", "Failed to start CSS extract worker.");
            }

            try
            {
                var capture = NodeWorkerOutputCapture.RunAsync(process, Math.Clamp(timeoutMs, 5000, 120_000)).GetAwaiter().GetResult();
                var latency = ElapsedMs(started);
                if (capture.TimedOut)
                {
                    return CssExtractResult.Failure(url, "timeout", "CSS extract worker timed out.", latency);
                }

                var jsonLine = NodeWorkerOutputCapture.TryParseLastJsonLine(capture.StdOut);
                if (jsonLine is null)
                {
                    return CssExtractResult.Failure(
                        url,
                        "extraction_failed",
                        SummarizeNoJsonFailure(capture.StdErr, capture.ExitCode),
                        latency);
                }

                var payload = JsonSerializer.Deserialize(jsonLine, CssExtractJsonContext.Default.CssExtractResponse);
                if (payload is null)
                {
                    return CssExtractResult.Failure(url, "extraction_failed", "CSS worker JSON parse failed.", latency);
                }

                if (!payload.Ok)
                {
                    return CssExtractResult.Failure(
                        url,
                        MapFailure(payload.Failure),
                        payload.Failure ?? "extraction_failed",
                        payload.LatencyMs > 0 ? payload.LatencyMs : latency,
                        payload.Backend);
                }

                return new CssExtractResult
                {
                    Ok = true,
                    Url = url,
                    Data = payload.Data,
                    LatencyMs = payload.LatencyMs > 0 ? payload.LatencyMs : latency,
                    HtmlBytes = payload.HtmlLength,
                    Backend = payload.Backend,
                };
            }
            finally
            {
                WorkerProcessGroup.Release(process);
            }
        }
        catch
        {
            return CssExtractResult.Failure(url, "extraction_failed", "CSS extract worker failed.");
        }
        finally
        {
            try
            {
                File.Delete(fieldsFile);
            }
            catch
            {
                // best-effort
            }
        }
    }

    private static int ElapsedMs(long started) =>
        (int)Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds);

    private static string MapFailure(string? failure) => failure?.ToLowerInvariant() switch
    {
        "timeout" or "aborterror" => "timeout",
        null or "" => "extraction_failed",
        _ when failure.StartsWith("http_", StringComparison.Ordinal) => failure,
        _ => "extraction_failed",
    };

    private static string SummarizeNoJsonFailure(string stderr, int exitCode)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return exitCode == 0 ? "no_json" : $"no_json:exit_{exitCode}";
        }

        var tail = stderr.Trim();
        if (tail.Length > 240)
        {
            tail = tail[^240..];
        }

        tail = tail.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return string.IsNullOrWhiteSpace(tail) ? "no_json" : $"no_json:{tail}";
    }
}

public sealed class CssExtractResult
{
    public required bool Ok { get; init; }
    public required string Url { get; init; }
    public JsonElement Data { get; init; }
    public int LatencyMs { get; init; }
    public int HtmlBytes { get; init; }
    public string? Backend { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureMessage { get; init; }

    public static CssExtractResult Failure(
        string url,
        string code,
        string message,
        int latencyMs = 0,
        string? backend = null) =>
        new()
        {
            Ok = false,
            Url = url,
            FailureCode = code,
            FailureMessage = message,
            LatencyMs = latencyMs,
            Backend = backend,
        };
}

internal sealed class CssExtractWireSpec
{
    public string? BaseSelector { get; init; }
    public required Dictionary<string, CssFieldWireSpec> Fields { get; init; }
}

internal sealed class CssFieldWireSpec
{
    public required string Selector { get; init; }
    public required string Attr { get; init; }
    public bool Multiple { get; init; }
    public int? Divide { get; init; }
}

internal sealed class CssExtractResponse
{
    public bool Ok { get; init; }
    public string? Failure { get; init; }
    public JsonElement Data { get; init; }
    public int LatencyMs { get; init; }
    [JsonPropertyName("html_length")]
    public int HtmlLength { get; init; }
    public string? Backend { get; init; }
}

[JsonSerializable(typeof(CssExtractWireSpec))]
[JsonSerializable(typeof(CssExtractResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class CssExtractJsonContext : JsonSerializerContext;
