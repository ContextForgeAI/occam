using System.Text.Json;
using OccamMcp.Core.Canary;

namespace OccamMcp.Core.Exam;

/// <summary>
/// Wire format for an exam submission produced by an external harness (ADR-0016).
/// </summary>
/// <remarks>
/// Kept separate from <see cref="ExamSubmission"/> so the pure grader stays free of subject
/// identity and self-report fields used only by H3 comparison arms.
/// </remarks>
public sealed record ExamHarnessDocument
{
    public string? ClientInfo { get; init; }
    public string? ModelHint { get; init; }
    public string? SessionId { get; init; }

    /// <summary>Optional self-report tier for H3 baseline comparison (<c>weak|medium|strong</c>).</summary>
    public string? SelfReportTier { get; init; }

    public string? CanaryVerdict { get; init; }
    public string? BasicCallArguments { get; init; }
    public string? FocusBudgetArguments { get; init; }
    public ExamHarnessChainDocument? Chain { get; init; }
}

public sealed record ExamHarnessChainDocument
{
    public string[]? Calls { get; init; }
    public string? ProducedValue { get; init; }
}

/// <summary>Parses harness JSON into an <see cref="ExamSubmission"/> + subject metadata.</summary>
public static class ExamSubmissionParser
{
    public static bool TryParse(
        string json,
        out ExamHarnessDocument document,
        out ExamSubmission submission,
        out string? error)
    {
        document = new ExamHarnessDocument();
        submission = new ExamSubmission();
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "submission JSON is empty.";
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "submission root must be a JSON object.";
                return false;
            }

            var root = doc.RootElement;
            document = new ExamHarnessDocument
            {
                ClientInfo = ReadString(root, "clientInfo"),
                ModelHint = ReadString(root, "modelHint"),
                SessionId = ReadString(root, "sessionId"),
                SelfReportTier = ReadString(root, "selfReportTier"),
                CanaryVerdict = ReadString(root, "canaryVerdict"),
                BasicCallArguments = ReadRawOrString(root, "basicCallArguments"),
                FocusBudgetArguments = ReadRawOrString(root, "focusBudgetArguments"),
                Chain = ReadChain(root),
            };

            CanaryVerdict? verdict = null;
            if (!string.IsNullOrWhiteSpace(document.CanaryVerdict)
                && CanaryVerdictStrings.TryParse(document.CanaryVerdict, out var parsed))
            {
                verdict = parsed;
            }
            else if (!string.IsNullOrWhiteSpace(document.CanaryVerdict))
            {
                // Unrecognised token → treat as hallucinated (failed canary), not a parse error.
                verdict = Canary.CanaryVerdict.Hallucinated;
            }

            ExamChainAttempt? chain = null;
            if (document.Chain is { Calls: { Length: > 0 } calls })
            {
                chain = new ExamChainAttempt(calls, document.Chain.ProducedValue);
            }

            submission = new ExamSubmission
            {
                CanaryVerdict = verdict,
                BasicCallArguments = document.BasicCallArguments,
                FocusBudgetArguments = document.FocusBudgetArguments,
                Chain = chain,
            };
            return true;
        }
        catch (JsonException ex)
        {
            error = $"submission JSON is malformed: {ex.Message}";
            return false;
        }
    }

    private static ExamHarnessChainDocument? ReadChain(JsonElement root)
    {
        if (!root.TryGetProperty("chain", out var chain) || chain.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string[]? calls = null;
        if (chain.TryGetProperty("calls", out var callsEl) && callsEl.ValueKind == JsonValueKind.Array)
        {
            var list = new List<string>();
            foreach (var item in callsEl.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    list.Add(item.GetString() ?? "");
                }
                else if (item.ValueKind == JsonValueKind.Object)
                {
                    list.Add(item.GetRawText());
                }
            }

            calls = list.ToArray();
        }

        return new ExamHarnessChainDocument
        {
            Calls = calls,
            ProducedValue = ReadString(chain, "producedValue"),
        };
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    /// <summary>Accepts either a JSON string or an embedded object (serialized back to text).</summary>
    private static string? ReadRawOrString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el))
        {
            return null;
        }

        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Object => el.GetRawText(),
            _ => null,
        };
    }
}
