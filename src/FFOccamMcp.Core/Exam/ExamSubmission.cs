using OccamMcp.Core.Canary;

namespace OccamMcp.Core.Exam;

/// <summary>
/// What an agent did, in the form the grader consumes.
/// </summary>
/// <remarks>
/// <para>
/// The grader deliberately takes a *record of behaviour* rather than driving the agent itself. MCP
/// has no general mechanism for a server to hand a client an arbitrary task and await a result —
/// `sampling/createMessage` comes closest but is optional and client-gated — so the administration
/// of the exam is left pluggable: an out-of-band harness, a host that supports sampling, or a human
/// can all produce this record. Keeping the engine pure is also what makes it testable without a
/// model in the loop.
/// </para>
/// <para>
/// Every field is nullable. An omitted submission is a failed task, not an error: an agent that
/// cannot attempt the chain task has told us something, and refusing to grade would lose it.
/// </para>
/// </remarks>
public sealed record ExamSubmission
{
    /// <summary>
    /// Verdict the canary verifier returned for the sentinel this agent reported. Supplied by the
    /// caller rather than re-derived here, because adjudication needs the process secret and the
    /// issuance log — see <see cref="CanaryService.Verify"/>.
    /// </summary>
    public CanaryVerdict? CanaryVerdict { get; init; }

    /// <summary>Raw JSON arguments the agent sent for the basic-call task.</summary>
    public string? BasicCallArguments { get; init; }

    /// <summary>Raw JSON arguments the agent sent for the focus-and-budget task.</summary>
    public string? FocusBudgetArguments { get; init; }

    /// <summary>The agent's attempt at the chaining task.</summary>
    public ExamChainAttempt? Chain { get; init; }
}

/// <summary>
/// An ordered sequence of calls plus the value the first one produced.
/// </summary>
/// <remarks>
/// The produced value is part of the submission because the grader never sees tool responses. That
/// keeps grading a pure function of the record, and it means the check is exact: the later call must
/// carry *that* value, not merely something that looks like a hash.
/// </remarks>
/// <param name="Calls">Raw JSON argument objects, in call order.</param>
/// <param name="ProducedValue">Value returned by an earlier call, for example a <c>contentHash</c>.</param>
public readonly record struct ExamChainAttempt(IReadOnlyList<string> Calls, string? ProducedValue);
