using System.Text.Json;

namespace OccamMcp.Rc2Regression;

internal sealed class TestHarness(string suite)
{
    private readonly List<TestResult> _results = [];

    public IReadOnlyList<TestResult> Results => _results;

    public void Check(string defect, string name, bool passed, string observation, bool intentionallyRed = false)
    {
        _results.Add(new TestResult(defect, name, passed, observation, intentionallyRed));
        var disposition = passed ? "PASS" : intentionallyRed ? "EXPECTED_RED" : "FAIL";
        Console.WriteLine($"{disposition} [{defect}] {name} :: {observation}");
    }

    public int Finish()
    {
        var failures = _results.Count(result => !result.Passed);
        var summary = new
        {
            suite,
            total = _results.Count,
            passed = _results.Count - failures,
            failed = failures,
            intentionallyRed = _results.Count(result => result.IntentionallyRed && !result.Passed),
            results = _results,
        };
        Console.WriteLine(JsonSerializer.Serialize(summary));
        return failures == 0 ? 0 : 1;
    }
}

internal sealed record TestResult(
    string Defect,
    string Name,
    bool Passed,
    string Observation,
    bool IntentionallyRed);
