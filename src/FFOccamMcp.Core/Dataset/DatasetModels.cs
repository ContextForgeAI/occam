using System.Text.Json.Serialization;
using OccamMcp.Core.Receipts;

namespace OccamMcp.Core.Dataset;

/// <summary>
/// One row of an exported dataset. <see cref="RowLeaf"/> is the manifest leaf (recomputable from the
/// other fields via <c>DatasetManifestBuilder.RowLeafHex</c>); <see cref="Receipt"/> is the row's own
/// signed extraction receipt (independently verifiable via <c>occam_verify</c>).
/// </summary>
public sealed record OccamDatasetRowInfo(
    string Url,
    string FinalUrl,
    bool Ok,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ContentHash,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? BlockMerkleRoot,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? FailureCode,
    string RowLeaf,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ReceiptEnvelope? Receipt);

/// <summary>
/// The signed dataset manifest: one signature over the Merkle root of the row leaves. Verify with
/// <c>DatasetManifestBuilder.Verify(rows, …)</c> — reconstructs the root from the rows and checks the
/// detached signature. <see cref="Sig"/> is null when receipts are disabled (<c>OCCAM_RECEIPTS=off</c>).
/// </summary>
public sealed record OccamDatasetManifestInfo(
    int V,
    string CreatedAt,
    int RowCount,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ManifestRoot,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? KeyId,
    string Alg,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Sig);

public sealed record OccamDatasetExportResponse(
    bool Ok,
    OccamDatasetManifestInfo Manifest,
    OccamDatasetRowInfo[] Rows,
    string Timestamp);

public sealed record OccamDatasetExportFailureInfo(string Code, string Message);

public sealed record OccamDatasetExportFailureResponse(
    bool Ok,
    OccamDatasetExportFailureInfo Failure,
    string Timestamp);

[JsonSerializable(typeof(OccamDatasetExportResponse))]
[JsonSerializable(typeof(OccamDatasetExportFailureResponse))]
[JsonSerializable(typeof(OccamDatasetRowInfo))]
[JsonSerializable(typeof(OccamDatasetRowInfo[]))]
[JsonSerializable(typeof(OccamDatasetManifestInfo))]
[JsonSerializable(typeof(ReceiptEnvelope))]
[JsonSerializable(typeof(ReceiptPlaybook))]
[JsonSerializable(typeof(string[]))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class OccamDatasetJsonContext : JsonSerializerContext;
