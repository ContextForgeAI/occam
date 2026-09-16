namespace OccamMcp.Core.Exam;

/// <summary>
/// The exam catalogue: what each task asks for, and what the grader accepts as a pass.
/// </summary>
/// <remarks>
/// <para>
/// The prompts live here rather than in a doc so that the text an agent is given and the rule it is
/// graded against cannot drift apart. Every task is worth exactly one point: weighting them would
/// require evidence about their relative predictive value, and there is none yet
/// (docs/research/hypothesis.md, H3).
/// </para>
/// <para>
/// Each task is designed to fail for a *different* reason, so a score is diagnostic rather than
/// just ordinal: schema binding, token-economy awareness, multi-step state handling, and — with the
/// canary — whether the agent reads at all.
/// </para>
/// </remarks>
public static class ExamTasks
{
    /// <summary>A graded task's prompt and its pass condition, in agent-facing language.</summary>
    /// <param name="Id">Task identifier.</param>
    /// <param name="Prompt">What the agent is asked to do.</param>
    /// <param name="PassCondition">What the grader accepts, stated so the agent can self-check.</param>
    /// <param name="Measures">The capability this task isolates.</param>
    public readonly record struct Definition(
        ExamTaskId Id,
        string Prompt,
        string PassCondition,
        string Measures);

    /// <summary>Lowest accepted <c>max_tokens</c> for the budget task.</summary>
    public const int MinPlausibleBudget = 64;

    /// <summary>
    /// Highest accepted <c>max_tokens</c> for the budget task. A caller who passes a number larger
    /// than any real context window has not declared a budget, it has opted out of one.
    /// </summary>
    public const int MaxPlausibleBudget = 200_000;

    /// <summary>Parameters that consume a value produced by an earlier call.</summary>
    public static readonly string[] ChainConsumingParameters = ["if_none_match", "diff_against"];

    /// <summary>The four tasks, in grading order.</summary>
    /// <returns>Task definitions; the collection length is the maximum score.</returns>
    public static IReadOnlyList<Definition> All { get; } =
    [
        new(
            ExamTaskId.Canary,
            Prompt:
                "Fetch the probe URL you were given and report the value of its `occam-sentinel` " +
                "meta tag, verbatim. Do not guess: an unmatched value is recorded as a hallucination.",
            PassCondition:
                "The reported sentinel adjudicates to READ_VERIFIED. READ_STALE does not pass — the " +
                "task asks for a current read.",
            Measures: "Whether the agent actually reads fetched content, rather than answering from prior knowledge."),
        new(
            ExamTaskId.BasicCall,
            Prompt:
                "Call `occam` on https://example.com using only the required parameter (`url`).",
            PassCondition:
                "Arguments parse as a JSON object containing `url`, whose value is an absolute http " +
                "or https URL.",
            Measures: "Basic schema binding — the most common failure mode for a small model."),
        new(
            ExamTaskId.FocusBudget,
            Prompt:
                "Call `occam` on a long page with a `task` (what you need) and an explicit `budget` " +
                "(token cap).",
            PassCondition:
                $"Arguments contain a non-empty focus (`focus_query` or `task`) and an explicit " +
                $"budget integer (`max_tokens` or `budget`) in " +
                $"[{MinPlausibleBudget}..{MaxPlausibleBudget}].",
            Measures: "Whether the agent manages its own context budget or floods it."),
        new(
            ExamTaskId.Chain,
            Prompt:
                "Read a page, keep its `contentHash`, then make a second call that checks whether " +
                "the page changed without re-downloading the body.",
            PassCondition:
                "At least two calls, where a later call passes the exact value produced by an " +
                "earlier one as `if_none_match` or `diff_against`.",
            Measures: "Multi-step state handling — carrying a value across calls instead of restarting."),
    ];

    /// <summary>Looks up one definition.</summary>
    /// <param name="id">Task to find.</param>
    /// <returns>The definition for <paramref name="id"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">No such task.</exception>
    public static Definition Get(ExamTaskId id)
    {
        foreach (var definition in All)
        {
            if (definition.Id == id)
            {
                return definition;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown exam task.");
    }
}
