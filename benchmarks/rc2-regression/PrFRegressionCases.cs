namespace OccamMcp.Rc2Regression;

internal static class PrFRegressionCases
{
    public static async Task<int> RunAsync(string fixtureRoot)
    {
        var test = new TestHarness("rc2-pr-f");
        DigestNormalizerCases.Run(test);
        PrCAccessCases.Run(test, fixtureRoot);
        PrDFocusCases.Run(test, fixtureRoot);
        PrEBudgetCases.Run(test, fixtureRoot);
        PrFSemanticCases.Run(test, fixtureRoot);
        await McpBoundaryCharacterization.RunDesiredContractAsync(test);
        return test.Finish();
    }
}
