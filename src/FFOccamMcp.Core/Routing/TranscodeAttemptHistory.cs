namespace OccamMcp.Core.Routing;

/// <summary>
/// Browser exhaustion is a property of attempt history, not of the winning fallback backend.
/// A later timeout must not revive a retry-browser hint after the browser already ran.
/// </summary>
public static class TranscodeAttemptHistory
{
    public static bool IsBrowserBackend(string? backend) =>
        backend?.Contains("browser", StringComparison.OrdinalIgnoreCase) == true
        || backend?.Contains("playwright", StringComparison.OrdinalIgnoreCase) == true;

    public static bool BrowserWasAttempted(
        string? winningBackend,
        IEnumerable<string?>? recoveryBackends = null)
    {
        if (IsBrowserBackend(winningBackend))
        {
            return true;
        }

        if (recoveryBackends is null)
        {
            return false;
        }

        foreach (var backend in recoveryBackends)
        {
            if (IsBrowserBackend(backend))
            {
                return true;
            }
        }

        return false;
    }

    public static bool SuppressExtractRetry(string failureCode, bool browserWasAttempted) =>
        browserWasAttempted && failureCode is "thin_extract" or "render_error";
}
