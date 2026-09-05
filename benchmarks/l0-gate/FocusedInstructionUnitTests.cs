using OccamMcp.Core.Compile;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Abstractions;
using OccamMcp.Core.Composition;
using OccamMcp.Core.Digest;
using OccamMcp.Core.Knowledge;
using OccamMcp.Core.Services;
using OccamMcp.Core.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace OccamMcp.L0Gate;

internal static class FocusedInstructionUnitTests
{
    // Frozen extraction surface: no network, domain rules, or command-specific matching.
    internal const string Fixture = """
        # Automatic connection

        [](https://example.com/edit "Edit this page")

        `relay attach`

        For validated hosts detected with sufficient confidence, connect MCP configuration:

        1. Builds the launch specification with the selected runtime. Resolves absolute paths and carries the required environment into the child process without changing the parent shell.
        2. Registers the managed server name. Existing unrelated entries remain untouched and an owned entry is replaced only after validation of the generated document succeeds.
        3. Verifies the saved file and available tools. If the application cannot be queried directly, the saved document is read again and compared with the intended values.
        4. Prints the status for each host. A failure remains visible in the final report together with the exact next action needed to finish the registration.

        The installer runs this for you after setup completes.

        Astronomers observe distant galaxies through enormous optical telescopes.

        ## Safety reminders

        * Existing unmanaged entries are left alone.
        * Backup before atomic write.
        * Build agents leave desktop files alone.

        Details about MCP configuration are available in the operator handbook.

        ## Gardening

        Water the roses every morning during the warm summer months.
        """;

    public static void Run(Action<string, bool> assert)
    {
        var result = TranscodeCompiler.Apply(Fixture, new OccamTranscodeOptions
        {
            MaxTokens = 650, FitMarkdown = true, FocusQuery = "connect MCP configuration",
        });
        assert("focused instruction retains short command", result.Markdown.Contains("`relay attach`"));
        assert("focused instruction retains complete dependent list", result.Markdown.Contains("4. Prints the status"));
        assert("focused instruction retains safety body", result.Markdown.Contains("Backup before atomic write"));
        assert("focused instruction prunes unrelated section", !result.Markdown.Contains("Water the roses"));
        assert("focused instruction prunes unrelated paragraph", !result.Markdown.Contains("Astronomers"));
        assert("focused instruction reports filtering", result.Omitted is { TokensDropped: > 0 });
        assert("focused instruction reports partial section removal", result.Markdown.Split("reason: focus_filtered").Length >= 3);
        var steps = FitMarkdown.Apply("# Setup\n\n1. Connect MCP configuration.\n2. Restart the application.\n\n## Plants\n\nWater the roses every morning.\n\nAdd fertilizer during spring.", "connect MCP configuration");
        assert("focused instruction list seed preserves other steps", steps.Contains("2. Restart the application."));
        RunPipeline(assert);
    }

    private static void RunPipeline(Action<string, bool> assert)
    {
        var services = new ServiceCollection().AddOccamCore();
        services.RemoveAll<IExtractBackend>();
        services.AddSingleton<IExtractBackend>(new FixtureBackend());
        services.AddSingleton<IRobotsThrottleService>(new NoRobots());
        using var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<TranscodePipeline>();
        var digest = provider.GetRequiredService<DigestService>();
        const string url = "https://example.com/frozen-instruction";
        foreach (var (focus, fit, budget) in new (string?, bool, int)[]
        {
            (null, false, 650), ("connect MCP configuration", false, 650),
            ("connect MCP configuration", true, 650), ("connect MCP configuration", true, 128),
        })
        {
            var single = pipeline.TranscodeAsync(url, OccamBackendPolicy.Http,
                new OccamTranscodeOptions { MaxTokens = budget, FocusQuery = focus, FitMarkdown = fit },
                CancellationToken.None).AsTask().GetAwaiter().GetResult();
            var many = digest.DigestAsync([new DigestUrlEntry(url)], perUrlMaxTokens: budget,
                backendPolicy: OccamBackendPolicy.Http, focusQuery: focus, fitMarkdown: fit)
                .AsTask().GetAwaiter().GetResult();
            var item = many.Items.Single();
            var label = $"focused pipeline focus={focus is not null} fit={fit} budget={budget}";
            assert(label + " succeeds", single.Ok && item.Ok);
            assert(label + " equivalent content", single.Markdown == item.Excerpt);
            assert(label + " equivalent metadata", single.MaterializationAssessment == item.MaterializationAssessment);
            assert(label + " budget", single.TokensEstimated <= budget && item.TokensEstimated <= budget);
            if (budget == 650)
            {
                assert(label + " meaning retained", single.Markdown!.Contains("`relay attach`")
                    && single.Markdown.Contains("4. Prints the status")
                    && single.Markdown.Contains("Backup before atomic write"));
                assert(label + " complete", single.MaterializationAssessment?.Completeness == MaterializationCompleteness.Complete);
            }
            else
            {
                assert(label + " no dangling introduction", !single.Markdown!.Contains("confidence, connect MCP configuration:"));
                assert(label + " honest loss", single.Omitted is not null
                    && single.MaterializationAssessment?.Completeness == MaterializationCompleteness.Incomplete);
                assert(label + " useful retry", single.MaterializationAssessment?.SuggestedMinTokens > budget);
            }
        }
    }

    private sealed class FixtureBackend : IExtractBackend
    {
        public string Name => "http";
        public bool IsReady => true;
        public ValueTask<ExtractRunResult> ExtractAsync(string url, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ExtractRunResult(true, Fixture, "fixture", null, 0, url, false, StatusCode: 200));
    }

    private sealed class NoRobots : IRobotsThrottleService
    {
        public string? CheckAndThrottle(string url, CancellationToken cancellationToken) => null;
    }
}
