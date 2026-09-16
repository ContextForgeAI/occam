namespace OccamMcp.Core.Exam;

/// <summary>
/// Capability tier assigned to a client. Drives how wide a tool surface the host advertises
/// (docs/adr/0016-capability-exam.md).
/// </summary>
public enum AgentTier
{
    /// <summary>
    /// Could not demonstrate reliable tool use. Gets the narrowest surface, because the failure this
    /// guards against is tool *selection* — a weak agent reaching for playbook heal when it wanted
    /// to read a page and never recovering.
    /// </summary>
    Weak = 0,

    /// <summary>
    /// Default. Assigned when no exam result exists, which is the common case: assuming competence
    /// and being wrong is cheaper to recover from than starving a capable agent of tools.
    /// </summary>
    Medium = 1,

    /// <summary>Demonstrated every graded capability. Gets the full surface.</summary>
    Strong = 2,
}

/// <summary>The four graded tasks. Each is independently checkable and worth exactly one point.</summary>
public enum ExamTaskId
{
    /// <summary>
    /// Report a proof-of-read sentinel. Passes only on <c>READ_VERIFIED</c> — the one task whose
    /// grading cannot be gamed by pattern-matching, because the answer did not exist at training time.
    /// </summary>
    Canary = 0,

    /// <summary>Call a tool with schema-valid arguments: a parseable object with a usable absolute URL.</summary>
    BasicCall = 1,

    /// <summary>Use the token-economy parameters — a focus query together with an explicit budget.</summary>
    FocusBudget = 2,

    /// <summary>Chain two calls so the second consumes a value the first produced.</summary>
    Chain = 3,
}

/// <summary>Stable wire spellings for tiers and tasks.</summary>
public static class ExamWire
{
    /// <summary>Wire value for <see cref="AgentTier.Weak"/>.</summary>
    public const string Weak = "weak";

    /// <summary>Wire value for <see cref="AgentTier.Medium"/>.</summary>
    public const string Medium = "medium";

    /// <summary>Wire value for <see cref="AgentTier.Strong"/>.</summary>
    public const string Strong = "strong";

    /// <summary>Maps a tier to its wire spelling.</summary>
    /// <param name="tier">Tier to convert.</param>
    /// <returns>Lowercase wire token.</returns>
    public static string ToWire(AgentTier tier) => tier switch
    {
        AgentTier.Weak => Weak,
        AgentTier.Strong => Strong,
        _ => Medium,
    };

    /// <summary>Parses a wire tier, falling back to <see cref="AgentTier.Medium"/>.</summary>
    /// <param name="value">Wire token, case-insensitive.</param>
    /// <returns>The parsed tier, or the default when unrecognised.</returns>
    public static AgentTier ParseTier(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            Weak => AgentTier.Weak,
            Strong => AgentTier.Strong,
            _ => AgentTier.Medium,
        };

    /// <summary>Maps a task id to its wire spelling.</summary>
    /// <param name="task">Task to convert.</param>
    /// <returns>Lowercase snake_case token.</returns>
    public static string ToWire(ExamTaskId task) => task switch
    {
        ExamTaskId.Canary => "canary",
        ExamTaskId.BasicCall => "basic_call",
        ExamTaskId.FocusBudget => "focus_budget",
        _ => "chain",
    };
}

/// <summary>
/// Outcome of grading one task.
/// </summary>
/// <param name="Task">Which task was graded.</param>
/// <param name="Passed">Whether the point was awarded.</param>
/// <param name="Detail">Why, in a form that is useful to the agent being graded.</param>
public readonly record struct ExamTaskOutcome(ExamTaskId Task, bool Passed, string Detail);

/// <summary>
/// A graded exam.
/// </summary>
/// <param name="Score">Points awarded, 0–4.</param>
/// <param name="Tier">Tier derived from <paramref name="Score"/>.</param>
/// <param name="Outcomes">Per-task detail, in task order.</param>
/// <param name="GradedAt">When grading happened, UTC.</param>
/// <param name="Source">
/// How the tier was reached: <c>exam</c>, <c>default</c> (never sat), <c>rolling</c> (adjusted from
/// observed behaviour) or <c>operator</c> (pinned by configuration).
/// </param>
public readonly record struct ExamResult(
    int Score,
    AgentTier Tier,
    IReadOnlyList<ExamTaskOutcome> Outcomes,
    DateTimeOffset GradedAt,
    string Source)
{
    /// <summary>Wire spelling of <see cref="Tier"/>.</summary>
    public string TierWire => ExamWire.ToWire(Tier);

    /// <summary>Highest attainable score — one point per task in <see cref="ExamTaskId"/>.</summary>
    public static int MaxScore => Enum.GetValues<ExamTaskId>().Length;

    /// <summary>
    /// The result for a client that never sat the exam. Deliberately <see cref="AgentTier.Medium"/>:
    /// an unknown client is assumed competent until it demonstrates otherwise.
    /// </summary>
    /// <param name="at">Timestamp to record.</param>
    /// <returns>A default-tier result with no task outcomes.</returns>
    public static ExamResult Default(DateTimeOffset at) =>
        new(Score: 0, Tier: AgentTier.Medium, Outcomes: [], GradedAt: at, Source: "default");
}

/// <summary>
/// Identity an exam result is cached against.
/// </summary>
/// <remarks>
/// All three components matter and none is sufficient alone. <paramref name="ClientInfo"/> is the
/// MCP client name and version; <paramref name="ModelHint"/> is the model behind it, which the same
/// client can change between sessions; <paramref name="SessionId"/> keeps one session's result from
/// silently authorising another. Comparison is ordinal and case-sensitive, because these are
/// protocol identifiers rather than human text.
/// </remarks>
/// <param name="ClientInfo">MCP <c>clientInfo</c> name and version, or <c>"unknown"</c>.</param>
/// <param name="ModelHint">Model identifier the client declared, or <c>"unknown"</c>.</param>
/// <param name="SessionId">Session the result belongs to.</param>
public readonly record struct ExamSubject(string ClientInfo, string ModelHint, string SessionId)
{
    /// <summary>Normalises free-form inputs into a subject key, mapping blanks to <c>"unknown"</c>.</summary>
    /// <param name="clientInfo">Raw client info.</param>
    /// <param name="modelHint">Raw model hint.</param>
    /// <param name="sessionId">Raw session id.</param>
    /// <returns>A subject safe to use as a cache key.</returns>
    public static ExamSubject Create(string? clientInfo, string? modelHint, string? sessionId) =>
        new(
            Normalize(clientInfo),
            Normalize(modelHint),
            Normalize(sessionId));

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
}
