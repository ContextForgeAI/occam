using OccamMcp.Core.Playbooks;

namespace OccamMcp.L0Gate;

/// <summary>
/// L_LINT — SI-13 playbook/genome linter. Pure, deterministic schema validation: errors that break
/// resolve/save vs warnings that only degrade quality, and the ready/usable/broken grade. No network.
/// </summary>
public static class PlaybookLintUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        // A clean, complete 1.0 genome → ready, no issues.
        const string clean = """
        {
          "schema_version": "1.0",
          "id": "kubernetes.io",
          "hosts": ["kubernetes.io"],
          "meta": { "title": "Kubernetes docs" },
          "genome": { "page_classes": { "concepts": "/docs/concepts/*" } },
          "routing": { "preferred_backend": "http_then_browser" },
          "extract": { "contentSelectors": ["main", ".td-content"] },
          "knowledge_schema": { "concepts": { "title": { "selector": "h1" } }, "default": {} },
          "agent_notes": "prefer leaf pages"
        }
        """;
        var ok = PlaybookLinter.Lint(clean);
        assert("lint clean genome is ready", ok is { Grade: "ready", AgentReady: true, Errors: 0, Warnings: 0 });

        // Invalid JSON → single error, broken.
        var bad = PlaybookLinter.Lint("{not json");
        assert("lint invalid json -> broken", bad is { Grade: "broken", AgentReady: false } && bad.Errors == 1);

        // Missing required fields → errors for each (schema_version, id, hosts, contentSelectors).
        var empty = PlaybookLinter.Lint("{}");
        assert("lint empty object flags all required fields", empty.Errors >= 4 && !empty.AgentReady);
        assert("lint empty flags schema_version", empty.Issues.Any(i => i.Field == "schema_version" && i.Severity == "error"));
        assert("lint empty flags hosts", empty.Issues.Any(i => i.Field == "hosts" && i.Severity == "error"));
        assert("lint empty flags contentSelectors", empty.Issues.Any(i => i.Field == "extract.contentSelectors" && i.Severity == "error"));

        // Wrong schema major → unsupported error.
        var v2 = PlaybookLinter.Lint("""{"schema_version":"2.0","id":"x","hosts":["x.com"],"extract":{"contentSelectors":["main"]}}""");
        assert("lint rejects non-1.x schema", v2.Issues.Any(i => i.Field == "schema_version" && i.Code == "unsupported"));

        // Warnings-only doc → usable (bad backend + non-bare host + unrouted knowledge class), no errors.
        const string warny = """
        {
          "schema_version": "1.0",
          "id": "x.com",
          "hosts": ["https://X.com/path"],
          "routing": { "preferred_backend": "curl" },
          "extract": { "contentSelectors": ["main"] },
          "knowledge_schema": { "product": { "title": { "selector": "h1" } } }
        }
        """;
        var w = PlaybookLinter.Lint(warny);
        assert("lint warnings-only doc is usable", w is { Grade: "usable", AgentReady: true, Errors: 0 } && w.Warnings >= 3);
        assert("lint flags non-bare host", w.Issues.Any(i => i.Code == "host_not_bare"));
        assert("lint flags invalid backend", w.Issues.Any(i => i.Code == "invalid_backend"));
        assert("lint flags unrouted knowledge class", w.Issues.Any(i => i.Code == "unrouted_class"));

        // "default" knowledge class needs no page_class route → not flagged.
        const string defaultOnly = """
        {"schema_version":"1.0","id":"x.com","hosts":["x.com"],"meta":{"title":"t"},
         "extract":{"contentSelectors":["main"]},"knowledge_schema":{"default":{"title":{"selector":"h1"}}},
         "agent_notes":"n"}
        """;
        var d = PlaybookLinter.Lint(defaultOnly);
        assert("lint default knowledge class is not unrouted", d is { Grade: "ready" } && !d.Issues.Any(i => i.Code == "unrouted_class"));

        Console.WriteLine("L_LINT_OK");
    }
}
