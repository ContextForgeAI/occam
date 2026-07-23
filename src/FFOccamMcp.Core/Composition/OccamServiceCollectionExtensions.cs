using OccamMcp.Core.Abstractions;
using OccamMcp.Core.Backends;
using OccamMcp.Core.Playbooks;
using OccamMcp.Core.PostProcessors;
using OccamMcp.Core.Probe;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Services;
using OccamMcp.Core.Telemetry;
using OccamMcp.Core.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;

namespace OccamMcp.Core.Composition;

public static class OccamServiceCollectionExtensions
{
    public static IServiceCollection AddOccamCore(this IServiceCollection services)
    {
        var workerPaths = WorkerPaths.Resolve();
        services.AddSingleton(workerPaths);
        // Receipt v1 signer — one local ECDsa key per host, loaded/generated on first use.
        services.AddSingleton(_ => OccamMcp.Core.Receipts.ReceiptSigner.LoadOrCreate());
        // LLM client context budget — agent declares via occam_client_capabilities or OCCAM_CLIENT_CONTEXT_TOKENS.
        services.AddSingleton<OccamMcp.Core.Client.ClientCapabilityStore>();
        // SI-15 time-anchor producer — self-gates on OCCAM_TIME_ANCHOR + OCCAM_TSA_URL (off by default).
        services.AddSingleton<OccamMcp.Core.Receipts.TimeAnchorService>();
        services.AddSingleton<IHttpExtractRunner, HttpExtractRunner>();
        services.AddSingleton<IBrowserExtractRunner, BrowserExtractRunner>();
        services.AddSingleton<IWorkerProcessSpawner, NodeWorkerProcessSpawner>();
        services.AddSingleton<IExtractBackend, HttpExtractBackend>();
        services.AddSingleton<IExtractBackend, BrowserExtractBackend>();
        services.AddSingleton<OccamRouter>();
        services.AddSingleton<ITranscodePostProcessor, ChallengePagePostProcessor>();
        services.AddSingleton<ITranscodePostProcessor, RequiresLoginPostProcessor>();
        services.AddSingleton<ITranscodePostProcessor, ThinExtractPostProcessor>();
        services.AddSingleton<IOccamTelemetrySink, OccamLoggerTelemetrySink>();

        services.AddSingleton<IBrowserPoolManager>(sp =>
        {
            var telemetry = sp.GetRequiredService<IOccamTelemetrySink>();
            var browserDaemonClient = sp.GetRequiredService<IBrowserDaemonClient>();
            var manager = new BrowserPoolManager(BrowserPoolSettings.ReadFromEnvironment(), telemetry, browserDaemonClient);
            BrowserPoolManager.InstallShared(manager);
            return manager;
        });
        // SSRF guard (OutboundHttpGuard.ConnectAsync) on the clients that fetch user-influenced URLs
        // directly in-process (probe, genome) — resolves + rejects private IPs (v4/v6) and pins the
        // connection, also covering redirect targets. SocketsHttpHandler is required for ConnectCallback.
        services.AddHttpClient(HttpProbeFetcher.RedirectTrackingClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.All,
                ConnectCallback = OutboundHttpGuard.ConnectAsync,
            });
        services.AddHttpClient(HttpProbeFetcher.AutoRedirectClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.All,
                ConnectCallback = OutboundHttpGuard.ConnectAsync,
            });
        services.AddHttpClient("playbook.wellKnownGenome")
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.All,
                ConnectCallback = OutboundHttpGuard.ConnectAsync,
            });
        // SI-15 time anchor: operator-controlled TSA URL, so a short explicit timeout (default 3s) keeps
        // an opt-in anchor from blocking the success path, and the SSRF guard covers the outbound POST.
        services.AddHttpClient("receipts.timeAnchor", c => c.Timeout = TimeSpan.FromMilliseconds(
            OccamMcp.Core.Configuration.OccamEnvironment.GetInt("OCCAM_TSA_TIMEOUT_MS", defaultValue: 3_000, min: 500, max: 15_000)))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                ConnectCallback = OutboundHttpGuard.ConnectAsync,
            });
        services.AddHttpClient(Services.TranslationService.HttpClientName, c => c.Timeout = TimeSpan.FromMilliseconds(
            OccamMcp.Core.Configuration.OccamEnvironment.GetInt("OCCAM_TRANSLATE_TIMEOUT_MS", defaultValue: 20_000, min: 1_000, max: 120_000)));
        services.AddHttpClient(Backends.ManagedExtractBackend.HttpClientName, c => c.Timeout = TimeSpan.FromMilliseconds(
            OccamMcp.Core.Configuration.OccamEnvironment.GetInt("OCCAM_MANAGED_TIMEOUT_MS", defaultValue: 60_000, min: 1_000, max: 180_000)));
        services.AddSingleton<Backends.Managed.IManagedProvider, Backends.Managed.JinaProvider>();
        services.AddSingleton<Backends.Managed.IManagedProvider, Backends.Managed.FirecrawlProvider>();
        services.AddSingleton<Backends.Managed.IManagedProvider, Backends.Managed.SpiderProvider>();
        services.AddSingleton<Backends.Managed.IManagedProvider, Backends.Managed.ScrapflyProvider>();
        services.AddSingleton<Backends.IManagedExtractBackend, Backends.ManagedExtractBackend>();
        services.AddHttpClient(Services.SearchService.HttpClientName, c => c.Timeout = TimeSpan.FromMilliseconds(
            OccamMcp.Core.Configuration.OccamEnvironment.GetInt("OCCAM_SEARCH_TIMEOUT_MS", defaultValue: 20_000, min: 1_000, max: 120_000)));
        services.AddSingleton<Search.ISearchProvider, Search.SearxngProvider>();
        services.AddSingleton<Search.ISearchProvider, Search.BraveProvider>();
        services.AddSingleton<Search.ISearchProvider, Search.TavilyProvider>();
        services.AddSingleton<Services.ISearchService, Services.SearchService>();
        services.AddHttpClient(Services.RobotsThrottleService.HttpClientName, c => c.Timeout = TimeSpan.FromMilliseconds(
            OccamMcp.Core.Configuration.OccamEnvironment.GetInt("OCCAM_ROBOTS_TIMEOUT_MS", defaultValue: 10_000, min: 1_000, max: 60_000)))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                // robots.txt is fetched from the user-supplied origin → same SSRF guard.
                ConnectCallback = OutboundHttpGuard.ConnectAsync,
            });
        services.AddSingleton<Services.IRobotsThrottleService, Services.RobotsThrottleService>();
        // Dedicated long-ceiling HttpClient: per-request timing is the CancellationTokenSource
        // (daemon-wait / provision grace), so the client's own Timeout must only be a finite anti-hang
        // ceiling ABOVE the largest cts we ever set. The biggest is BrowserExtractTimeouts.MaxDaemonWaitMs
        // (15 min under raised OCCAM_BROWSER_MAX_PARALLEL); a fixed 10-min ceiling would fire first and
        // mis-attribute a truncated queue-wait as a timeout — so derive it from that max + margin. Singleton
        // → one reused socket.
        services.AddSingleton<IBrowserDaemonClient>(_ =>
            new BrowserDaemonClient(new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(Workers.BrowserExtractTimeouts.MaxDaemonWaitMs)
                    + TimeSpan.FromMinutes(2),
            }));
        services.AddSingleton<IProxyRotationService, RoundRobinProxyRotationService>();
        services.AddSingleton<HttpProbeFetcher>();
        services.AddSingleton<ProbeService>();
        services.AddSingleton<TranscodePipeline>();
        services.AddSingleton<DigestService>();
        services.AddSingleton<MapService>();
        // SI-16 claim-check — grounds a claim in provable source blocks (core tool).
        services.AddSingleton<Claims.IClaimCheckService, Claims.ClaimCheckService>();
        // SI-11 attest — batch-grounds an LLM report against its own citations (core tool).
        services.AddSingleton<Attest.IAttestService, Attest.AttestService>();
        // SI-17 dataset export — signed, auditable multi-URL dataset with a Merkle manifest (core tool).
        services.AddSingleton<Dataset.IDatasetExportService, Dataset.DatasetExportService>();
        services.AddSingleton<WellKnownGenomeFetcher>();
        services.AddSingleton<PlaybookSeedResolver>();
        services.AddSingleton<PlaybookHealService>();
        services.AddSingleton<PlaybookSaveVerifier>();
        services.AddSingleton<CssExtractWorker>();
        services.AddSingleton<KnowledgeExtractService>();
        services.AddSingleton<PlaybookSaveService>();
        services.AddSingleton<FeatureDiscoveryService>();
        services.AddSingleton<Services.ITranslationService, Services.TranslationService>();
        services.AddSingleton<Caching.ITranscodeResponseCache, Caching.FileTranscodeResponseCache>();
        // ADR-0001 / master PR-E: built-in codecs only via DI. Third-party OptInExtension codecs require
        // KnowledgeCodecExtensionOptions.AllowOptInExtensions + TryRegisterExtension (no assembly scan).
        // Live transcode resolves the configured default via KnowledgeCodecSelector (no MCP codec param).
        services.AddSingleton<Codecs.IKnowledgeCodec, Codecs.MarkdownPassthroughCodec>();
        services.AddSingleton<Codecs.IKnowledgeCodec, Codecs.CompactMarkdownCodec>();
        services.AddSingleton<Codecs.IKnowledgeCodec, Codecs.JsonKnowledgeCodec>();
        services.AddSingleton(Codecs.KnowledgeCodecExtensionOptions.Disabled);
        services.AddSingleton(sp => new Codecs.KnowledgeCodecRegistry(
            sp.GetServices<Codecs.IKnowledgeCodec>(),
            sp.GetRequiredService<Codecs.KnowledgeCodecExtensionOptions>(),
            defaultCodecId: Codecs.MarkdownPassthroughCodec.Id));
        // Materialization Planner owns semantic retention; live FinishMaterialize wires
        // Canonical → Planner → View → Codec.
        services.AddSingleton<Knowledge.MaterializationPlanner>();
        return services;
    }

    public static (WorkerPaths Paths, TranscodePipeline Pipeline, ProbeService Probe, DigestService Digest, MapService Map) BuildOccamCore()
    {
        var services = new ServiceCollection();
        services.AddOccamCore();
        var provider = services.BuildServiceProvider();
        return (
            provider.GetRequiredService<WorkerPaths>(),
            provider.GetRequiredService<TranscodePipeline>(),
            provider.GetRequiredService<ProbeService>(),
            provider.GetRequiredService<DigestService>(),
            provider.GetRequiredService<MapService>());
    }

    /// <summary>Resolves a standalone translation service (gate/diagnostics ad-hoc path).</summary>
    public static Services.ITranslationService BuildTranslationService()
    {
        var services = new ServiceCollection();
        services.AddOccamCore();
        return services.BuildServiceProvider().GetRequiredService<Services.ITranslationService>();
    }

    /// <summary>Resolves a standalone managed extraction backend (gate/diagnostics ad-hoc path).</summary>
    public static Backends.IManagedExtractBackend BuildManagedBackend()
    {
        var services = new ServiceCollection();
        services.AddOccamCore();
        return services.BuildServiceProvider().GetRequiredService<Backends.IManagedExtractBackend>();
    }

    /// <summary>Resolves a standalone search service (gate/diagnostics ad-hoc path).</summary>
    public static Services.ISearchService BuildSearchService()
    {
        var services = new ServiceCollection();
        services.AddOccamCore();
        return services.BuildServiceProvider().GetRequiredService<Services.ISearchService>();
    }
}
