namespace OccamMcp.Rc2Regression;

internal static class PrCRegressionCases
{
    public static async Task<int> RunAsync(string fixtureRoot)
    {
        var test = new TestHarness("rc2-pr-c");
        DigestNormalizerCases.Run(test);
        PrCAccessCases.Run(test, fixtureRoot);
        await McpBoundaryCharacterization.RunDesiredContractAsync(test);
        return test.Finish();
    }
}
