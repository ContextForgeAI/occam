using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Configuration;
using OccamMcp.Core.Playbooks;
using OccamMcp.Core.Session;

namespace OccamMcp.Core.Workers;

public sealed class DomSkeletonWorker(WorkerPaths paths, IBrowserPoolManager browserPool, IBrowserDaemonClient browserDaemonClient)
{
    public async Task<PlaybookHealResult?> TryCaptureAsync(PlaybookHealRequest request)
    {
        if (!paths.IsConfigured)
        {
            return null;
        }

        var preflight = FetchPreflight.Prepare(request.Url, request.SessionProfile);
        if (!preflight.Ok)
        {
            return null;
        }

        var headersFile = preflight.ActiveHeadersFile
            ?? OccamEnvironment.Get("OCCAM_REQUEST_HEADERS_FILE");

        if (browserPool.IsEnabled && await browserPool.TryEnsureMinimumHealthyAsync(paths))
        {
            BrowserPoolSlot? slot = null;
            try
            {
                var maxNodes = Math.Clamp(request.MaxSkeletonNodes, 50, 600);
                slot = await browserPool.AcquireSlotAsync(CancellationToken.None);
                var daemonJson = await browserDaemonClient.TryCaptureSkeletonJsonAsync(
                    request.Url,
                    maxNodes,
                    120_000,
                    headersFile,
                    CancellationToken.None,
                    slot.Port);

                if (!string.IsNullOrWhiteSpace(daemonJson))
                {
                    var jsonLine = NodeWorkerOutputCapture.TryParseLastJsonLine(daemonJson) ?? daemonJson.Trim();
                    var parsed = ParseWorkerJsonLine(jsonLine, request);
                    if (parsed is { Ok: true })
                    {
                        browserPool.ReleaseSlot(slot, ok: true, extractMs: parsed.LatencyMs);
                        slot = null;
                        return parsed;
                    }
                }
            }
            finally
            {
                if (slot is not null)
                {
                    browserPool.ReleaseSlot(slot, ok: false, extractMs: 0);
                }
            }
        }

        return TryCaptureOneShot(request, headersFile);
    }

