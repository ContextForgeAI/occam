namespace OccamMcp.Core.Receipts;

/// <summary>
/// The single gate for whether Receipt v1 signing is active: on by default, disabled by
/// <c>OCCAM_RECEIPTS=off|0|false</c>. Centralized so the transcode / digest / claim-check / dataset
/// paths share one definition instead of re-implementing the check.
/// </summary>
public static class ReceiptsPolicy
{
    public static bool Enabled()
    {
        var v = Environment.GetEnvironmentVariable("OCCAM_RECEIPTS");
        return v is null
            || !(v.Equals("off", StringComparison.OrdinalIgnoreCase)
                 || v == "0"
                 || v.Equals("false", StringComparison.OrdinalIgnoreCase));
    }
}
