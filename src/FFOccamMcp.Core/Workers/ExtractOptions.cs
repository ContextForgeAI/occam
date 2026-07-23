namespace OccamMcp.Core.Workers;

/// <summary>Explicit options for extract operations, replacing AsyncLocal scopes.</summary>
public record ExtractOptions(
    string Url,
    string? HeadersFile = null,
    string? StorageStateFile = null,
    bool ForceRecycle = false,
    string? OversizeMode = null,
    string? PlaybookOverlayPath = null,
    bool PlaybookOverlayStrict = true,
    // A3: the resolved genome JSON, sent inline to the browser daemon /extract (no temp file across the
    // process boundary). The one-shot path still uses PlaybookOverlayPath (--playbook-overlay CLI arg).
    string? PlaybookOverlayJson = null,
    string? Features = null);