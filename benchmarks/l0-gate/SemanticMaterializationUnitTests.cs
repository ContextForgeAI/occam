using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OccamMcp.Core.Abstractions;
using OccamMcp.Core.Compile;
using OccamMcp.Core.Composition;
using OccamMcp.Core.Digest;
using OccamMcp.Core.Knowledge;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Services;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

internal static class SemanticMaterializationUnitTests
{
    private const int AdequateBudget = 650;
    private const int TightBudget = 128;

    internal static string ReadFixture(string fileName)
    {
        var path = ResolveSemanticPath(fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Semantic fixture missing: {path}");
        return File.ReadAllText(path);
    }

    public static void Run(Action<string, bool> assert)
    {
        var manifestPath = ResolveSemanticPath("manifest.jsonl");
        assert("semantic manifest exists", File.Exists(manifestPath));
        var cases = File.ReadAllLines(manifestPath)
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Select(ParseCase)
            .ToArray();
        assert("semantic manifest has pinned cases", cases.Length >= 6);

        foreach (var testCase in cases)
        {
            var markdown = ReadFixture(testCase.File);
            RunCompilerMatrix(assert, testCase, markdown);
            RunPipelineParity(assert, testCase, markdown);
        }
    }

    private static void RunCompilerMatrix(Action<string, bool> assert, SemanticCase testCase, string markdown)
    {
        var focused = TranscodeCompiler.Apply(markdown, new OccamTranscodeOptions
        {
            MaxTokens = AdequateBudget, FitMarkdown = true, FocusQuery = testCase.Focus,
        });
        var prefix = $"semantic {testCase.Id} focus+fit@{AdequateBudget}";
        assert(prefix + " stays in budget", focused.TokensEstimated <= AdequateBudget);
        foreach (var needle in testCase.MustContain)
            assert(prefix + " retains " + Short(needle), focused.Markdown.Contains(needle, StringComparison.Ordinal));
        if (focused.Markdown.Length < markdown.Length)
        {
            foreach (var noise in testCase.MustExclude)
                assert(prefix + " drops " + Short(noise), !focused.Markdown.Contains(noise, StringComparison.Ordinal));
        }

        var tight = TranscodeCompiler.Apply(markdown, new OccamTranscodeOptions
        {
            MaxTokens = TightBudget, FitMarkdown = true, FocusQuery = testCase.Focus,
        });
        var tightLabel = $"semantic {testCase.Id} focus+fit@{TightBudget}";
        assert(tightLabel + " stays in budget", tight.TokensEstimated <= TightBudget);
        var sourceTokens = TokenEstimator.Estimate(markdown);
        if (sourceTokens > TightBudget)
            assert(tightLabel + " honest omission", tight.Omitted is { TokensDropped: > 0 } || tight.Truncated);
        assert(tightLabel + " no orphan instruction heading only",
            !HasOrphanInstructionHeading(tight.Markdown, testCase.MustContain));

        var plain = TranscodeCompiler.Apply(markdown, new OccamTranscodeOptions
        {
            MaxTokens = AdequateBudget, FitMarkdown = false, FocusQuery = null,
        });
        var plainLabel = $"semantic {testCase.Id} no-focus@{AdequateBudget}";
        foreach (var neighbor in testCase.NeighborPresentWithoutFocus)
            assert(plainLabel + " keeps neighbor " + Short(neighbor),
                plain.Markdown.Contains(neighbor, StringComparison.Ordinal));
    }

    private static void RunPipelineParity(Action<string, bool> assert, SemanticCase testCase, string markdown)
    {
        var services = new ServiceCollection().AddOccamCore();
        services.RemoveAll<IExtractBackend>();
        services.AddSingleton<IExtractBackend>(new FixtureBackend(markdown));
        services.AddSingleton<IRobotsThrottleService>(new NoRobots());
        using var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<TranscodePipeline>();
        var digest = provider.GetRequiredService<DigestService>();
        var url = "https://example.com/semantic/" + testCase.Id;
        foreach (var (focus, fit, budget) in new (string?, bool, int)[]
        {
            (null, false, AdequateBudget),
            (testCase.Focus, true, AdequateBudget),
            (testCase.Focus, true, TightBudget),
        })
        {
            var single = pipeline.TranscodeAsync(url, OccamBackendPolicy.Http,
                new OccamTranscodeOptions { MaxTokens = budget, FocusQuery = focus, FitMarkdown = fit },
                CancellationToken.None).AsTask().GetAwaiter().GetResult();
            var many = digest.DigestAsync([new DigestUrlEntry(url)], perUrlMaxTokens: budget,
                backendPolicy: OccamBackendPolicy.Http, focusQuery: focus, fitMarkdown: fit)
                .AsTask().GetAwaiter().GetResult();
            var item = many.Items.Single();
            var label = $"semantic pipeline {testCase.Id} focus={focus is not null} fit={fit} budget={budget}";
            assert(label + " succeeds", single.Ok && item.Ok);
            assert(label + " equivalent content", single.Markdown == item.Excerpt);
            assert(label + " equivalent metadata", single.MaterializationAssessment == item.MaterializationAssessment);
            assert(label + " budget", single.TokensEstimated <= budget && item.TokensEstimated <= budget);
            if (focus is not null && budget == AdequateBudget)
            {
                foreach (var needle in testCase.MustContain)
                    assert(label + " retains " + Short(needle),
                        single.Markdown!.Contains(needle, StringComparison.Ordinal));
            }
            else if (focus is not null && budget == TightBudget)
            {
                var sourceTokens = TokenEstimator.Estimate(markdown);
                if (sourceTokens > TightBudget)
                    assert(label + " honest loss", single.Omitted is not null || single.Truncated);
                if (single.MaterializationAssessment?.Completeness == MaterializationCompleteness.Incomplete)
                    assert(label + " useful retry", single.MaterializationAssessment.SuggestedMinTokens > budget);
            }
        }
    }

    private static bool HasOrphanInstructionHeading(string markdown, IReadOnlyList<string> mustContain)
    {
        var firstHeading = markdown.Split('\n').FirstOrDefault(static line => line.StartsWith("# ", StringComparison.Ordinal));
        if (firstHeading is null)
            return false;
        var body = markdown[(markdown.IndexOf('\n') + 1)..];
        return mustContain.All(needle => !body.Contains(needle, StringComparison.Ordinal));
    }

    private static string ResolveSemanticPath(string fileName)
    {
        var output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "fixtures", "semantic", fileName));
        if (File.Exists(output))
            return output;
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "fixtures", "semantic", fileName));
    }

    private static SemanticCase ParseCase(string line)
    {
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        return new SemanticCase(
            root.GetProperty("id").GetString()!,
            root.GetProperty("file").GetString()!,
            root.GetProperty("focus").GetString()!,
            ReadStringArray(root.GetProperty("mustContain")),
            ReadStringArray(root.GetProperty("mustExclude")),
            ReadStringArray(root.GetProperty("neighborPresentWithoutFocus")));
    }

    private static string[] ReadStringArray(JsonElement element) =>
        element.EnumerateArray().Select(static item => item.GetString()!).ToArray();

    private static string Short(string value) =>
        value.Length <= 40 ? value : value[..40];

    private sealed record SemanticCase(
        string Id,
        string File,
        string Focus,
        string[] MustContain,
        string[] MustExclude,
        string[] NeighborPresentWithoutFocus);

    private sealed class FixtureBackend(string markdown) : IExtractBackend
    {
        public string Name => "http";
        public bool IsReady => true;
        public ValueTask<ExtractRunResult> ExtractAsync(string url, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ExtractRunResult(true, markdown, "fixture", null, 0, url, false, StatusCode: 200));
    }

    private sealed class NoRobots : IRobotsThrottleService
    {
        public string? CheckAndThrottle(string url, CancellationToken cancellationToken) => null;
    }
}
