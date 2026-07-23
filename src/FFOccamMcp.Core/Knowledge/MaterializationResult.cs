using OccamMcp.Core.Compile;

namespace OccamMcp.Core.Knowledge;

/// <summary>
/// Full planner output: the task-shaped view plus compile/budget telemetry that the pipeline needs
/// for receipts and the public <c>compile</c> block. Codecs consume only <see cref="View"/>.
/// </summary>
public sealed record MaterializationResult(
    MaterializedKnowledgeView View,
    bool SelectorsMatched,
    bool Truncated,
    int TokensEstimated,
    string? TruncationStrategy = null,
    OmittedManifest? Omitted = null,
    MaterializationAssessment? Assessment = null);
