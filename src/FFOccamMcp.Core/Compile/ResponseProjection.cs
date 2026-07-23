using OccamMcp.Core.Knowledge;

namespace OccamMcp.Core.Compile;

/// <summary>Builds the exact public sidecar inventory before whole-response allocation.</summary>
public static class ResponseProjection
{
    public static ResponseBudgetSidecars Project(
        ResponseBudgetSidecars raw,
        MaterializationRequest request)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(request);

        return new ResponseBudgetSidecars(
            request.ExposePublicBlocks ? raw.Blocks : null,
            request.ExposePublicTables ? raw.Tables : null,
            request.ExposePublicChunks ? raw.Chunks : null,
            request.ExposePublicMedia ? raw.MediaRefs : null,
            request.ExposePublicFeed ? raw.Feed : null,
            request.ExposePublicScreenshot ? raw.Screenshot : null,
            raw.ExpectReceipt,
            IsProjected: true);
    }

    public static ResponseBudgetSidecars Empty(bool expectReceipt = true) =>
        new(null, null, null, null, null, null, expectReceipt, IsProjected: true);
}
