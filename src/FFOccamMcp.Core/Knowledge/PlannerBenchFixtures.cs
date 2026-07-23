using OccamMcp.Core.Knowledge.Canonical;
using OccamMcp.Core.Knowledge.Extraction;
using OccamMcp.Core.Knowledge.Legacy;

namespace OccamMcp.Core.Knowledge;

/// <summary>Offline <see cref="ExtractedKnowledgeBundle"/> fixtures for <see cref="PlannerBench"/>.</summary>
public static class PlannerBenchFixtures
{
    public static readonly DateTimeOffset FixedAt = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);

    public const string TechnicalDocsFocusQuery = "proxy_pass";

    public static ExtractedKnowledgeBundle LongArticle()
    {
        var blocks = new List<KnowledgeBlock>
        {
            new("heading", "Materialization and the Web Runtime", Level: 1),
            new("paragraph", "Occam turns web pages into compact, verifiable knowledge representations for agents."),
            new("heading", "Acquisition", Level: 2),
            new("paragraph", "HTTP and browser workers fetch live HTML. There is no file cache by design."),
            new("heading", "Planning", Level: 2),
            new("paragraph", "The MaterializationPlanner owns semantic retention: selectors, fit, budget, and Canonical refs."),
            new("paragraph", "Codecs must not re-plan. They only serialize an already-materialized view."),
            new("heading", "Evidence", Level: 2),
            new("paragraph", "Receipts bind content hashes and Merkle leaves so agents can verify what was extracted."),
            new("list_item", "Keep provenance honest"),
            new("list_item", "Prefer measured token reduction"),
            new("list_item", "Do not invent page content on failure"),
            new("heading", "Budget pressure", Level: 2),
            new("paragraph", "When max_tokens is small, the planner truncates the surface and may prune document IR blocks."),
            new("paragraph", "Long articles exercise compact versus compat differences in retained structure."),
            new("code", "planner.Plan(request, bundle);"),
            new("quote", "Architecture maturity is more valuable than architecture expansion."),
            new("heading", "Appendix A", Level: 2),
            new("paragraph", "Extra filler paragraphs ensure the compact budget actually binds on this fixture."),
            new("paragraph", "More filler about routing, workers, post-processors, and recovery cascades on thin extracts."),
            new("paragraph", "Still more text describing playbooks, genomes, heal loops, and community seeds for coverage."),
            new("paragraph", "Additional sentences mentioning focus_query, fit_markdown, and content_selectors as compile knobs."),
            new("paragraph", "Closing remarks on Hermes orchestration staying outside Occam's knowledge pipeline."),
            new("heading", "Appendix B", Level: 2),
            new("paragraph", "Secondary appendix content about codecs: markdown-passthrough, compact-markdown, knowledge-json."),
            new("paragraph", "Benchmark harnesses compare policies without claiming universal ranking across all websites."),
            new("paragraph", "Determinism checks run Plan twice and compare views byte-for-byte for stability."),
            new("paragraph", "Integrity checks resolve claim→evidence→source and provenance membership when Canonical is present."),
            new("paragraph", "End of long-article fixture body used by PlannerBench offline evaluation."),
        };

        // Pad to ensure compact (128) differs from compat (4096).
        while (blocks.Count < 29)
        {
            blocks.Add(new KnowledgeBlock(
                "paragraph",
                $"Padding paragraph {blocks.Count} with enough words to consume heuristic unicode tokens for budget tests."));
        }

        var md = string.Join(
            "\n\n",
            blocks.Select(b => b.Type switch
            {
                "heading" => $"{new string('#', b.Level ?? 2)} {b.Text}",
                "list_item" => $"- {b.Text}",
                "code" => $"```\n{b.Text}\n```",
                "quote" => $"> {b.Text}",
                _ => b.Text,
            }));

        return Bundle(md, new KnowledgeDocument(blocks, []), url: "https://fixture.local/long-article");
    }

    public static ExtractedKnowledgeBundle TechnicalDocs()
    {
        var blocks = new List<KnowledgeBlock>
        {
            new("heading", "nginx proxy_pass", Level: 1),
            new("paragraph", "The proxy_pass directive sets the protocol and address of a proxied server."),
            new("heading", "Syntax", Level: 2),
            new("code", "proxy_pass http://backend;"),
            new("paragraph", "A URI can be specified along with the address."),
            new("heading", "Related", Level: 2),
            new("paragraph", "See also proxy_set_header and proxy_redirect for related configuration."),
            new("list_item", "proxy_pass with variables"),
            new("list_item", "upstream blocks"),
            new("heading", "Notes", Level: 2),
            new("paragraph", "Trailing slashes change how the request URI is passed."),
            new("paragraph", "resolver may be required when using variables in proxy_pass."),
            new("quote", "If proxy_pass is specified without a URI, the request URI is passed in full."),
            new("paragraph", "Other directives: root, alias, try_files — not the focus of this fixture."),
            new("heading", "Examples", Level: 2),
            new("paragraph", "Simple reverse proxy to an upstream named backend."),
            new("code", "location / { proxy_pass http://backend; }"),
            new("paragraph", "With a URI path that replaces the matched location prefix."),
            new("paragraph", "Padding for budget: keepalive connections, buffers, and timeouts also matter in production."),
            new("paragraph", "More padding about SSL termination, SNI, and certificate management outside proxy_pass."),
            new("paragraph", "Still more about rate limiting, caching zones, and gzip that are adjacent but not focus hits."),
            new("paragraph", "Final padding paragraph so compact and focus share a comparable MaxTokens band."),
            new("heading", "See also", Level: 2),
            new("paragraph", "http://nginx.org/en/docs/http/ngx_http_proxy_module.html"),
            new("list_item", "proxy_buffering"),
            new("list_item", "proxy_cache"),
            new("paragraph", "End of technical-docs fixture."),
        };

        var md = string.Join(
            "\n\n",
            blocks.Select(b => b.Type switch
            {
                "heading" => $"{new string('#', b.Level ?? 2)} {b.Text}",
                "list_item" => $"- {b.Text}",
                "code" => $"```\n{b.Text}\n```",
                "quote" => $"> {b.Text}",
                _ => b.Text,
            }));

        return Bundle(md, new KnowledgeDocument(blocks, []), url: "https://fixture.local/nginx-proxy");
    }

    public static ExtractedKnowledgeBundle Tables()
    {
        var md = """
            # Directive table

            | Directive | Meaning |
            |-----------|---------|
            | proxy_pass | upstream address |
            """;

        var doc = new KnowledgeDocument(
            [
                new KnowledgeBlock("heading", "Directive table", Level: 1),
                new KnowledgeBlock("paragraph", "Summary of proxy-related directives."),
                new KnowledgeBlock("table", "proxy_pass upstream address"),
            ],
            [
                new KnowledgeTable(
                    "Directives",
                    ["Directive", "Meaning"],
                    [["proxy_pass", "upstream address"]]),
            ]);

        return Bundle(md, doc, url: "https://fixture.local/tables");
    }

    /// <summary>
    /// Canonical refs with enough claim-statement tokens that MaxTokens=128 under default policy
    /// prunes claims, while evidence-preserving retains the full graph.
    /// </summary>
    public static ExtractedKnowledgeBundle CanonicalRefs()
    {
        var sourceId = SourceId.From("11111111111111111111111111111111");
        var evidenceIds = Enumerable.Range(0, 8)
            .Select(i => EvidenceId.From($"{i + 2:D32}"))
            .ToArray();
        var claimIds = Enumerable.Range(0, 8)
            .Select(i => ClaimCandidateId.From($"{i + 20:D32}"))
            .ToArray();
        var provId = ProvenanceId.From("55555555555555555555555555555555");

        var source = Source.Create(
            sourceId,
            SourceKind.WebPage,
            "https://fixture.local/canonical",
            FixedAt,
            contentHash: "sha256:fixturecanonical01",
            title: "Canonical fixture");

        var evidence = new List<Evidence>();
        var claims = new List<ClaimCandidate>();
        var statements = new[]
        {
            "MaterializationPlanner owns retention of Canonical knowledge under budget and focus policies.",
            "Codecs serialize only and must never prune claims, evidence, or provenance records themselves.",
            "Default provenance policy may drop low-priority claims when the claim token budget binds tightly.",
            "Evidence-preserving policy keeps the full claim to evidence to source closure for integrity checks.",
            "Focus-aware ranking prefers claim statements that overlap the focus_query terms before others.",
            "Receipt content hashes bridge provenance to verify without embedding Markdown in Canonical models.",
            "Orphan evidence without a retained claim is removed under default policy after claim selection.",
            "Deterministic planning restores claim order after focus-ranked greedy retention for stable views.",
        };

        for (var i = 0; i < statements.Length; i++)
        {
            evidence.Add(Evidence.Create(
                evidenceIds[i],
                sourceId,
                EvidenceLocator.SourceSelector($"#main > p:nth-of-type({i + 1})"),
                EvidenceKind.ContentBlock,
                FixedAt,
                contentHash: $"leaf{i:D2}",
                excerpt: statements[i]));

            claims.Add(ClaimCandidate.Create(
                claimIds[i],
                statements[i],
                ClaimKind.ExtractedClaim,
                [evidenceIds[i]],
                FixedAt,
                extractorId: "fixture",
                extractorVersion: "1"));
        }

        var provenance = KnowledgeProvenance.Create(
            provId,
            sourceId,
            evidenceIds,
            observedAt: FixedAt,
            extractionMethod: "fixture",
            receiptContentHash: "sha256:fixturecanonical01",
            blockLeafHash: "leaf00");

        var canonical = new CanonicalExtract(source, evidence, claims, [provenance]);

        var md = """
            # Canonical fixture

            MaterializationPlanner owns retention.

            Codecs serialize only.
            """;

        var doc = new KnowledgeDocument(
            [
                new KnowledgeBlock("heading", "Canonical fixture", Level: 1),
                new KnowledgeBlock("paragraph", "MaterializationPlanner owns retention."),
                new KnowledgeBlock("paragraph", "Codecs serialize only."),
            ],
            []);

        return new ExtractedKnowledgeBundle(
            SourceSurface.Markdown(md),
            doc,
            Canonical: canonical,
            FinalUrl: "https://fixture.local/canonical",
            Backend: "fixture");
    }

    /// <summary>Empty / minimal bundle for edge handling.</summary>
    public static ExtractedKnowledgeBundle Empty() =>
        new(SourceSurface.Markdown(""), KnowledgeDocument.Empty, Canonical: null, FinalUrl: null, Backend: "fixture");

    public static IReadOnlyList<(string Id, ExtractedKnowledgeBundle Bundle, string FocusQuery)> All() =>
    [
        ("long-article", LongArticle(), "materialization"),
        ("technical-docs", TechnicalDocs(), TechnicalDocsFocusQuery),
        ("tables", Tables(), "proxy_pass"),
        ("canonical-refs", CanonicalRefs(), "MaterializationPlanner"),
    ];

    private static ExtractedKnowledgeBundle Bundle(string markdown, KnowledgeDocument doc, string url) =>
        new(SourceSurface.Markdown(markdown), doc, Canonical: null, FinalUrl: url, Backend: "fixture");
}
