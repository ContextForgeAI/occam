using System.Text.Json;
using OccamMcp.Core.BrowserActions;

namespace OccamMcp.L0Gate;

internal static class BrowserActionPlanUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        using var empty = JsonDocument.Parse("[]");
        var emptyResult = BrowserActionPlan.Validate(empty.RootElement);
        assert("browser actions: empty array rejected", !emptyResult.Ok);

        using var tooMany = JsonDocument.Parse(
            "[" + string.Join(",", Enumerable.Repeat("{\"do\":\"wait\",\"ms\":50}", BrowserActionPlan.MaxActions + 1)) + "]");
        assert("browser actions: >16 rejected", !BrowserActionPlan.Validate(tooMany.RootElement).Ok);

        using var js = JsonDocument.Parse("[{\"do\":\"wait\",\"ms\":50,\"js\":\"1+1\"}]");
        assert("browser actions: js smuggle rejected", !BrowserActionPlan.Validate(js.RootElement).Ok);

        using var ok = JsonDocument.Parse("""
            [
              {"do":"type","selector":"input","text":"secret"},
              {"do":"press","key":"Enter"},
              {"do":"scroll","to":"bottom"}
            ]
            """);
        var validated = BrowserActionPlan.Validate(ok.RootElement);
        assert("browser actions: valid plan accepted", validated.Ok && validated.Actions is { Count: 3 });
        var hash = BrowserActionPlan.PlanHash(validated.Actions!);
        assert("browser actions: plan hash is 64 hex", hash.Length == 64);
        assert("browser actions: hash stable", hash == BrowserActionPlan.PlanHash(validated.Actions!));
        var wire = BrowserActionPlan.SerializeForWorker(validated.Actions!);
        assert("browser actions: worker JSON retains type text for execution", wire.Contains("secret", StringComparison.Ordinal));
        assert("browser actions: plan hash redacts type text", !hash.Contains("secret", StringComparison.Ordinal));

        Console.WriteLine("L_BROWSER_ACTIONS_OK");
    }
}
