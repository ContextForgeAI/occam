using OccamMcp.Core.Compile;
using OccamMcp.Core.Probe;
using OccamMcp.Core.Services;

namespace OccamMcp.L0Gate;

/// <summary>
/// Discovery focus-precision benchmark (offline fixtures).
/// Entity-first ranking: primary anchors beat supporting-term overlap; version roots lose.
/// </summary>
internal static class DiscoveryFocusUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        RunDecomposition(assert);
        RunLegacyBench(assert);
        RunPythonEntityFixture(assert);
        RunMdnFixture(assert);
        RunDotNetFixture(assert);
        RunK8sFixture(assert);
        RunNegativeGeneric(assert);
        RunVersionRootPenalty(assert);
        RunSharedRankerSurface(assert);

        Console.WriteLine("L_DISCOVERY_FOCUS_OK");
    }

    private static void RunDecomposition(Action<string, bool> assert)
    {
        var aio = FocusQueryDecomposition.Decompose("asyncio event loop tasks synchronization");
        assert("decomp: asyncio is primary", aio.PrimaryAnchors.Contains("asyncio", StringComparer.Ordinal));
        assert(
            "decomp: supporting has event/loop/tasks",
            aio.SupportingTerms.Contains("event", StringComparer.Ordinal)
            && aio.SupportingTerms.Contains("loop", StringComparer.Ordinal)
            && aio.SupportingTerms.Contains("tasks", StringComparer.Ordinal));

        var abort = FocusQueryDecomposition.Decompose("AbortController cancel fetch request");
        assert(
            "decomp: AbortController primary",
            abort.PrimaryAnchors.Any(a => a.Contains("abortcontroller", StringComparison.Ordinal)));

        var neg = FocusQueryDecomposition.Decompose("how to cancel a request safely");
        assert("decomp: negative has no primary", !neg.HasPrimaryAnchors);

        var unicode = FocusQueryDecomposition.Decompose("конфигурация синтаксис");
        assert("decomp: unicode tokens kept", unicode.AllTerms.Count >= 1);
    }

    private static void RunLegacyBench(Action<string, bool> assert)
    {
        var few = MapLinkRanker.Rank(
            [
                new MappedLink("https://docs.python.org/3/whatsnew/3.12.html", "What's new in Python 3.12", "/3/whatsnew/3.12.html"),
                new MappedLink("https://docs.python.org/3/library/asyncio.html", "asyncio — Asynchronous I/O", "/3/library/asyncio.html"),
            ],
            "asyncio",
            maxLinks: 8);
        assert("discovery: ranks when count ≤ max_links", few.Count == 2);
        assert(
            "discovery: asyncio first over whatsnew",
            few[0].Path.Contains("asyncio", StringComparison.OrdinalIgnoreCase));

        var pythonPool = BuildPythonPool();
        var ranked = MapLinkRanker.Rank(pythonPool, "asyncio", maxLinks: 3);
        assert("discovery bench: returns ≤3", ranked.Count == 3);
        assert(
            "discovery bench: top is asyncio module",
            ranked[0].Path.Contains("/library/asyncio", StringComparison.OrdinalIgnoreCase));
        assert(
            "discovery bench: top-3 exclude whatsnew versions",
            ranked.All(l => !l.Path.Contains("whatsnew", StringComparison.OrdinalIgnoreCase)));

        var scored = MapLinkRanker.RankScored(pythonPool, "asyncio");
        assert(
            "discovery bench: asyncio is strong hit",
            scored[0].Link.Path.Contains("asyncio", StringComparison.OrdinalIgnoreCase)
            && scored[0].Score >= MapLinkRanker.StrongHitThreshold);
        assert("discovery bench: HasStrongHit", MapLinkRanker.HasStrongHit(scored));

        var fieldPool = new List<MappedLink>
        {
            new("https://example.com/a", "Unrelated", "/a/other"),
            new("https://example.com/b", "Something", "/b/path", Context: "mentions asyncio briefly"),
            new("https://example.com/c", "asyncio helpers", "/c/helpers"),
            new("https://example.com/d", "Docs", "/library/asyncio/tasks"),
        };
        var fieldRanked = MapLinkRanker.Rank(fieldPool, "asyncio", 2);
        assert(
            "discovery: path phrase beats weak context",
            fieldRanked[0].Path.Contains("asyncio", StringComparison.OrdinalIgnoreCase));

        var html = """
            <html><body>
            <p>Intro text before.</p>
            <a href="/3/library/asyncio.html">asyncio — Asynchronous I/O</a>
            <p>Neighbor after about coroutines.</p>
            <a href="/3/whatsnew/3.12.html">What's new in Python 3.12</a>
            </body></html>
            """;
        var extracted = HtmlLinkExtractor.Extract(html, "https://docs.python.org/3/", maxLinks: 10);
        assert("discovery html: two links", extracted.Count == 2);
        var aio = extracted.First(l => l.Path.Contains("asyncio", StringComparison.OrdinalIgnoreCase));
        assert("discovery html: title is anchor text", aio.Title is not null && aio.Title.Contains("asyncio", StringComparison.OrdinalIgnoreCase));
        assert(
            "discovery html: context captures neighbors",
            aio.Context is not null
            && (aio.Context.Contains("Intro", StringComparison.OrdinalIgnoreCase)
                || aio.Context.Contains("Neighbor", StringComparison.OrdinalIgnoreCase)
                || aio.Context.Contains("coroutines", StringComparison.OrdinalIgnoreCase)));

        var htmlRanked = MapLinkRanker.Rank(extracted, "asyncio", 1);
        assert(
            "discovery html+rank: asyncio preferred",
            htmlRanked[0].Path.Contains("asyncio", StringComparison.OrdinalIgnoreCase));

        var hubs = MapService.SelectExpansionHubs(
            [
                new MappedLink("https://docs.python.org/3/whatsnew/3.12.html", "What's new", "/3/whatsnew/3.12.html"),
                new MappedLink("https://docs.python.org/3/library/index.html", "The Python Standard Library", "/3/library/index.html"),
                new MappedLink("https://docs.python.org/3/tutorial/index.html", "Tutorial", "/3/tutorial/index.html"),
            ],
            "asyncio",
            maxHubs: 2);
        assert("discovery expand: selects ≥1 hub", hubs.Count >= 1);
        assert(
            "discovery expand: library hub preferred",
            hubs[0].Path.Contains("/library", StringComparison.OrdinalIgnoreCase));

        // Leaf library modules must not be treated as expansion hubs.
        var leafHubs = MapService.SelectExpansionHubs(
            [
                new MappedLink("https://docs.python.org/3/library/queue.html", "queue", "/3/library/queue.html"),
                new MappedLink("https://docs.python.org/3/library/index.html", "The Python Standard Library", "/3/library/index.html"),
            ],
            "asyncio",
            maxHubs: 2);
        assert("discovery expand: skips leaf modules", leafHubs.All(h => !h.Path.EndsWith("queue.html", StringComparison.OrdinalIgnoreCase)));
        assert("discovery expand: keeps library index", leafHubs.Any(h => h.Path.Contains("index", StringComparison.OrdinalIgnoreCase)));

        // Primary-anchor HTML enrichment finds deep entity links past sequential extract caps.
        var deepHtml = """
            <html><body>
            <a href="/3/library/queue.html">queue</a>
            <a href="/3/library/itertools.html">itertools</a>
            """ + string.Concat(Enumerable.Range(0, 80).Select(i => $"<a href=\"/3/library/mod{i}.html\">mod{i}</a>\n")) + """
            <a href="/3/library/asyncio.html">asyncio — Asynchronous I/O</a>
            </body></html>
            """;
        var capped = HtmlLinkExtractor.Extract(deepHtml, "https://docs.python.org/3/", maxLinks: 10);
        assert("discovery enrich: sequential cap misses asyncio", capped.All(l => !l.Path.Contains("asyncio", StringComparison.OrdinalIgnoreCase)));
        var enriched = HtmlLinkExtractor.ExtractPrimaryMatches(
            deepHtml,
            "https://docs.python.org/3/",
            ["asyncio"]);
        assert("discovery enrich: primary scan finds asyncio", enriched.Any(l => l.Path.Contains("asyncio", StringComparison.OrdinalIgnoreCase)));

        var asyncQuery = MapLinkRanker.Rank(pythonPool, "async io", maxLinks: 2);
        assert(
            "discovery semantic: async io near asyncio",
            asyncQuery[0].Path.Contains("asyncio", StringComparison.OrdinalIgnoreCase)
            || (asyncQuery[0].Title?.Contains("async", StringComparison.OrdinalIgnoreCase) ?? false));
    }

    private static void RunPythonEntityFixture(Action<string, bool> assert)
    {
        const string query = "asyncio event loop tasks synchronization";
        var pool = new List<MappedLink>
        {
            new("https://docs.python.org/3.12/", "Python 3.12", "/3.12/"),
            new("https://docs.python.org/3.11/", "Python 3.11", "/3.11/"),
            new("https://docs.python.org/3.10/", "Python 3.10", "/3.10/"),
            new("https://docs.python.org/3/", "Python 3 documentation", "/3/"),
            new("https://docs.python.org/3/library/queue.html", "queue — A synchronized queue class", "/3/library/queue.html",
                Context: "thread-safe queue synchronization tasks"),
            new("https://docs.python.org/3/library/itertools.html", "itertools — Functions creating iterators", "/3/library/itertools.html",
                Context: "loop over tasks efficiently"),
            new("https://docs.python.org/3/library/sched.html", "sched — Event scheduler", "/3/library/sched.html",
                Context: "event loop scheduling"),
            new("https://docs.python.org/3/library/contextvars.html", "contextvars — Context Variables", "/3/library/contextvars.html"),
            new("https://docs.python.org/3/library/concurrent.futures.html", "concurrent.futures — Launching parallel tasks", "/3/library/concurrent.futures.html",
                Context: "asynchronous tasks"),
            new("https://docs.python.org/3/library/asyncio.html", "asyncio — Asynchronous I/O", "/3/library/asyncio.html",
                Context: "event loop tasks synchronization primitives"),
            new("https://docs.python.org/3/library/asyncio-task.html", "Coroutines and Tasks", "/3/library/asyncio-task.html",
                Context: "asyncio Task"),
            new("https://docs.python.org/3/whatsnew/3.12.html", "What's new in Python 3.12", "/3/whatsnew/3.12.html"),
        };

        var top5 = MapLinkRanker.Rank(pool, query, maxLinks: 5);
        assert(
            "fixture python: top5 contains /library/asyncio",
            top5.Any(l => l.Path.Contains("/library/asyncio", StringComparison.OrdinalIgnoreCase)));
        assert(
            "fixture python: asyncio ranks ahead of queue",
            IndexOfPath(top5, "asyncio") < IndexOfPath(top5, "queue")
            || top5[0].Path.Contains("asyncio", StringComparison.OrdinalIgnoreCase));

        var detailed = MapLinkRanker.RankDetailed(pool, query);
        assert("fixture python: winner is asyncio family", detailed[0].Link.Path.Contains("asyncio", StringComparison.OrdinalIgnoreCase));
        assert("fixture python: winner has primary hit", detailed[0].PrimaryHits.Count > 0);
        assert(
            "fixture python: queue missing-primary or lower score",
            detailed.First(d => d.Link.Path.Contains("queue", StringComparison.OrdinalIgnoreCase)).Total
            < detailed[0].Total);

        // Version roots must not occupy the majority of top-8.
        var top8 = MapLinkRanker.Rank(pool, query, 8);
        var versionRoots = top8.Count(l => MapLinkRanker.LooksLikeVersionRoot(l.Path));
        assert("fixture python: top8 not mostly version roots", versionRoots <= 2);
    }

    private static void RunMdnFixture(Action<string, bool> assert)
    {
        const string query = "AbortController cancel fetch request";
        var pool = new List<MappedLink>
        {
            new("https://developer.mozilla.org/en-US/docs/Web/API/Fetch_API", "Fetch API", "/en-US/docs/Web/API/Fetch_API",
                Context: "fetch request"),
            new("https://developer.mozilla.org/en-US/docs/Web/API/Request", "Request", "/en-US/docs/Web/API/Request",
                Context: "cancel a fetch request"),
            new("https://developer.mozilla.org/en-US/docs/Web/API/AbortController", "AbortController", "/en-US/docs/Web/API/AbortController",
                Context: "abort signal cancel fetch"),
            new("https://developer.mozilla.org/en-US/docs/Web/API/AbortSignal", "AbortSignal", "/en-US/docs/Web/API/AbortSignal"),
            new("https://developer.mozilla.org/en-US/docs/Web", "Web technology", "/en-US/docs/Web"),
        };

        var top = MapLinkRanker.Rank(pool, query, 3);
        assert(
            "fixture mdn: top contains AbortController",
            top.Any(l => l.Path.Contains("AbortController", StringComparison.OrdinalIgnoreCase)));
        assert(
            "fixture mdn: AbortController ranks first",
            top[0].Path.Contains("AbortController", StringComparison.OrdinalIgnoreCase));
    }

    private static void RunDotNetFixture(Action<string, bool> assert)
    {
        const string query = "CancellationToken cancel asynchronous operation";
        var pool = new List<MappedLink>
        {
            new("https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task", "Task Class", "/en-us/dotnet/api/system.threading.tasks.task",
                Context: "asynchronous operation"),
            new("https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken", "CancellationToken Struct", "/en-us/dotnet/api/system.threading.cancellationtoken",
                Context: "cancel asynchronous operation"),
            new("https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtokensource", "CancellationTokenSource", "/en-us/dotnet/api/system.threading.cancellationtokensource"),
            new("https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/", "Asynchronous programming", "/en-us/dotnet/csharp/asynchronous-programming/"),
            new("https://learn.microsoft.com/en-us/dotnet/api/", "API browser", "/en-us/dotnet/api/"),
        };

        var top = MapLinkRanker.Rank(pool, query, 3);
        assert(
            "fixture dotnet: top contains CancellationToken",
            top.Any(l => l.Path.Contains("cancellationtoken", StringComparison.OrdinalIgnoreCase)
                         && !l.Path.Contains("cancellationtokensource", StringComparison.OrdinalIgnoreCase)));
    }

    private static void RunK8sFixture(Action<string, bool> assert)
    {
        const string query = "StatefulSet stable network identity";
        var pool = new List<MappedLink>
        {
            new("https://kubernetes.io/docs/concepts/services-networking/service/", "Service", "/docs/concepts/services-networking/service/",
                Context: "stable network identity"),
            new("https://kubernetes.io/docs/concepts/workloads/controllers/deployment/", "Deployments", "/docs/concepts/workloads/controllers/deployment/"),
            new("https://kubernetes.io/docs/concepts/workloads/controllers/statefulset/", "StatefulSets", "/docs/concepts/workloads/controllers/statefulset/",
                Context: "stable network identity for pods"),
            new("https://kubernetes.io/docs/concepts/workloads/controllers/replicaset/", "ReplicaSet", "/docs/concepts/workloads/controllers/replicaset/"),
            new("https://kubernetes.io/docs/", "Kubernetes Documentation", "/docs/"),
        };

        var top = MapLinkRanker.Rank(pool, query, 3);
        assert(
            "fixture k8s: top contains StatefulSet",
            top.Any(l => l.Path.Contains("statefulset", StringComparison.OrdinalIgnoreCase)));
        assert(
            "fixture k8s: StatefulSet ranks first",
            top[0].Path.Contains("statefulset", StringComparison.OrdinalIgnoreCase));
    }

    private static void RunNegativeGeneric(Action<string, bool> assert)
    {
        const string query = "how to cancel a request safely";
        var decomp = FocusQueryDecomposition.Decompose(query);
        assert("fixture neg: no primary anchors", !decomp.HasPrimaryAnchors);

        var pool = new List<MappedLink>
        {
            new("https://example.com/privacy", "Privacy policy", "/privacy"),
            new("https://example.com/guide/cancel-request", "Cancel a request safely", "/guide/cancel-request",
                Context: "how to cancel a request safely"),
            new("https://example.com/api/AbortController", "AbortController", "/api/AbortController"),
        };

        var ranked = MapLinkRanker.RankDetailed(pool, query);
        assert("fixture neg: semantic winner is cancel-request guide", ranked[0].Link.Path.Contains("cancel-request", StringComparison.OrdinalIgnoreCase));
        assert(
            "fixture neg: no missing-primary penalty applied",
            ranked.All(r => r.MissingPrimaryPenaltyApplied == 0));
    }

    private static void RunVersionRootPenalty(Action<string, bool> assert)
    {
        assert("version root: /3.12/", MapLinkRanker.LooksLikeVersionRoot("/3.12/"));
        assert("version root: /3/", MapLinkRanker.LooksLikeVersionRoot("/3/"));
        assert("version root: /docs/3.10/", MapLinkRanker.LooksLikeVersionRoot("/docs/3.10/"));
        assert("version root: not library", !MapLinkRanker.LooksLikeVersionRoot("/3/library/asyncio.html"));

        var pool = new List<MappedLink>
        {
            new("https://docs.python.org/3.12/", "3.12", "/3.12/"),
            new("https://docs.python.org/3/library/asyncio.html", "asyncio", "/3/library/asyncio.html"),
        };
        var ranked = MapLinkRanker.RankDetailed(pool, "asyncio event loop");
        assert("version penalty: asyncio beats version root", ranked[0].Link.Path.Contains("asyncio", StringComparison.OrdinalIgnoreCase));
        assert(
            "version penalty: root has version penalty",
            ranked.First(r => MapLinkRanker.LooksLikeVersionRoot(r.Link.Path)).VersionPenaltyApplied > 0);
    }

    private static void RunSharedRankerSurface(Action<string, bool> assert)
    {
        // Map and digest both call MapLinkRanker.Rank — same implementation surface.
        var links = new List<MappedLink>
        {
            new("https://docs.python.org/3/library/queue.html", "queue", "/3/library/queue.html"),
            new("https://docs.python.org/3/library/asyncio.html", "asyncio", "/3/library/asyncio.html"),
        };
        var a = MapLinkRanker.Rank(links, "asyncio event loop tasks synchronization", 2);
        var b = MapLinkRanker.RankScored(links, "asyncio event loop tasks synchronization")
            .Take(2)
            .Select(s => s.Link)
            .ToList();
        assert("shared ranker: Rank ≡ RankScored order", a[0].Url == b[0].Url && a[1].Url == b[1].Url);
    }

    private static List<MappedLink> BuildPythonPool() =>
    [
        new("https://docs.python.org/3/whatsnew/3.12.html", "What's new in Python 3.12", "/3/whatsnew/3.12.html"),
        new("https://docs.python.org/3/whatsnew/3.11.html", "What's new in Python 3.11", "/3/whatsnew/3.11.html"),
        new("https://docs.python.org/3/tutorial/index.html", "The Python Tutorial", "/3/tutorial/index.html"),
        new("https://docs.python.org/3/library/index.html", "The Python Standard Library", "/3/library/index.html"),
        new("https://docs.python.org/3/library/os.html", "os — Miscellaneous operating system interfaces", "/3/library/os.html"),
        new(
            "https://docs.python.org/3/library/asyncio.html",
            "asyncio — Asynchronous I/O",
            "/3/library/asyncio.html",
            Context: "See also the Asynchronous I/O section of the library reference"),
        new("https://docs.python.org/3/reference/index.html", "The Python Language Reference", "/3/reference/index.html"),
    ];

    private static int IndexOfPath(IReadOnlyList<MappedLink> links, string marker)
    {
        for (var i = 0; i < links.Count; i++)
        {
            if (links[i].Path.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return int.MaxValue;
    }
}
