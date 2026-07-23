using OccamMcp.Core.Knowledge;

namespace OccamMcp.Core.Compile;

/// <summary>Internal projection telemetry retained for PR-F mapping and calibration.</summary>
public sealed record ResponseBudgetDiagnostics(
    int RequestedTokens,
    int EstimatedProjectedTokens,
    int MarkdownTokens,
    int StructuredTokens,
    int ReceiptTokens,
    int PlannerRetries,
    int? SelectedAnswerUnitTokens,
    MaterializationCompleteness Completeness,
    int? ActualSerializedProjectionTokens = null);
