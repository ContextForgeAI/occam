using OccamMcp.Core.Playbooks;
using OccamMcp.Core.Receipts;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Tools;

namespace OccamMcp.Core.Dataset;

public interface IDatasetExportService
{
    ValueTask<OccamDatasetExportResponse> ExportAsync(
        IReadOnlyList<string> urls,
        OccamBackendPolicy policy,
        string? sessionProfile,
        CancellationToken cancellationToken);
}

/// <summary>
/// SI-17: export a set of extractions as a signed, auditable dataset. Each URL is transcoded (with
/// <c>json_blocks</c> so the row carries a block-Merkle root) and gets its own signed receipt; the whole
/// set is then bound by a single manifest signature over the Merkle root of the per-row leaves. The
/// result is verifiable two ways: each row via <c>occam_verify</c>, and the set via
/// <see cref="DatasetManifestBuilder.Verify"/>. Reuses the transcode pipeline + Receipt v1 machinery;
/// this layer only assembles rows and signs the manifest.
/// </summary>
public sealed class DatasetExportService(TranscodePipeline pipeline, ReceiptSigner signer) : IDatasetExportService
{
    public async ValueTask<OccamDatasetExportResponse> ExportAsync(
        IReadOnlyList<string> urls,
        OccamBackendPolicy policy,
        string? sessionProfile,
        CancellationToken cancellationToken)
    {
        var effectiveSigner = ReceiptsPolicy.Enabled() ? signer : null;
        var rows = new List<DatasetRow>(urls.Count);
        var rowInfos = new OccamDatasetRowInfo[urls.Count];

        for (var i = 0; i < urls.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (row, info) = await ExportOneAsync(urls[i], policy, sessionProfile, effectiveSigner, cancellationToken);
            rows.Add(row);
            rowInfos[i] = info;
        }

        var root = DatasetManifestBuilder.ManifestRoot(rows);
        var createdAt = DatasetManifestBuilder.NowUtc();
        var keyId = effectiveSigner?.KeyId ?? string.Empty;
        string? sig = null;
        if (effectiveSigner is not null)
        {
            var bytes = DatasetManifestBuilder.CanonicalBytes(
                DatasetManifestBuilder.Version, createdAt, rows.Count, root, keyId, DatasetManifestBuilder.Alg);
            sig = effectiveSigner.SignDetached(bytes);
        }

        var manifest = new OccamDatasetManifestInfo(
            DatasetManifestBuilder.Version,
            createdAt,
            rows.Count,
            root,
            effectiveSigner is null ? null : keyId,
            DatasetManifestBuilder.Alg,
            sig);

        return new OccamDatasetExportResponse(true, manifest, rowInfos, createdAt);
    }

    private async ValueTask<(DatasetRow Row, OccamDatasetRowInfo Info)> ExportOneAsync(
        string url,
        OccamBackendPolicy policy,
        string? sessionProfile,
        ReceiptSigner? effectiveSigner,
        CancellationToken cancellationToken)
    {
        var options = new OccamTranscodeOptions
        {
            JsonBlocks = true, // so each row carries a block-Merkle root
            SessionProfile = sessionProfile,
            PlaybookPolicy = PlaybookPolicy.Auto,
        };

        var outcome = await pipeline.TranscodeAsync(url, policy, options, cancellationToken);
        var finalUrl = outcome.FinalUrl ?? url;
        var backend = outcome.Backend ?? "http";

        if (!outcome.Ok || string.IsNullOrEmpty(outcome.Markdown))
        {
            var code = outcome.FailureCode ?? "extraction_failed";
            var negative = OccamTranscodeResponseBuilder.BuildNegativeReceipt(
                url, finalUrl, backend, code, outcome.StatusCode, effectiveSigner);
            var failRow = new DatasetRow(url, finalUrl, false, null, null, code);
            return (failRow, new OccamDatasetRowInfo(
                url, finalUrl, false, null, null, code, DatasetManifestBuilder.RowLeafHex(failRow), negative));
        }

        var blocks = outcome.Blocks is null
            ? Array.Empty<(string, string?)>()
            : [.. outcome.Blocks.Select(b => (b.Text ?? string.Empty, (string?)b.SourceSelector))];

        var contentHash = ReceiptCanonicalizer.ContentHash(outcome.Markdown);
        var blockRoot = MerkleTree.Root(blocks);
        var receipt = OccamTranscodeResponseBuilder.BuildReceipt(outcome, url, effectiveSigner)?.Signed;

        var row = new DatasetRow(url, finalUrl, true, contentHash, blockRoot, null);
        return (row, new OccamDatasetRowInfo(
            url, finalUrl, true, contentHash, blockRoot, null, DatasetManifestBuilder.RowLeafHex(row), receipt));
    }

}
