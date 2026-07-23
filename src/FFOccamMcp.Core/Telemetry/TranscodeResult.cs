namespace OccamMcp.Core.Telemetry;

/// <summary>Thin transcode outcome for Occam stderr profiler (L0 — no cache/playbooks).</summary>
public sealed record TranscodeResult(
    bool Ok,
    string Url,
    string Text,
    int HtmlBytes,
    int LatencyMs,
    string Backend);
