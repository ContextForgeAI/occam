using System.Diagnostics;
using System.Text.Json;
using OccamMcp.Core.Compile;
using OccamMcp.Core.Knowledge;
using OccamMcp.Core.Workers;

namespace OccamMcp.Rc2Regression;

internal static class PrEBudgetCases
{
    public static void Run(TestHarness test, string fixtureRoot)
    {
        var raw = Sidecars();
        var hiddenRequest = new MaterializationRequest(ExposePublicMedia: false);
        var hidden = ResponseProjection.Project(raw, hiddenRequest);
        var hiddenCaps = BudgetOwnership.PrepareSurfaceBudget(700, hidden);
        var unmarkedCaps = BudgetOwnership.PrepareSurfaceBudget(700, raw);
        var emptyCaps = BudgetOwnership.PrepareSurfaceBudget(700, ResponseProjection.Empty());
        test.Check("D10", "unrequested sidecars have zero public allocation",
            hidden.Blocks is null && hidden.Tables is null
            && hiddenCaps.SurfaceMaxTokens == emptyCaps.SurfaceMaxTokens
            && unmarkedCaps.SurfaceMaxTokens == emptyCaps.SurfaceMaxTokens,
            $"hidden={hiddenCaps.SurfaceMaxTokens}; unmarked={unmarkedCaps.SurfaceMaxTokens}; empty={emptyCaps.SurfaceMaxTokens}");

        var exposed = ResponseProjection.Project(raw,
            hiddenRequest with { ExposePublicBlocks = true, ExposePublicTables = true });
        var exposedCaps = BudgetOwnership.PrepareSurfaceBudget(700, exposed);
        test.Check("D10", "requested sidecars alone consume public allocation",
            exposed.IsProjected && exposed.Blocks?.Count == 3 && exposed.Tables?.Count == 1
            && exposedCaps.SurfaceMaxTokens < hiddenCaps.SurfaceMaxTokens,
            $"hidden={hiddenCaps.SurfaceMaxTokens}; exposed={exposedCaps.SurfaceMaxTokens}; structured={ResponseBudgetPlanner.EstimateStructuredRaw(exposed)}");

        var source = Read(fixtureRoot, "budget-answer.md");
        var constrained = TokenBudget.Apply(source, 128, "simple requests");
        var larger = TokenBudget.Apply(source, 256, "simple requests");
        test.Check("D10", "minimum answer unit retains coupled list",
            constrained.Text.Contains("ANSWER_BODY", StringComparison.Ordinal)
            && constrained.Text.Contains("- GET", StringComparison.Ordinal)
            && constrained.Text.Contains("- POST", StringComparison.Ordinal),
            $"tokens={TokenEstimator.Estimate(constrained.Text)}; strategy={constrained.Strategy}");
        test.Check("D10", "larger budget never removes protected answer unit",
            larger.Text.Contains("ANSWER_BODY", StringComparison.Ordinal)
            && larger.Text.Contains("- GET", StringComparison.Ordinal),
            $"small={TokenEstimator.Estimate(constrained.Text)}; large={TokenEstimator.Estimate(larger.Text)}");

        var partial = MaterializationAssessmentEvaluator.Evaluate(
            source, constrained.Text, "simple requests", null, truncated: true);
        var incomplete = MaterializationAssessmentEvaluator.Evaluate(
            source, "## Simple requests", "simple requests", null, truncated: true);
        test.Check("C10b", "focus found and retained reports partial rather than complete",
            partial.Focus is FocusMatchStatus.Hit or FocusMatchStatus.Weak
            && partial.Completeness == MaterializationCompleteness.Partial
            && partial.IncompleteReason == "context_truncated",
            $"focus={partial.Focus}; completeness={partial.Completeness}; reason={partial.IncompleteReason}; answerTokens={partial.SelectedAnswerUnitTokens}");
        test.Check("C10b", "focus found but body lost is explicit incomplete",
            incomplete.Focus is FocusMatchStatus.Hit or FocusMatchStatus.Weak
            && incomplete.Completeness == MaterializationCompleteness.Incomplete
            && incomplete.IncompleteReason == "focus_body_truncated"
            && incomplete.SuggestedMinTokens >= 128,
            $"focus={incomplete.Focus}; completeness={incomplete.Completeness}; reason={incomplete.IncompleteReason}; suggested={incomplete.SuggestedMinTokens}");

        var cap = BudgetOwnership.PrepareSurfaceBudget(700, exposed).Caps!;
        var markdown = TokenBudget.Apply(source, cap.MarkdownCap, "simple requests").Text;
        var trim = ResponseBudgetPlanner.TrimStructured(700, cap, markdown, exposed);
        var actualProjection = JsonSerializer.Serialize(new
        {
            markdown,
            blocks = trim.Blocks,
            tables = trim.Tables,
            receiptTokens = trim.Allocation.Receipt,
        });
        var actualTokens = TokenEstimator.Estimate(actualProjection);
        var tolerance = Math.Max(16, (int)Math.Ceiling(700 * 0.03));
        test.Check("D10", "projected allocation never silently exceeds max_tokens",
            trim.Allocation.Total <= 700,
            $"requested=700; estimated={trim.Allocation.Total}; markdown={trim.Allocation.Markdown}; structured={trim.Allocation.Blocks + trim.Allocation.Tables}");
        test.Check("D10", "estimated versus serialized projection is measured",
            actualTokens <= 700 + tolerance,
            $"estimated={trim.Allocation.Total}; actual={actualTokens}; tolerance={tolerance}");

        var started = Stopwatch.GetTimestamp();
        for (var i = 0; i < 200; i++)
        {
            _ = ResponseProjection.Project(raw, hiddenRequest);
            _ = TokenBudget.Apply(source, 128, "simple requests");
        }
        var elapsed = Stopwatch.GetElapsedTime(started);
        test.Check("D10", "projection and answer planning latency is bounded",
            elapsed.TotalMilliseconds < 250,
            $"iterations=200; elapsedMs={elapsed.TotalMilliseconds:F3}; plannerRetries=0");
    }

    private static ResponseBudgetSidecars Sidecars()
    {
        var blocks = Enumerable.Range(0, 3).Select(i => new WorkerExtractBlockInfo
        {
            Type = "paragraph",
            Text = $"Projected block {i}: " + new string('x', 60),
            SourceSelector = $"main > p:nth-of-type({i + 1})",
        }).ToArray();
        var tables = new[]
        {
            new WorkerExtractTableInfo
            {
                Caption = "Methods",
                Headers = ["method", "allowed"],
                Rows = [["GET", "yes"], ["POST", "yes"]],
                SourceSelector = "main > table",
            },
        };
        return new ResponseBudgetSidecars(blocks, tables, null, null, null, null, ExpectReceipt: true);
    }

    private static string Read(string root, string name) => File.ReadAllText(Path.Combine(root, name));
}
