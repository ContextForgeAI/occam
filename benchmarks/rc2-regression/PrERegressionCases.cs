namespace OccamMcp.Rc2Regression;

internal static class PrERegressionCases
{
    public static async Task<int> RunAsync(string fixtureRoot)
    {
        var test = new TestHarness("rc2-pr-e");
        DigestNormalizerCases.Run(test);
        PrCAccessCases.Run(test, fixtureRoot);
        PrDFocusCases.Run(test, fixtureRoot);
        PrEBudgetCases.Run(test, fixtureRoot);
        await McpBoundaryCharacterization.RunDesiredContractAsync(test);
        return test.Finish();
    }
}
