namespace OccamMcp.Rc2Regression;

internal static class PrBRegressionCases
{
    public static async Task<int> RunAsync()
    {
        var test = new TestHarness("rc2-pr-b");
        DigestNormalizerCases.Run(test);
        await McpBoundaryCharacterization.RunDesiredContractAsync(test);
        return test.Finish();
    }
}
