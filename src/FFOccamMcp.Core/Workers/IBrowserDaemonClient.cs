using System.Text;
using System.Threading.Tasks;
using OccamMcp.Core.Abstractions;

namespace OccamMcp.Core.Workers;

/// <summary>Client for communicating with browser daemon processes.</summary>
public interface IBrowserDaemonClient
{
    Task<bool> IsHealthyAsync(int port, CancellationToken cancellationToken);

    Task<ExtractRunResult?> TryExtractAsync(
        string url,
        int timeoutMs,
        bool forceRecycle,
        string? headersFile,
        string? storageStateFile,
        CancellationToken cancellationToken,
        int port = 0,
        string? features = null,
        string? playbookOverlayJson = null,
        bool playbookOverlayStrict = false);

    Task<string?> TryCaptureSkeletonJsonAsync(
        string url,
        int maxNodes,
        int timeoutMs,
        string? headersFile,
        CancellationToken cancellationToken,
        int port = 0);
}