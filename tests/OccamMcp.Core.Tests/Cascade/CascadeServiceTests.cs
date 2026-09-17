using OccamMcp.Core.Cascade;
using OccamMcp.Core.Playbooks;
using OccamMcp.Core.Routing;
using Xunit;

namespace OccamMcp.Core.Tests.Cascade;

public sealed class CascadeServiceTests
{
    [Fact]
    public async Task HttpSuccessSkipsBrowserAndRecordsSteps()
    {
        var backend = new Fake(
            Miss(),
            new CascadeExtractAttempt(true, "# Body", "http", null, null),
            new CascadeExtractAttempt(false, null, "browser", "unused", null),
            receiptsEnabled: false);
        var result = await new CascadeService(backend).RunAsync(
            new CascadeRequest("https://example.com"));

        Assert.True(result.Ok);
        Assert.False(result.Partial);
        Assert.Equal("http", result.BackendUsed);
        Assert.Contains(result.Steps, s => s.Kind == CascadeStepKind.HttpExtract && s.Status == CascadeStepStatus.Ok);
        Assert.Contains(result.Steps, s => s.Kind == CascadeStepKind.BrowserFallback && s.Status == CascadeStepStatus.Skipped);
    }

    [Fact]
    public async Task BrowserDegradesWhenHttpFails()
    {
        var backend = new Fake(
            Miss(),
            new CascadeExtractAttempt(false, null, "http", "thin_extract", "thin"),
            new CascadeExtractAttempt(true, "# BrowserBody", "browser", null, null),
            receiptsEnabled: true);
        var result = await new CascadeService(backend).RunAsync(
            new CascadeRequest("https://example.com"));

        Assert.True(result.Ok);
        Assert.Equal("browser", result.BackendUsed);
        Assert.Contains(result.Steps, s => s.Kind == CascadeStepKind.BrowserFallback && s.Status == CascadeStepStatus.Degraded);
        Assert.Contains(result.Steps, s => s.Kind == CascadeStepKind.Receipt && s.Status == CascadeStepStatus.Ok);
    }

    [Fact]
    public async Task HttpTimeoutOmitsStepAndSetsPartial()
    {
        var backend = new Fake(
            Miss(),
            new CascadeExtractAttempt(true, "late", "http", null, null),
            new CascadeExtractAttempt(true, "# FromBrowser", "browser", null, null),
            receiptsEnabled: false,
            httpDelayMs: 400);
        var result = await new CascadeService(backend).RunAsync(
            new CascadeRequest(
                "https://example.com",
                Timeouts: CascadeTimeouts.Fast with { HttpMs = 20, BrowserMs = 500, TotalMs = 2_000 }));

        Assert.True(result.Ok);
        Assert.True(result.Partial);
        Assert.Contains(CascadeStepKindStrings.HttpExtract, result.Omitted);
        Assert.Contains("FromBrowser", result.Markdown);
    }

    [Fact]
    public async Task RejectsInvalidUrlAndTinyBudget()
    {
        var backend = new Fake(
            Miss(),
            new CascadeExtractAttempt(true, "x", "http", null, null),
            new CascadeExtractAttempt(true, "x", "browser", null, null),
            false);

        var badUrl = await new CascadeService(backend).RunAsync(new CascadeRequest("ftp://x"));
        Assert.False(badUrl.Ok);
        Assert.Equal("invalid_arguments", badUrl.FailureCode);

        var badBudget = await new CascadeService(backend).RunAsync(
            new CascadeRequest("https://example.com", Budget: 10));
        Assert.False(badBudget.Ok);
        Assert.Equal("invalid_arguments", badBudget.FailureCode);
    }

    [Fact]
    public async Task TaskAndBudgetMapOntoFocusOptions()
    {
        OccamTranscodeOptions? seen = null;
        var backend = new Fake(
            Miss(),
            new CascadeExtractAttempt(true, "# M", "http", null, null),
            new CascadeExtractAttempt(false, null, "browser", "x", null),
            false,
            onExtract: opts => seen = opts);

        var result = await new CascadeService(backend).RunAsync(
            new CascadeRequest("https://example.com", Task: "closures", Budget: 800, Timeouts: CascadeTimeouts.Fast));

        Assert.True(result.Ok);
        Assert.Equal("closures", result.FocusQueryApplied);
        Assert.Equal(800, result.MaxTokensApplied);
        Assert.NotNull(seen);
        Assert.True(seen!.FitMarkdown);
        Assert.Equal("closures", seen.FocusQuery);
        Assert.Equal(800, seen.MaxTokens);
    }

    [Fact]
    public void ResponseMapperEmitsCamelCaseEnvelope()
    {
        var result = new CascadeResult(
            true,
            false,
            "https://example.com",
            "# Hi",
            null,
            null,
            null,
            "http",
            null,
            null,
            "abc",
            "advanced",
            [new CascadeStep(CascadeStepKind.HttpExtract, CascadeStepStatus.Ok, 1, "http")],
            []);
        var json = CascadeResponseMapper.Serialize(result);
        Assert.Contains("\"ok\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"steps\":", json, StringComparison.Ordinal);
        Assert.Contains("\"hint\":", json, StringComparison.Ordinal);
    }

    private static PlaybookSeedResolveResult Miss() =>
        new(false, "https://example.com", null, null, null, null, null, null, null, null, "not_found", "no");

    private sealed class Fake(
        PlaybookSeedResolveResult playbook,
        CascadeExtractAttempt http,
        CascadeExtractAttempt browser,
        bool receiptsEnabled,
        int httpDelayMs = 0,
        Action<OccamTranscodeOptions>? onExtract = null) : ICascadeExtractBackend
    {
        public bool ReceiptsEnabled => receiptsEnabled;

        public PlaybookSeedResolveResult ResolvePlaybook(string url) => playbook;

        public async ValueTask<CascadeExtractAttempt> ExtractAsync(
            string url,
            OccamBackendPolicy policy,
            OccamTranscodeOptions options,
            CancellationToken cancellationToken)
        {
            onExtract?.Invoke(options);
            if (httpDelayMs > 0 && policy == OccamBackendPolicy.Http)
            {
                await Task.Delay(httpDelayMs, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return policy == OccamBackendPolicy.Http ? http : browser;
        }

        public string? ComputeContentHash(string markdown) => "hash";
    }
}
