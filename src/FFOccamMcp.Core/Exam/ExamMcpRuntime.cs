using OccamMcp.Core.Configuration;
using OccamMcp.Core.Transport;

namespace OccamMcp.Core.Exam;

/// <summary>
/// MCP-side exam bridge: grade a harness submission, cache the result, optionally apply the
/// session tool surface. Gated by <c>OCCAM_EXAM_MCP</c> (default off).
/// </summary>
public sealed class ExamMcpRuntime
{
    public const string EnvFlag = "OCCAM_EXAM_MCP";

    private readonly ExamResultCache _cache;
    private readonly SessionToolSurface _surface;
    private readonly TimeProvider _timeProvider;

    public ExamMcpRuntime(
        ExamResultCache cache,
        SessionToolSurface surface,
        TimeProvider? timeProvider = null)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _surface = surface ?? throw new ArgumentNullException(nameof(surface));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public SessionToolSurface Surface => _surface;

    public ExamResultCache Cache => _cache;

    /// <summary>Whether the opt-in MCP exam path is enabled for this process.</summary>
    public static bool IsEnabled =>
        OccamEnvironment.GetFlag(EnvFlag, defaultValue: false);

    /// <summary>
    /// Best-effort cache apply after initialize. Never applies <see cref="ExamResult.Default"/> —
    /// a miss leaves the host default (reader) alone. Does not block the caller.
    /// </summary>
    public bool TryApplyCached(ExamSubject subject, out ExamResult result)
    {
        if (_surface.IsPinned || !_cache.TryGet(subject, out result))
        {
            result = default;
            return false;
        }

        return _surface.TryApplyExamTier(result.Tier);
    }

    /// <summary>
    /// Grades a harness submission JSON, stores the result, and applies the surface when not pinned.
    /// </summary>
    public ExamMcpSubmitOutcome Submit(string submissionJson)
    {
        if (!ExamSubmissionParser.TryParse(submissionJson, out var document, out var submission, out var error))
        {
            return ExamMcpSubmitOutcome.Invalid(error ?? "invalid submission.");
        }

        var now = _timeProvider.GetUtcNow();
        var result = ExamGrader.Grade(submission, now);
        var subject = ExamSubject.Create(document.ClientInfo, document.ModelHint, document.SessionId);
        _cache.Store(subject, result);

        var recommendedProfile = ExamScoring.ProfileFor(result.Tier);
        var toolCount = ExamScoring.SurfaceSizeFor(result.Tier);
        var applied = _surface.TryApplyExamTier(result.Tier);

        AgentTier? selfReport = string.IsNullOrWhiteSpace(document.SelfReportTier)
            ? null
            : ExamWire.ParseTier(document.SelfReportTier);

        return new ExamMcpSubmitOutcome(
            Ok: true,
            FailureCode: null,
            FailureMessage: null,
            Subject: subject,
            Result: result,
            RecommendedProfile: recommendedProfile,
            ToolCount: toolCount,
            Applied: applied,
            Pinned: _surface.IsPinned,
            ActiveProfile: _surface.ProfileId,
            SelfReportTier: selfReport,
            StaticMapTier: StaticModelMap.Map(document.ModelHint));
    }
}

/// <summary>Result of <see cref="ExamMcpRuntime.Submit"/>.</summary>
public readonly record struct ExamMcpSubmitOutcome(
    bool Ok,
    string? FailureCode,
    string? FailureMessage,
    ExamSubject Subject,
    ExamResult Result,
    string RecommendedProfile,
    int ToolCount,
    bool Applied,
    bool Pinned,
    string ActiveProfile,
    AgentTier? SelfReportTier,
    AgentTier StaticMapTier)
{
    public static ExamMcpSubmitOutcome Invalid(string message) =>
        new(
            Ok: false,
            FailureCode: "invalid_arguments",
            FailureMessage: message,
            Subject: default,
            Result: default,
            RecommendedProfile: OccamToolProfile.Reader,
            ToolCount: 0,
            Applied: false,
            Pinned: false,
            ActiveProfile: OccamToolProfile.Reader,
            SelfReportTier: null,
            StaticMapTier: AgentTier.Medium);
}
