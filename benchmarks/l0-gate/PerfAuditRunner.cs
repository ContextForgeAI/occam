using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using OccamMcp.Core.Receipts;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Tools;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

/// <summary>
/// Host-side performance audit: network/parse/compile/sign + RSS/PDF/browser cases.
/// Invoked via <c>--perf-audit</c>. Writes JSON under <c>artifacts/perf/</c>.
/// </summary>
internal static class PerfAuditRunner
{
    private static readonly (string Id, string Url, string Backend, bool JsonFeed)[] Cases =
    [
        ("html-example", "https://example.com/", "http", false),
        ("html-mdn", "https://developer.mozilla.org/en-US/docs/Web/HTTP", "http", false),
        ("rss-hn-nofeed", "https://hnrss.org/frontpage", "http", false),
        ("rss-hn-jsonfeed", "https://hnrss.org/frontpage", "http", true),
        ("pdf-w3c-dummy", "https://www.w3.org/WAI/ER/tests/xhtml/testfiles/resources/pdf/dummy.pdf", "http", false),
        ("browser-mdn", "https://developer.mozilla.org/en-US/docs/Web/HTTP", "browser", false),
    ];

    public static int Run(TranscodePipeline pipeline, string? outDirArg = null)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var home = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory();
        var outDir = string.IsNullOrWhiteSpace(outDirArg)
            ? Path.Combine(home, "artifacts", "perf", $"host-{stamp}")
            : outDirArg;
        Directory.CreateDirectory(outDir);

        Console.WriteLine($"PERF_AUDIT out: {outDir}");

        // Warm HTTP daemon once.
        _ = pipeline.Transcode(
            "https://example.com/",
            OccamBackendPolicy.Http,
            OccamTranscodeOptions.Default,
            CancellationToken.None);

        var signer = ReceiptSigner.CreateEphemeral();
        var results = new List<object>();

        foreach (var c in Cases)
        {
            if (!OccamBackendPolicyParser.TryParse(c.Backend, out var policy))
            {
                Console.WriteLine($"  SKIP {c.Id}: bad backend");
                continue;
            }

            var opts = OccamTranscodeOptions.Default with { JsonFeed = c.JsonFeed };
            var runs = new List<object>();
            for (var i = 0; i < 2; i++)
            {
                var sw = Stopwatch.StartNew();
                var outcome = pipeline.Transcode(c.Url, policy, opts, CancellationToken.None);
                sw.Stop();

                var signSw = Stopwatch.StartNew();
                if (outcome.Ok && !string.IsNullOrEmpty(outcome.Markdown))
                {
                    _ = OccamTranscodeResponseBuilder.BuildReceipt(outcome, c.Url, signer);
                }
                else
                {
                    // Isolated ECDSA microbench when extract failed.
                    var env = new ReceiptEnvelope(
                        ReceiptEnvelope.CurrentVersion,
                        ReceiptEnvelope.KindNegative,
                        c.Url,
                        outcome.FinalUrl ?? c.Url,
                        outcome.Backend ?? c.Backend,
                        DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                        ReceiptSigner.Toolchain,
                        Playbook: null,
                        ContentHash: null,
                        BlockMerkleRoot: null,
                        Tokens: null,
                        FailureCode: outcome.FailureCode ?? "extraction_failed",
                        StatusCode: outcome.StatusCode,
                        Confidence: null,
                        KeyId: string.Empty,
                        Alg: string.Empty,
                        Sig: null);
                    _ = signer.Sign(env);
                }

                signSw.Stop();
                var signMs = (int)signSw.ElapsedMilliseconds;

                var t = outcome.Timings;
                var feedItems = outcome.Feed?.Items?.Length;
                var row = new Dictionary<string, object?>
                {
                    ["run"] = i + 1,
                    ["wallMs"] = sw.ElapsedMilliseconds,
                    ["ok"] = outcome.Ok,
                    ["failure"] = outcome.FailureCode,
                    ["backend"] = outcome.Backend,
                    ["mdLen"] = outcome.Markdown?.Length ?? 0,
                    ["feedItems"] = feedItems,
                    ["workerNetworkMs"] = outcome.WorkerNetworkMs,
                    ["workerParseMs"] = outcome.WorkerParseMs,
                    ["timings"] = t is null
                        ? null
                        : new Dictionary<string, int>
                        {
                            ["totalMs"] = t.TotalMs,
                            ["preflightMs"] = t.PreflightMs,
                            ["routeMs"] = t.RouteMs,
                            ["networkMs"] = t.NetworkMs,
                            ["parseMs"] = t.ParseMs,
                            ["postProcessMs"] = t.PostProcessMs,
                            ["compileMs"] = t.CompileMs,
                        },
                    ["signMs"] = signMs,
                };
                runs.Add(row);
                Console.WriteLine(
                    $"  {c.Id} run={i + 1} ok={outcome.Ok} wall={sw.ElapsedMilliseconds}ms " +
                    $"net={t?.NetworkMs ?? outcome.WorkerNetworkMs} parse={t?.ParseMs ?? outcome.WorkerParseMs} " +
                    $"compile={t?.CompileMs ?? -1} post={t?.PostProcessMs ?? -1} sign={signMs}ms " +
                    $"fail={outcome.FailureCode ?? "-"} md={outcome.Markdown?.Length ?? 0} feed={feedItems}");
            }

            results.Add(new Dictionary<string, object?>
            {
                ["id"] = c.Id,
                ["url"] = c.Url,
                ["backend"] = c.Backend,
                ["jsonFeed"] = c.JsonFeed,
                ["runs"] = runs,
            });
        }

        var payload = new Dictionary<string, object?>
        {
            ["stamp"] = stamp,
            ["machine"] = Environment.MachineName,
            ["os"] = Environment.OSVersion.ToString(),
            ["cases"] = results,
        };
        var jsonPath = Path.Combine(outDir, "host-perf-audit.json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"PERF_AUDIT_JSON: {jsonPath}");
        Console.WriteLine("PERF_AUDIT_OK");
        return 0;
    }
}
