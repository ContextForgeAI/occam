using System.Text.Json.Serialization;

namespace OccamMcp.Core.Batch;

[JsonSerializable(typeof(BatchSubmitRequest))]
[JsonSerializable(typeof(BatchSubmitResponse))]
[JsonSerializable(typeof(BatchStatusResponse))]
[JsonSerializable(typeof(BatchResultsResponse))]
[JsonSerializable(typeof(BatchItemResult))]
[JsonSerializable(typeof(BatchFailureInfo))]
[JsonSerializable(typeof(BatchErrorResponse))]
[JsonSerializable(typeof(BatchProgress))]
[JsonSerializable(typeof(BatchJobParams))]
[JsonSerializable(typeof(BatchHealthResponse))]
[JsonSerializable(typeof(BatchStoreSnapshot))]
internal partial class BatchJsonContext : JsonSerializerContext;
