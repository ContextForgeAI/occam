using OccamMcp.Core.Playbooks;

namespace OccamMcp.L0Gate;

internal static class L4GenomeUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        RunPageClassMatcher(assert);
        RunKnowledgeSchemaPlanner(assert);
        RunPlaybookGenomeMerger(assert);
        RunPlaybookPolicy(assert);
    }

    private static void RunPageClassMatcher(Action<string, bool> assert)
    {
        assert("l4 unit/page class prefix", PageClassMatcher.Matches("/docs/concepts/overview/", "/docs/concepts/*"));
        assert("l4 unit/page class exact", PageClassMatcher.Matches("/docs/tasks/", "/docs/tasks/"));
        assert("l4 unit/page class miss", !PageClassMatcher.Matches("/blog/", "/docs/concepts/*"));
    }

    private static void RunKnowledgeSchemaPlanner(Action<string, bool> assert)
    {
        var root = PlaybookGenomeMerger.ParseRoot(
            """
            {
              "genome": { "page_classes": { "concepts": "/docs/concepts/*" } },
              "knowledge_schema": {
                "concepts": { "title": { "selector": "h1", "attr": "text" } }
              }
            }
            """);

        var ok = KnowledgeSchemaPlanner.TryMatch(
            root,
            "https://kubernetes.io/docs/concepts/overview/",
            out var match,
            out var failureCode);
        assert("l4 unit/schema match ok", ok);
        assert("l4 unit/schema page class", match?.PageClass == "concepts");
        assert("l4 unit/schema failure null", failureCode is null);
    }

    private static void RunPlaybookGenomeMerger(Action<string, bool> assert)
    {
        var playbookRoot = PlaybookGenomeMerger.ParseRoot(
            """
            {
              "genome": {
                "site_type": "documentation",
                "page_classes": { "concepts": "/docs/concepts/*" }
              }
            }
            """);
        var playbook = PlaybookGenomeMerger.GetObject(playbookRoot, "genome");

        var siteRoot = PlaybookGenomeMerger.ParseRoot(
            """
            {
              "genome": {
                "site_type": "site_hint",
                "page_classes": { "tasks": "/docs/tasks/*" },
                "features": ["search"]
              }
            }
            """);
        var site = PlaybookGenomeMerger.GetObject(siteRoot, "genome");

        var merged = PlaybookGenomeMerger.MergeGenome(playbook, site);
        assert(
            "l4 unit/merge playbook page_classes win",
            merged?.TryGetProperty("page_classes", out var pageClasses) == true
            && pageClasses.TryGetProperty("concepts", out _));
        assert(
            "l4 unit/merge site features fill gap",
            merged?.TryGetProperty("features", out _) == true);
        assert(
            "l4 unit/merge site_type playbook wins",
            merged?.TryGetProperty("site_type", out var siteType) == true
            && siteType.GetString() == "documentation");
        RunGenomeElementRoundTrip(assert);
    }

    private static void RunGenomeElementRoundTrip(Action<string, bool> assert)
    {
        var genome = PlaybookGenomeMerger.ParseRoot(
            """
            {
              "site_type": "documentation",
              "page_classes": { "concepts": "/docs/concepts/*" }
            }
            """);
        var element = PlaybookGenomeMerger.ToElementOrNull(genome);
        assert("l4 unit/genome element roundtrip", element is not null);
        assert(
            "l4 unit/genome element concepts",
            element!.Value.ToString().Contains("concepts", StringComparison.Ordinal));
    }

    private static void RunPlaybookPolicy(Action<string, bool> assert)
    {
        assert("l4 unit/policy auto", PlaybookPolicy.ShouldApply("auto"));
        assert("l4 unit/policy off", !PlaybookPolicy.ShouldApply("off"));
        assert("l4 unit/policy none alias", PlaybookPolicy.Normalize("none") == PlaybookPolicy.Off);
        assert("l4 unit/policy parse", PlaybookPolicy.TryParse("auto", out var normalized, out _) && normalized == "auto");
    }
}
