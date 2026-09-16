using System.Text.Json;
using OccamMcp.Core.Canary;

namespace OccamMcp.Core.Exam;

/// <summary>
/// Grades an <see cref="ExamSubmission"/> into an <see cref="ExamResult"/>.
/// </summary>
/// <remarks>
/// <para>
/// Pure and total: the same submission always grades the same way, malformed input scores zero for
/// that task instead of throwing, and no task can throw its way out of being graded. JSON is read
/// with <see cref="JsonDocument"/> so the grader stays reflection-free under Native AOT.
/// </para>
/// <para>
/// Grading is deliberately strict about *form* and silent about *quality*. "Did the arguments bind
/// to the schema" is checkable; "was that a good focus query" is not, and a grader that pretended
/// otherwise would produce a tier nobody could reason about.
/// </para>
/// </remarks>
public static class ExamGrader
{
    /// <summary>Grades every task and derives a tier.</summary>
    /// <param name="submission">Record of what the agent did; <c>null</c> grades as zero.</param>
    /// <param name="gradedAt">Timestamp to record on the result.</param>
    /// <returns>The graded result, with one outcome per task in catalogue order.</returns>
    public static ExamResult Grade(ExamSubmission? submission, DateTimeOffset gradedAt)
    {
        var outcomes = new List<ExamTaskOutcome>(ExamTasks.All.Count)
        {
            GradeCanary(submission?.CanaryVerdict),
            GradeBasicCall(submission?.BasicCallArguments),
            GradeFocusBudget(submission?.FocusBudgetArguments),
            GradeChain(submission?.Chain),
        };

        var score = 0;
        foreach (var outcome in outcomes)
        {
            if (outcome.Passed)
            {
                score++;
            }
        }

        return new ExamResult(score, ExamScoring.TierFor(score), outcomes, gradedAt, Source: "exam");
    }

    /// <summary>
    /// Grades the proof-of-read task. Only <see cref="CanaryVerdict.ReadVerified"/> passes.
    /// </summary>
    /// <remarks>
    /// <see cref="CanaryVerdict.ReadStale"/> is real evidence of a read and still fails, because the
    /// task asks for a *current* one — an agent replaying a sentinel from an earlier turn has not
    /// demonstrated that it reads when asked. <see cref="CanaryVerdict.ReplaySuspect"/> fails for a
    /// stronger reason: the value is authentic but its provenance is unknown.
    /// </remarks>
    /// <param name="verdict">Verdict from the canary verifier, or <c>null</c> if not attempted.</param>
    /// <returns>The task outcome.</returns>
    public static ExamTaskOutcome GradeCanary(CanaryVerdict? verdict)
    {
        if (verdict is null)
        {
            return new ExamTaskOutcome(ExamTaskId.Canary, false, "No sentinel was reported.");
        }

        return verdict switch
        {
            CanaryVerdict.ReadVerified => new ExamTaskOutcome(
                ExamTaskId.Canary, true, "Sentinel verified as a current read."),
            CanaryVerdict.ReadStale => new ExamTaskOutcome(
                ExamTaskId.Canary, false,
                "Sentinel is authentic but from an earlier bucket; the task asks for a current read."),
            CanaryVerdict.ReplaySuspect => new ExamTaskOutcome(
                ExamTaskId.Canary, false,
                "Sentinel is authentic but this host never recorded issuing it."),
            _ => new ExamTaskOutcome(
                ExamTaskId.Canary, false, "Reported sentinel matched nothing in the recognised window."),
        };
    }

