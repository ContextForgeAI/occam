using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using OccamMcp.Core.Abstractions;
using OccamMcp.Core.Configuration;

namespace OccamMcp.Core.Workers;

/// <summary>Request payload for browser daemon extract endpoint.</summary>
internal sealed record BrowserDaemonExtractRequest
{
    public required string Url { get; init; }
    public bool LeanAssets { get; init; }
    public bool ForceRecycle { get; init; }
    public string? HeadersFile { get; init; }
    public string? StorageStateFile { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("features")]
    public string? Features { get; init; }
    public int TimeoutMs { get; init; }

    // A3: the resolved genome sent inline (serializes as playbook_overlay_json / playbook_overlay_strict)
    // so the warm daemon applies the overlay without a temp file crossing the process boundary.
    public string? PlaybookOverlayJson { get; init; }
    public bool PlaybookOverlayStrict { get; init; }
}

/// <summary>Request payload for browser daemon skeleton endpoint.</summary>
internal sealed record BrowserDaemonSkeletonRequest
{
    public required string Url { get; init; }
    public int MaxNodes { get; init; }
    public string? HeadersFile { get; init; }
}

/// <summary>JSON serialization context for browser daemon requests.</summary>
// The browser daemon (browser-daemon.mjs) reads snake_case keys (url, lean_assets,
// force_recycle, headers_file, storage_state_file, max_nodes). Without this policy the
// PascalCase C# properties serialized as "Url"/"LeanAssets"/… and the daemon silently
// dropped every field → "missing_url". Keep in lockstep with the daemon's body.* reads.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(BrowserDaemonExtractRequest))]
[JsonSerializable(typeof(BrowserDaemonSkeletonRequest))]
internal partial class BrowserDaemonJsonContext : JsonSerializerContext
{
}

/// <summary>Client for communicating with browser daemon processes.</summary>
public sealed class BrowserDaemonClient : IBrowserDaemonClient
{
    private readonly HttpClient _httpClient;
    private readonly BrowserPoolSettings _settings;

    // P1-6: Configurable timeout instead of InfiniteTimeSpan to prevent hangs
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(3);

    public BrowserDaemonClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = DefaultTimeout };
        _settings = BrowserPoolSettings.ReadFromEnvironment();
    }

    public async Task<bool> IsHealthyAsync(int port, CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            var response = await _httpClient.GetAsync($"http://127.0.0.1:{port}/health", cts.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ExtractRunResult?> TryExtractAsync(
        string url,
        int timeoutMs,
        bool forceRecycle,
        string? headersFile,
        string? storageStateFile,
        CancellationToken cancellationToken,
        int port = 0,
        string? features = null,
        string? playbookOverlayJson = null,
        bool playbookOverlayStrict = false)
    {
        cancellationToken.ThrowIfCancellationRequested();

        port = port > 0 ? port : BrowserDaemonHost.Port;
        if (!await IsHealthyAsync(port, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            var request = new BrowserDaemonExtractRequest
            {
                Url = url,
                LeanAssets = true,
                ForceRecycle = forceRecycle,
                HeadersFile = headersFile,
                StorageStateFile = storageStateFile,
                Features = features,
                TimeoutMs = timeoutMs,
                PlaybookOverlayJson = playbookOverlayJson,
                PlaybookOverlayStrict = playbookOverlayStrict,
            };

            var json = JsonSerializer.Serialize(request, BrowserDaemonJsonContext.Default.BrowserDaemonExtractRequest);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync($"http://127.0.0.1:{port}/extract", content, cts.Token).ConfigureAwait(false);

            using var stream = response.Content.ReadAsStream(cts.Token);
            var payload = await JsonSerializer.DeserializeAsync(stream, WorkerExtractJsonContext.Default.WorkerExtractResponse, cts.Token).ConfigureAwait(false);
            if (payload is null)
            {
                return null;
            }

            MarkPortActivity(port);
            return WorkerExtractPayloadMapper.Map(payload);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ExtractRunResult(false, null, null, "timeout", timeoutMs, null, true);
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> TryCaptureSkeletonJsonAsync(
        string url,
        int maxNodes,
        int timeoutMs,
        string? headersFile,
        CancellationToken cancellationToken,
        int port = 0)
    {
        cancellationToken.ThrowIfCancellationRequested();

        port = port > 0 ? port : BrowserDaemonHost.Port;
        if (!await IsHealthyAsync(port, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            var request = new BrowserDaemonSkeletonRequest
            {
                Url = url,
                MaxNodes = maxNodes,
                HeadersFile = headersFile,
            };

            var json = JsonSerializer.Serialize(request, BrowserDaemonJsonContext.Default.BrowserDaemonSkeletonRequest);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync($"http://127.0.0.1:{port}/skeleton", content, cts.Token).ConfigureAwait(false);

            MarkPortActivity(port);
            return await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private void MarkPortActivity(int port)
    {
        for (var slotId = 0; slotId < _settings.PoolSize; slotId++)
        {
            if (_settings.ResolvePortForSlot(slotId) == port)
            {
                BrowserPoolManager.Shared.MarkActivity(new BrowserPoolSlot(slotId, port, DateTime.UtcNow.Ticks));
                return;
            }
        }
    }
}