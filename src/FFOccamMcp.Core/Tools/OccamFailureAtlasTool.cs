using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using OccamMcp.Core.Telemetry;

namespace OccamMcp.Core.Tools;

/// <summary>
/// SI-10 — failure atlas: read the running host's accumulated per-host outcome map. Opt-in
/// (<c>OCCAM_ATLAS_MCP=1</c>). Returns a closure map — which hosts are provably walled (captcha / login /
/// 4xx, so retrying is wasted) vs which merely had transient failures — so an agent planning a crawl can
/// skip the dead ones. In-memory over the current run; not persisted.
/// </summary>
[McpServerToolType]
public sealed class OccamFailureAtlasTool(FailureAtlasStore store)
{
    [McpServerTool(Name = "occam_failure_atlas"), Description("Read the running host's per-host failure atlas (opt-in, OCCAM_ATLAS_MCP=1). Returns { ok, hostCount, walledCount, hosts:[{ host, attempts, successes, failures, closureRate, walled, dominantFailure, byCode:[{code,count}], lastFailureAt }] } worst-first. 'walled' = never succeeded and the dominant failure is an honest closure (captcha/login/4xx) — retrying is wasted. In-memory over the current run; not persisted. Param: only_walled (default false).")]
    public string Read(
        [Description("Return only hosts classified as walled (provable dead ends). Default false.")] bool only_walled = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = store.Snapshot();
        var hosts = (only_walled ? snapshot.Where(h => h.Walled) : snapshot).ToArray();
        var response = new OccamFailureAtlasResponse(
            Ok: true,
            HostCount: snapshot.Count,
            WalledCount: snapshot.Count(h => h.Walled),
            Hosts: hosts,
            Timestamp: DateTimeOffset.UtcNow.ToString("O"));

        return JsonSerializer.Serialize(response, OccamFailureAtlasJsonContext.Default.OccamFailureAtlasResponse);
    }
}

public sealed record OccamFailureAtlasResponse(
    bool Ok,
    int HostCount,
    int WalledCount,
    FailureAtlasHostSummary[] Hosts,
    string Timestamp);

[JsonSerializable(typeof(OccamFailureAtlasResponse))]
[JsonSerializable(typeof(FailureAtlasHostSummary))]
[JsonSerializable(typeof(FailureAtlasHostSummary[]))]
[JsonSerializable(typeof(FailureCodeCount))]
[JsonSerializable(typeof(FailureCodeCount[]))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class OccamFailureAtlasJsonContext : JsonSerializerContext;
