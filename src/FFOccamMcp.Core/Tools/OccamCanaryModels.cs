using System.Text.Json.Serialization;

namespace OccamMcp.Core.Tools;

public sealed record OccamCanaryIssueSuccessResponse(
    bool Ok,
    string Url,
    string SessionId,
    string ExpiresAt,
    long Bucket);

public sealed record OccamCanaryVerifySuccessResponse(
    bool Ok,
    string Verdict,
    long? Bucket,
    string Reason,
    string SessionId,
    long CurrentBucket,
    long? MatchedBucket = null,
    int? BucketDistance = null);

public sealed record OccamCanaryFailureInfo(string Code, string Message);

public sealed record OccamCanaryFailureResponse(
    bool Ok,
    OccamCanaryFailureInfo Failure,
    string Timestamp);

[JsonSerializable(typeof(OccamCanaryIssueSuccessResponse))]
[JsonSerializable(typeof(OccamCanaryVerifySuccessResponse))]
[JsonSerializable(typeof(OccamCanaryFailureResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class OccamCanaryJsonContext : JsonSerializerContext;
