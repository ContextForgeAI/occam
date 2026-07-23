namespace OccamMcp.Core.Telemetry;

/// <summary>
/// Telemetry-facing alias for the single canonical estimator in
/// <see cref="OccamMcp.Core.Compile.TokenEstimator"/>. Kept as a thin forwarder so the telemetry
/// namespace has a local name without a second (previously divergent) implementation — both used to
/// be a flat chars/4 and drifted independently; now there is one script-aware source of truth.
/// </summary>
public static class TokenEstimator
{
    public const int ApproxCharsPerToken = Compile.TokenEstimator.ApproxCharsPerToken;

    public const string EstimatorId = Compile.TokenEstimator.EstimatorId;

    public static int Estimate(string? text) => Compile.TokenEstimator.Estimate(text);

    public static int EstimateFromByteCount(int byteCount) =>
        Compile.TokenEstimator.EstimateFromByteCount(byteCount);
}
