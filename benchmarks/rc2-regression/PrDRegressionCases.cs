namespace OccamMcp.Rc2Regression;

internal static class PrDRegressionCases
{
    public static async Task<int> RunAsync(string fixtureRoot)
    {
        var test = new TestHarness("rc2-pr-d");
        DigestNormalizerCases.Run(test);
        PrCAccessCases.Run(test, fixtureRoot);
        PrDFocusCases.Run(test, fixtureRoot);
        await McpBoundaryCharacterization.RunDesiredContractAsync(test);
        return test.Finish();
    }
}
