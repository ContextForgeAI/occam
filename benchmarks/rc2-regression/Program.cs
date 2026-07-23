using OccamMcp.Rc2Regression;

var mode = args.Length == 0 ? "characterization" : args[0].TrimStart('-').ToLowerInvariant();
var fixtureRoot = Path.Combine(AppContext.BaseDirectory, "fixtures");

return mode switch
{
    "characterization" => await RegressionCases.RunCharacterizationAsync(fixtureRoot),
    "regression" => await RegressionCases.RunExpectedRedAsync(fixtureRoot),
    "spikes" => TechnicalSpikes.Run(fixtureRoot),
    "pr-b" => await PrBRegressionCases.RunAsync(),
    "pr-c" => await PrCRegressionCases.RunAsync(fixtureRoot),
    "pr-d" => await PrDRegressionCases.RunAsync(fixtureRoot),
    "pr-e" => await PrERegressionCases.RunAsync(fixtureRoot),
    "pr-f" => await PrFRegressionCases.RunAsync(fixtureRoot),
    "pr-g" => await PrGRegressionCases.RunAsync(fixtureRoot),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("Usage: Rc2Regression [--characterization|--regression|--spikes|--pr-b|--pr-c|--pr-d|--pr-e|--pr-f|--pr-g]");
    return 2;
}