    private PlaybookHealResult? TryCaptureOneShot(PlaybookHealRequest request, string? headersFile)
    {
        var script = ResolveScript(paths);
        if (script is null)
        {
            return null;
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            var maxNodes = Math.Clamp(request.MaxSkeletonNodes, 50, 600);
            var args = $"\"{script}\" \"{request.Url}\" --mode=skeleton --max-nodes={maxNodes}";
            if (!string.IsNullOrWhiteSpace(headersFile) && File.Exists(headersFile))
            {
                args += $" --headers-file=\"{headersFile}\"";
            }

            var psi = new ProcessStartInfo
            {
                FileName = NodeRuntime.ResolveExecutable(),
                Arguments = NodeLaunchArguments.Build(browser: true, args),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            PlaywrightEnvironment.ApplyTo(psi);
            EgressProxyConfig.ApplyTo(psi);

            using var process = WorkerProcessGroup.Start(psi);
            if (process is null)
            {
                return Fail(request, "workers_unavailable", "Failed to start DOM skeleton worker process.", 0);
            }

            try
            {
                var timeoutMs = 120_000;
                var capture = NodeWorkerOutputCapture.RunAsync(process, timeoutMs).GetAwaiter().GetResult();
                var latency = (int)((Stopwatch.GetTimestamp() - started) * 1000 / Stopwatch.Frequency);

                if (capture.TimedOut)
                {
                    return Fail(request, "timeout", $"DOM skeleton capture exceeded {timeoutMs}ms.", latency);
                }

                var jsonLine = NodeWorkerOutputCapture.TryParseLastJsonLine(capture.StdOut);
                if (jsonLine is null)
                {
                    return Fail(
                        request,
                        "extraction_failed",
                        TrimWorkerMessage(capture.StdErr, capture.StdOut) ?? "DOM skeleton worker returned no JSON payload.",
                        latency);
                }

                return ParseWorkerJsonLine(jsonLine, request, latency);
            }
            finally
            {
                WorkerProcessGroup.Release(process);
            }
        }
        catch (Exception ex)
        {
            var latency = (int)((Stopwatch.GetTimestamp() - started) * 1000 / Stopwatch.Frequency);
            return Fail(request, "extraction_failed", ex.Message, latency);
        }
    }

    internal static PlaybookHealResult? ParseWorkerJsonLine(string jsonLine, PlaybookHealRequest request, int latencyMs = 0)
    {
        var payload = JsonSerializer.Deserialize(jsonLine, DomSkeletonWorkerJsonContext.Default.DomSkeletonWorkerResponse);
        if (payload is null || !payload.Ok || payload.Skeleton is null)
        {
            return new PlaybookHealResult(
                false,
                request.Url,
                request.FailureReason,
                null,
                null,
                null,
                payload?.FailureCode ?? "extraction_failed",
                payload?.Message ?? "DOM skeleton worker returned no payload.",
                latencyMs);
        }

        return MapSuccess(request, payload, latencyMs);
    }

    private static string? ResolveScript(WorkerPaths paths)
    {
        if (!string.IsNullOrWhiteSpace(paths.DomSkeletonScript) && File.Exists(paths.DomSkeletonScript))
        {
            return paths.DomSkeletonScript;
        }

        var env = OccamEnvironment.Get("OCCAM_DOM_SKELETON_SCRIPT");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
        {
            return env;
        }

        if (WorkerPaths.TryGetRepoRoot() is { } root)
        {
            var candidate = Path.Combine(root, "workers", "browser-extract", "dom-skeleton-capture.mjs");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static FetchHeadersScope? CreateHeadersScope(string? sessionProfile)
    {
        if (string.IsNullOrWhiteSpace(sessionProfile))
        {
            return null;
        }

        var session = SessionProfileHeaders.Resolve(sessionProfile);
        if (session.Status != SessionProfileStatus.Ok || session.Headers.Count == 0)
        {
            return null;
        }

        return FetchHeadersScope.Create(session.Headers);
    }

    private static PlaybookHealResult Fail(PlaybookHealRequest request, string code, string message, int latencyMs) =>
        new(false, request.Url, request.FailureReason, null, null, null, code, message, latencyMs);

    private static string? TrimWorkerMessage(string stderr, string stdout)
    {
        var line = (stderr + "\n" + stdout)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(l => l.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase)
                || l.Contains("playwright install", StringComparison.OrdinalIgnoreCase)
                || l.Contains("Error", StringComparison.OrdinalIgnoreCase));

        if (line is null)
        {
            return null;
        }

        return line.Length > 240 ? line[..240] : line;
    }

    private static PlaybookHealResult MapSuccess(
        PlaybookHealRequest request,
        DomSkeletonWorkerResponse payload,
        int latencyMs)
    {
        var sk = payload.Skeleton!;
        var root = MapNode(sk.Root);
        var anchors = new PlaybookHealAnchors(
            payload.Anchors?.Landmarks ?? [],
            payload.Anchors?.DataTestIds ?? [],
            (payload.Anchors?.MainCandidates ?? [])
                .Select(c => new MainCandidateAnchor(c.Selector, c.TextAnchor, c.Score))
                .ToList());

        var hints = new PlaybookHealAgentHints(
            "occam_playbook_save",
            [
                "compare_skeleton_to_broken_selectors",
                "dump_raw_html",
                "retry_transcode_before_save",
                $"max_verify_retries={PlaybookHealPolicy.MaxVerifyRetries}",
            ]);

        return new PlaybookHealResult(
            true,
            request.Url,
            request.FailureReason,
            new DomSkeletonPayload(root, new DomSkeletonStats(sk.Stats.NodeCount, sk.Stats.MaxDepth, sk.Stats.InteractiveCount)),
            anchors,
            hints,
            null,
            null,
            latencyMs);
    }

    private static DomSkeletonNode MapNode(DomSkeletonWorkerNode node) =>
        new(
            node.Tag,
            node.Id,
            node.Class,
            node.Role,
            node.TestId,
            node.Aria,
            node.Text,
            node.Interactive,
            node.Children?.Select(MapNode).ToList());
}

internal sealed class DomSkeletonWorkerResponse
{
    public bool Ok { get; init; }
    public string? FailureCode { get; init; }
    public string? Message { get; init; }
    public DomSkeletonWorkerSkeleton? Skeleton { get; init; }
    public DomSkeletonWorkerAnchors? Anchors { get; init; }
}

internal sealed class DomSkeletonWorkerSkeleton
{
    public DomSkeletonWorkerNode Root { get; init; } = new() { Tag = "body" };
    public DomSkeletonWorkerStats Stats { get; init; } = new();
}

internal sealed class DomSkeletonWorkerStats
{
    public int NodeCount { get; init; }
    public int MaxDepth { get; init; }
    public int InteractiveCount { get; init; }
}

internal sealed class DomSkeletonWorkerNode
{
    public string Tag { get; init; } = "div";
    public string? Id { get; init; }
    public List<string>? Class { get; init; }
    public string? Role { get; init; }

    [JsonPropertyName("testId")]
    public string? TestId { get; init; }

    public string? Aria { get; init; }
    public string? Text { get; init; }
    public bool Interactive { get; init; }
    public List<DomSkeletonWorkerNode>? Children { get; init; }
}

internal sealed class DomSkeletonWorkerAnchors
{
    public List<string> Landmarks { get; init; } = [];
    public List<string> DataTestIds { get; init; } = [];
    public List<DomSkeletonWorkerMainCandidate> MainCandidates { get; init; } = [];
}

internal sealed class DomSkeletonWorkerMainCandidate
{
    public string Selector { get; init; } = "";
    public string? TextAnchor { get; init; }
    public double Score { get; init; }
}

[JsonSerializable(typeof(DomSkeletonWorkerResponse))]
[JsonSerializable(typeof(DomSkeletonWorkerSkeleton))]
[JsonSerializable(typeof(DomSkeletonWorkerStats))]
[JsonSerializable(typeof(DomSkeletonWorkerNode))]
[JsonSerializable(typeof(DomSkeletonWorkerAnchors))]
[JsonSerializable(typeof(DomSkeletonWorkerMainCandidate))]
[JsonSerializable(typeof(List<DomSkeletonWorkerNode>))]
[JsonSerializable(typeof(List<string>))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
internal partial class DomSkeletonWorkerJsonContext : JsonSerializerContext;
