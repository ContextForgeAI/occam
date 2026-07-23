using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Playbooks;

public sealed class PlaybookHealService(WorkerPaths workerPaths, IBrowserPoolManager browserPool, IBrowserDaemonClient browserDaemonClient)
{
    private readonly DomSkeletonWorker _skeletonWorker = new(workerPaths, browserPool, browserDaemonClient);

    public async Task<PlaybookHealResult> HealAsync(PlaybookHealRequest request)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out _))
        {
            return Fail(request, "invalid_url", "URL is not absolute.");
        }

        if (string.IsNullOrWhiteSpace(request.FailureReason))
        {
            return Fail(request, "invalid_failure_reason", "failure_reason is required.");
        }

        if (PlaybookHealPolicy.IsTerminalFailure(request.FailureReason))
        {
            return Fail(
                request,
                PlaybookHealPolicy.NormalizeFailureReason(request.FailureReason),
                "Failure is terminal for self-heal; escalate to user.");
        }

        var sessionApplied = !string.IsNullOrWhiteSpace(request.SessionProfile);
        if (!PlaybookHealPolicy.ShouldOfferHeal(
                request.FailureReason,
                sessionProfileApplied: sessionApplied,
                requestUrl: request.Url))
        {
            return Fail(
                request,
                "heal_not_applicable",
                $"failure_reason '{request.FailureReason}' is not in heal_set; fix URL or policy first.");
        }

        if (!workerPaths.IsConfigured)
        {
            return Fail(request, "workers_unavailable", "Occam workers are not installed.");
        }

        var captured = await _skeletonWorker.TryCaptureAsync(request);
        if (captured is null)
        {
            return Fail(
                request,
                "workers_unavailable",
                "DOM skeleton script not found; set OCCAM_HOME or OCCAM_DOM_SKELETON_SCRIPT.");
        }

        return captured;
    }

    private static PlaybookHealResult Fail(PlaybookHealRequest request, string code, string message) =>
        new(false, request.Url, request.FailureReason, null, null, null, code, message, 0);
}
