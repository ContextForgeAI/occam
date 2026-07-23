using OccamMcp.Core.Digest;

namespace OccamMcp.Rc2Regression;

internal static class DigestParserCases
{
    public static void Run(TestHarness test)
    {
        test.Check("D12", "single URL string reaches normalization",
            DigestUrlParser.TryParse("https://example.com/one", out var single, out _) && single.Count == 1,
            $"parserInvoked=true; entries={single.Count}");
        test.Check("D12", "multiple delimiter URLs reach normalization",
            DigestUrlParser.TryParse("https://example.com/one\nhttps://example.com/two", out var multiple, out _) && multiple.Count == 2,
            $"parserInvoked=true; entries={multiple.Count}");
        test.Check("D12", "JSON-encoded string array reaches normalization",
            DigestUrlParser.TryParse("[\"https://example.com/one\",\"https://example.com/two\"]", out var json, out _) && json.Count == 2,
            $"parserInvoked=true; entries={json.Count}");
        test.Check("D12", "empty string is a typed parser rejection",
            !DigestUrlParser.TryParse("", out _, out var emptyError) && !string.IsNullOrWhiteSpace(emptyError),
            $"parserInvoked=true; error={emptyError}");
        test.Check("D12", "mixed JSON string array is a typed parser rejection",
            !DigestUrlParser.TryParse("[\"https://example.com/one\",7]", out _, out var mixedError) && !string.IsNullOrWhiteSpace(mixedError),
            $"parserInvoked=true; error={mixedError}");
    }
}
