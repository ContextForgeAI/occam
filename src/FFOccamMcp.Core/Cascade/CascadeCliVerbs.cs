using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OccamMcp.Core.Composition;
using OccamMcp.Core.Playbooks;
using OccamMcp.Core.Routing;

namespace OccamMcp.Core.Cascade;

/// <summary>
/// The <c>occam cascade</c> verb family: prove the step machine without workers, and run a live
/// cascade when the host is configured.
/// </summary>
public static class CascadeCliVerbs
{
    /// <summary>Marker printed by a passing <c>cascade selftest</c>.</summary>
    public const string SelftestOkMarker = "CASCADE_SELFTEST_OK";

    public static bool TryRun(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0 || !string.Equals(args[0], "cascade", StringComparison.Ordinal))
        {
            return false;
        }

        switch (args.Length >= 2 ? args[1] : "help")
        {
            case "selftest":
                exitCode = Selftest();
                return true;
            case "run":
                exitCode = RunLive(args).GetAwaiter().GetResult();
                return true;
            default:
                PrintUsage();
                exitCode = 2;
                return true;
        }
    }

    public static int Selftest()
    {
        var failures = new List<string>();

        // Happy path: HTTP succeeds; browser skipped; focus applied; receipts off → receipt skipped.
        {
            var backend = new FakeCascadeBackend(
                playbook: new PlaybookSeedResolveResult(
                    true, "https://example.com", "example.com", "pb-example", "1", "seed", null, null, null, null, null, null),
                http: new CascadeExtractAttempt(true, "# Hi\n\nBody.", "http", null, null, "pb-example", "abc"),
                browser: new CascadeExtractAttempt(false, null, "browser", "should_not_run", null),
                receiptsEnabled: false);
            var service = new CascadeService(backend);
            var result = service.RunAsync(new CascadeRequest(
                "https://example.com",
                Task: "closures",
                Budget: 800,
                Mode: "auto",
                Timeouts: CascadeTimeouts.Fast)).GetAwaiter().GetResult();

            Check(failures, "happy path ok", result.Ok);
            Check(failures, "happy path not partial", !result.Partial);
            Check(failures, "happy path has markdown", result.Markdown?.Contains("Body", StringComparison.Ordinal) == true);
            Check(failures, "happy path focus applied", result.FocusQueryApplied == "closures");
            Check(failures, "happy path budget applied", result.MaxTokensApplied == 800);
            Check(failures, "happy path playbook ok",
                result.Steps.Any(s => s.Kind == CascadeStepKind.PlaybookResolve && s.Status == CascadeStepStatus.Ok));
            Check(failures, "happy path http ok",
                result.Steps.Any(s => s.Kind == CascadeStepKind.HttpExtract && s.Status == CascadeStepStatus.Ok));
            Check(failures, "happy path browser skipped",
                result.Steps.Any(s => s.Kind == CascadeStepKind.BrowserFallback && s.Status == CascadeStepStatus.Skipped));
            Check(failures, "happy path focus recorded",
                result.Steps.Any(s => s.Kind == CascadeStepKind.FocusBudget && s.Status == CascadeStepStatus.Ok));
            Check(failures, "happy path receipt skipped when disabled",
                result.Steps.Any(s => s.Kind == CascadeStepKind.Receipt && s.Status == CascadeStepStatus.Skipped));
        }

        // HTTP fails → browser degrades to success.
        {
            var backend = new FakeCascadeBackend(
                playbook: MissPlaybook(),
                http: new CascadeExtractAttempt(false, null, "http", "thin_extract", "thin"),
                browser: new CascadeExtractAttempt(true, "# Recovered", "browser", null, null),
                receiptsEnabled: true);
            var result = new CascadeService(backend).RunAsync(new CascadeRequest(
                "https://example.com",
                Timeouts: CascadeTimeouts.Fast)).GetAwaiter().GetResult();

            Check(failures, "degrade ok", result.Ok);
            Check(failures, "degrade backend browser", result.BackendUsed == "browser");
            Check(failures, "degrade step status",
                result.Steps.Any(s => s.Kind == CascadeStepKind.BrowserFallback && s.Status == CascadeStepStatus.Degraded));
            Check(failures, "degrade receipt ok",
                result.Steps.Any(s => s.Kind == CascadeStepKind.Receipt && s.Status == CascadeStepStatus.Ok));
        }

        // HTTP times out → omitted; browser still tries.
        {
            var backend = new FakeCascadeBackend(
                playbook: MissPlaybook(),
                httpDelayMs: 500,
                http: new CascadeExtractAttempt(true, "late", "http", null, null),
                browser: new CascadeExtractAttempt(true, "# FromBrowser", "browser", null, null),
                receiptsEnabled: false);
            var result = new CascadeService(backend).RunAsync(new CascadeRequest(
                "https://example.com",
                Timeouts: CascadeTimeouts.Fast with { HttpMs = 20, BrowserMs = 200, TotalMs = 2_000 }))
                .GetAwaiter().GetResult();

            Check(failures, "http timeout still ok via browser", result.Ok);
            Check(failures, "http timeout omitted", result.Omitted.Contains(CascadeStepKindStrings.HttpExtract));
            Check(failures, "http timeout marks partial", result.Partial);
            Check(failures, "browser filled in", result.Markdown?.Contains("FromBrowser", StringComparison.Ordinal) == true);
        }

        // Both extract stages fail → typed failure with step log.
        {
            var backend = new FakeCascadeBackend(
                playbook: MissPlaybook(),
                http: new CascadeExtractAttempt(false, null, "http", "http_404", "gone"),
                browser: new CascadeExtractAttempt(false, null, "browser", "http_404", "gone"),
                receiptsEnabled: false);
            var result = new CascadeService(backend).RunAsync(new CascadeRequest(
                "https://example.com/missing",
                Timeouts: CascadeTimeouts.Fast)).GetAwaiter().GetResult();

            Check(failures, "double fail not ok", !result.Ok);
            Check(failures, "double fail has code", !string.IsNullOrWhiteSpace(result.FailureCode));
            Check(failures, "double fail has steps", result.Steps.Count >= 4);
        }

        // Invalid URL / budget.
        {
            var backend = new FakeCascadeBackend(MissPlaybook(),
                new CascadeExtractAttempt(true, "x", "http", null, null),
                new CascadeExtractAttempt(true, "x", "browser", null, null),
                false);
            var badUrl = new CascadeService(backend).RunAsync(new CascadeRequest("not-a-url")).GetAwaiter().GetResult();
            Check(failures, "bad url rejected", !badUrl.Ok && badUrl.FailureCode == "invalid_arguments");

            var badBudget = new CascadeService(backend).RunAsync(new CascadeRequest(
                "https://example.com", Budget: 10)).GetAwaiter().GetResult();
            Check(failures, "tiny budget rejected", !badBudget.Ok && badBudget.FailureCode == "invalid_arguments");
        }

        // JSON mapper produces camelCase envelope.
        {
            var backend = new FakeCascadeBackend(
                MissPlaybook(),
                new CascadeExtractAttempt(true, "# M", "http", null, null, null, "deadbeef"),
                new CascadeExtractAttempt(false, null, "browser", "x", null),
                receiptsEnabled: true);
            var result = new CascadeService(backend).RunAsync(new CascadeRequest(
                "https://example.com", Mode: "advanced", Timeouts: CascadeTimeouts.Fast))
                .GetAwaiter().GetResult();
            var json = CascadeResponseMapper.Serialize(result);
            using var doc = JsonDocument.Parse(json);
            Check(failures, "json has ok", doc.RootElement.GetProperty("ok").GetBoolean());
            Check(failures, "json has steps", doc.RootElement.GetProperty("steps").GetArrayLength() > 0);
            Check(failures, "json advanced hint",
                doc.RootElement.TryGetProperty("hint", out var hint) && hint.GetString()?.Contains("advanced", StringComparison.Ordinal) == true);
        }

        if (failures.Count == 0)
        {
            Console.Out.WriteLine(SelftestOkMarker);
            return 0;
        }

        foreach (var f in failures)
        {
            Console.Error.WriteLine($"cascade selftest FAIL: {f}");
        }

        return 1;
    }

    private static async Task<int> RunLive(string[] args)
    {
        string? url = null;
        string? task = null;
        int? budget = null;
        var mode = "auto";
        for (var i = 2; i < args.Length; i++)
        {
            var a = args[i];
            if (a.StartsWith("--url=", StringComparison.Ordinal))
            {
                url = a["--url=".Length..];
            }
            else if (a.StartsWith("--task=", StringComparison.Ordinal))
            {
                task = a["--task=".Length..];
            }
            else if (a.StartsWith("--budget=", StringComparison.Ordinal)
                     && int.TryParse(a["--budget=".Length..], out var b))
            {
                budget = b;
            }
            else if (a.StartsWith("--mode=", StringComparison.Ordinal))
            {
                mode = a["--mode=".Length..];
            }
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            Console.Error.WriteLine("usage: occam cascade run --url=https://… [--task=…] [--budget=N] [--mode=auto|advanced]");
            return 2;
        }

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddOccamCore();
        await using var provider = services.BuildServiceProvider();
        var cascade = provider.GetRequiredService<CascadeService>();
        var result = await cascade.RunAsync(new CascadeRequest(url, task, budget, mode)).ConfigureAwait(false);
        Console.Out.Write(CascadeResponseMapper.Serialize(result));
        return result.Ok ? 0 : 1;
    }

    private static PlaybookSeedResolveResult MissPlaybook() =>
        new(false, "https://example.com", null, null, null, null, null, null, null, null, "not_found", "no playbook");

    private static void Check(List<string> failures, string label, bool ok)
    {
        if (!ok)
        {
            failures.Add(label);
        }
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine(
            """
            usage:
              occam cascade selftest
              occam cascade run --url=https://… [--task=…] [--budget=N] [--mode=auto|advanced]
            """);
    }

    /// <summary>Deterministic backend for selftests — optional per-call delay to exercise timeouts.</summary>
    private sealed class FakeCascadeBackend(
        PlaybookSeedResolveResult playbook,
        CascadeExtractAttempt http,
        CascadeExtractAttempt browser,
        bool receiptsEnabled,
        int httpDelayMs = 0,
        int browserDelayMs = 0) : ICascadeExtractBackend
    {
        public bool ReceiptsEnabled { get; } = receiptsEnabled;

        public PlaybookSeedResolveResult ResolvePlaybook(string url) => playbook;

        public async ValueTask<CascadeExtractAttempt> ExtractAsync(
            string url,
            OccamBackendPolicy policy,
            OccamTranscodeOptions options,
            CancellationToken cancellationToken)
        {
            var delay = policy == OccamBackendPolicy.Http ? httpDelayMs : browserDelayMs;
            if (delay > 0)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return policy == OccamBackendPolicy.Http ? http : browser;
        }

        public string? ComputeContentHash(string markdown) =>
            string.IsNullOrEmpty(markdown) ? null : "fakehash";
    }
}
