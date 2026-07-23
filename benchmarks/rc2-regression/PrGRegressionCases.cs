namespace OccamMcp.Rc2Regression;

internal static class PrGRegressionCases
{
    public static async Task<int> RunAsync(string fixtureRoot)
    {
        var test = new TestHarness("rc2-pr-g");
        DigestNormalizerCases.Run(test);
        PrCAccessCases.Run(test, fixtureRoot);
        PrDFocusCases.Run(test, fixtureRoot);
        PrEBudgetCases.Run(test, fixtureRoot);
        PrFSemanticCases.Run(test, fixtureRoot);
        PrGLifecycleCases.Run(test);
        await McpBoundaryCharacterization.RunDesiredContractAsync(test);
        return test.Finish();
    }
}
