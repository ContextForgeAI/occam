namespace OccamMcp.Core.Workers;

internal static class WorkerExtractPayloadMapper
{
    public static ExtractRunResult Map(WorkerExtractResponse payload)
    {
        if (!payload.Ok)
        {
            return new ExtractRunResult(
                false,
                payload.Markdown,
                payload.Backend,
                payload.Failure,
                payload.LatencyMs,
                payload.Url?.Final,
                IsTimeoutFailure(payload.Failure),
                payload.StatusCode,
                NetworkMs: payload.NetworkMs,
                ParseMs: payload.ParseMs,
                MediaRefs: MediaRefMapper.Map(payload.MediaRefs),
                Chunks: payload.Chunks,
                Blocks: payload.Blocks,
                Tables: payload.Tables,
                Feed: payload.Feed,
                Meta: payload.Meta,
                Screenshot: payload.Screenshot,
                Reason: payload.Reason,
                Fix: payload.Fix,
                // A provision-then-fail (chromium installed but the page still failed) must still report the
                // install, so carry it on the failure branch too — not just on success.
                BrowserProvisioned: payload.BrowserProvisioned,
                OverlayApplied: payload.OverlayApplied,
                Access: payload.Access);
        }

        return new ExtractRunResult(
            true,
            payload.Markdown,
            payload.Backend,
            payload.Failure,
            payload.LatencyMs,
            payload.Url?.Final,
            false,
            payload.StatusCode,
            NetworkMs: payload.NetworkMs,
            ParseMs: payload.ParseMs,
            MediaRefs: MediaRefMapper.Map(payload.MediaRefs),
            Chunks: payload.Chunks,
            Blocks: payload.Blocks,
            Tables: payload.Tables,
            Feed: payload.Feed,
            Meta: payload.Meta,
            Screenshot: payload.Screenshot,
            BrowserProvisioned: payload.BrowserProvisioned,
            OverlayApplied: payload.OverlayApplied,
            Access: payload.Access);
    }

    private static bool IsTimeoutFailure(string? failure) =>
        failure is "timeout" or "aborterror"
        || (failure?.Contains("timeout", StringComparison.OrdinalIgnoreCase) ?? false);
}