    /// <summary>Grades schema binding: a JSON object carrying a usable absolute URL.</summary>
    /// <param name="argumentsJson">Raw arguments the agent sent.</param>
    /// <returns>The task outcome.</returns>
    public static ExamTaskOutcome GradeBasicCall(string? argumentsJson)
    {
        if (!TryReadObject(argumentsJson, out var document, out var failure))
        {
            return new ExamTaskOutcome(ExamTaskId.BasicCall, false, failure);
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("url", out var url))
            {
                return new ExamTaskOutcome(ExamTaskId.BasicCall, false, "Required parameter `url` is absent.");
            }

            if (url.ValueKind != JsonValueKind.String)
            {
                return new ExamTaskOutcome(
                    ExamTaskId.BasicCall, false, $"`url` must be a string, got {url.ValueKind}.");
            }

            var raw = url.GetString();
            if (!IsUsableHttpUrl(raw))
            {
                return new ExamTaskOutcome(
                    ExamTaskId.BasicCall, false, "`url` is not an absolute http or https URL.");
            }

            return new ExamTaskOutcome(ExamTaskId.BasicCall, true, "Arguments bound to the schema.");
        }
    }

    /// <summary>
    /// Grades token-economy awareness: a focus query together with an explicit budget.
    /// Accepts either the specialised <c>focus_query</c>/<c>max_tokens</c> pair or the cascade
    /// facade's <c>task</c>/<c>budget</c> aliases.
    /// </summary>
    /// <param name="argumentsJson">Raw arguments the agent sent.</param>
    /// <returns>The task outcome.</returns>
    public static ExamTaskOutcome GradeFocusBudget(string? argumentsJson)
    {
        if (!TryReadObject(argumentsJson, out var document, out var failure))
        {
            return new ExamTaskOutcome(ExamTaskId.FocusBudget, false, failure);
        }

        using (document)
        {
            var root = document.RootElement;

            string? focusText = null;
            if (root.TryGetProperty("focus_query", out var focus)
                && focus.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(focus.GetString()))
            {
                focusText = focus.GetString();
            }
            else if (root.TryGetProperty("task", out var task)
                     && task.ValueKind == JsonValueKind.String
                     && !string.IsNullOrWhiteSpace(task.GetString()))
            {
                focusText = task.GetString();
            }

            if (focusText is null)
            {
                return new ExamTaskOutcome(
                    ExamTaskId.FocusBudget, false, "`focus_query`/`task` is absent or empty.");
            }

            JsonElement budgetEl;
            string budgetName;
            if (root.TryGetProperty("max_tokens", out budgetEl))
            {
                budgetName = "max_tokens";
            }
            else if (root.TryGetProperty("budget", out budgetEl))
            {
                budgetName = "budget";
            }
            else
            {
                return new ExamTaskOutcome(
                    ExamTaskId.FocusBudget, false, "`max_tokens`/`budget` is absent; the budget must be explicit.");
            }

            if (budgetEl.ValueKind != JsonValueKind.Number || !budgetEl.TryGetInt32(out var tokens))
            {
                return new ExamTaskOutcome(
                    ExamTaskId.FocusBudget, false, $"`{budgetName}` must be an integer.");
            }

            if (tokens < ExamTasks.MinPlausibleBudget || tokens > ExamTasks.MaxPlausibleBudget)
            {
                return new ExamTaskOutcome(
                    ExamTaskId.FocusBudget,
                    false,
                    $"`{budgetName}`={tokens} is outside [{ExamTasks.MinPlausibleBudget}..{ExamTasks.MaxPlausibleBudget}]; " +
                    "a number larger than any real context window is not a budget.");
            }

            return new ExamTaskOutcome(
                ExamTaskId.FocusBudget, true, $"Focused request with an explicit {tokens}-token budget.");
        }
    }

    /// <summary>Grades chaining: a later call must carry the exact value an earlier one produced.</summary>
    /// <param name="attempt">The agent's call sequence and the value it captured.</param>
    /// <returns>The task outcome.</returns>
    public static ExamTaskOutcome GradeChain(ExamChainAttempt? attempt)
    {
        if (attempt is null)
        {
            return new ExamTaskOutcome(ExamTaskId.Chain, false, "No call sequence was submitted.");
        }

        var chain = attempt.Value;
        if (chain.Calls is null || chain.Calls.Count < 2)
        {
            return new ExamTaskOutcome(
                ExamTaskId.Chain, false, "Chaining needs at least two calls.");
        }

        if (string.IsNullOrWhiteSpace(chain.ProducedValue))
        {
            return new ExamTaskOutcome(
                ExamTaskId.Chain, false, "No value from the first call was captured, so nothing could be chained.");
        }

        var produced = chain.ProducedValue.Trim();

        // Only calls after the first can consume; a value cannot be carried backwards.
        for (var i = 1; i < chain.Calls.Count; i++)
        {
            if (!TryReadObject(chain.Calls[i], out var document, out _))
            {
                continue;
            }

            using (document)
            {
                foreach (var parameter in ExamTasks.ChainConsumingParameters)
                {
                    if (document.RootElement.TryGetProperty(parameter, out var value)
                        && value.ValueKind == JsonValueKind.String
                        && string.Equals(value.GetString()?.Trim(), produced, StringComparison.Ordinal))
                    {
                        return new ExamTaskOutcome(
                            ExamTaskId.Chain, true, $"Call {i + 1} consumed the captured value via `{parameter}`.");
                    }
                }
            }
        }

        return new ExamTaskOutcome(
            ExamTaskId.Chain,
            false,
            "No later call passed the captured value as " +
            string.Join(" or ", ExamTasks.ChainConsumingParameters.Select(p => $"`{p}`")) + ".");
    }

    private static bool TryReadObject(string? json, out JsonDocument document, out string failure)
    {
        document = null!;
        if (string.IsNullOrWhiteSpace(json))
        {
            failure = "No arguments were submitted.";
            return false;
        }

        JsonDocument parsed;
        try
        {
            parsed = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            failure = $"Arguments are not valid JSON: {ex.Message}";
            return false;
        }

        var rootKind = parsed.RootElement.ValueKind;
        if (rootKind != JsonValueKind.Object)
        {
            // Read the kind before disposing: RootElement is a view over the document's buffer.
            parsed.Dispose();
            failure = $"Arguments must be a JSON object, got {rootKind}.";
            return false;
        }

        document = parsed;
        failure = string.Empty;
        return true;
    }

    private static bool IsUsableHttpUrl(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
