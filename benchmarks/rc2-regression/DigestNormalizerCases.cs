using System.Text.Json;
using OccamMcp.Core.Digest;

namespace OccamMcp.Rc2Regression;

internal static class DigestNormalizerCases
{
    public static void Run(TestHarness test)
    {
        using var native = JsonDocument.Parse(
            "[\"https://example.com/one\",\"https://example.com/two\"]");
        test.Check("D12", "native string array normalizes in order",
            DigestInputNormalizer.TryNormalize(native.RootElement, out var entries, out _)
            && entries.Select(entry => entry.Url).SequenceEqual(
                ["https://example.com/one", "https://example.com/two"]),
            $"entries={entries.Count}");

        var legacy = JsonSerializer.SerializeToElement(
            "[\"https://example.com/one\",\"https://example.com/two\"]");
        test.Check("D12", "legacy JSON-array string remains supported",
            DigestInputNormalizer.TryNormalize(legacy, out var legacyEntries, out _)
            && legacyEntries.Count == 2,
            $"entries={legacyEntries.Count}");

        var delimited = JsonSerializer.SerializeToElement(
            "https://example.com/one\nhttps://example.com/two");
        test.Check("D12", "legacy delimiter string remains supported",
            DigestInputNormalizer.TryNormalize(delimited, out var delimitedEntries, out _)
            && delimitedEntries.Count == 2,
            $"entries={delimitedEntries.Count}");

        using var mixed = JsonDocument.Parse("[\"https://example.com/one\",7]");
        test.Check("D12", "mixed native array is rejected explicitly",
            !DigestInputNormalizer.TryNormalize(mixed.RootElement, out _, out var mixedError)
            && mixedError == "urls array entries must all be URL strings.",
            $"error={mixedError}");

        using var nested = JsonDocument.Parse("[[\"https://example.com/one\"]]");
        test.Check("D12", "nested native array is rejected explicitly",
            !DigestInputNormalizer.TryNormalize(nested.RootElement, out _, out var nestedError)
            && nestedError == "urls array entries must all be URL strings.",
            $"error={nestedError}");

        using var empty = JsonDocument.Parse("[]");
        test.Check("D12", "empty native array is rejected explicitly",
            !DigestInputNormalizer.TryNormalize(empty.RootElement, out _, out var emptyError)
            && emptyError == "urls array is empty.",
            $"error={emptyError}");

        using var wrongShape = JsonDocument.Parse("{\"url\":\"https://example.com/\"}");
        test.Check("D12", "wrong top-level shape is rejected explicitly",
            !DigestInputNormalizer.TryNormalize(wrongShape.RootElement, out _, out var shapeError)
            && shapeError == "urls must be a string or an array of URL strings.",
            $"error={shapeError}");

        var tooLong = JsonSerializer.SerializeToElement(
            "https://example.com/" + new string('a', DigestInputNormalizer.MaxInputCharacters));
        test.Check("D12", "oversized legacy input is rejected at normalization",
            !DigestInputNormalizer.TryNormalize(tooLong, out _, out var lengthError)
            && lengthError?.Contains("input limit", StringComparison.Ordinal) == true,
            $"error={lengthError}");
    }
}
