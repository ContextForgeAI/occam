using System.Text.Json;

namespace OccamMcp.Core.Playbooks;

/// <summary>
/// SI-13: a pure, network-free validator for a playbook / genome JSON against the 1.x schema. Given a
/// draft (from the heal loop, a community contributor, or CI vetting a genome file) it returns a graded
/// list of issues — errors that would break <c>resolve</c>/<c>save</c> schema acceptance, warnings that
/// degrade quality, and info nudges — so an agent can fix a recipe before paying for a live verify.
/// Hard errors share <see cref="PlaybookSchemaGate"/> with save (EF-015). It only reads the document;
/// it never fetches. Grade: <c>ready</c> (clean), <c>usable</c> (works, has warnings), <c>broken</c>
/// (has errors).
/// </summary>
public static class PlaybookLinter
{
    public static PlaybookLintReport Lint(string? playbookJson)
    {
        var issues = new List<PlaybookLintIssue>();
        issues.AddRange(PlaybookSchemaGate.CollectHardErrors(playbookJson));

        // Soft checks only when JSON is an object (hard errors already cover empty/invalid JSON).
        if (!string.IsNullOrWhiteSpace(playbookJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(playbookJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    CheckHostsShape(doc.RootElement, issues);
                    CheckRouting(doc.RootElement, issues);
                    CheckContentSelectorBlanks(doc.RootElement, issues);
                    CheckKnowledgeSchema(doc.RootElement, issues);
                    CheckMetaAndNotes(doc.RootElement, issues);
                }
            }
            catch (JsonException)
            {
                // Hard error already recorded.
            }
        }

        return Report(issues);
    }

    private static void CheckHostsShape(JsonElement root, List<PlaybookLintIssue> issues)
    {
        if (!root.TryGetProperty("hosts", out var hosts) || hosts.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var h in hosts.EnumerateArray())
        {
            var value = h.ValueKind == JsonValueKind.String ? h.GetString() : null;
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (value.Contains("://", StringComparison.Ordinal) || value.Contains('/')
                || value.Any(char.IsWhiteSpace) || value.Any(char.IsUpper))
            {
                issues.Add(Warning("hosts", "host_not_bare",
                    $"host \"{value}\" should be a bare lowercase hostname (no scheme, path, or caps)."));
            }
        }
    }

    private static void CheckContentSelectorBlanks(JsonElement root, List<PlaybookLintIssue> issues)
    {
        if (!PlaybookSchemaGate.TryGetContentSelectors(root, out var selectors))
        {
            return;
        }

        foreach (var s in selectors.EnumerateArray())
        {
            if (s.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(s.GetString()))
            {
                issues.Add(Warning("extract.contentSelectors", "selector_blank",
                    "contentSelectors contains a blank/non-string entry; it will be ignored."));
            }
        }
    }

    private static void CheckRouting(JsonElement root, List<PlaybookLintIssue> issues)
    {
        if (!root.TryGetProperty("routing", out var routing) || routing.ValueKind != JsonValueKind.Object
            || !routing.TryGetProperty("preferred_backend", out var backendEl))
        {
            return; // routing is optional; resolve defaults to http_then_browser
        }

        var backend = backendEl.ValueKind == JsonValueKind.String ? backendEl.GetString() : null;
        if (!PlaybookSchemaGate.IsKnownBackend(backend))
        {
            issues.Add(Warning("routing.preferred_backend", "invalid_backend",
                $"preferred_backend \"{backend}\" is not http | browser | http_then_browser (or http-then-browser); it will fall back to http_then_browser."));
        }
    }

    private static void CheckKnowledgeSchema(JsonElement root, List<PlaybookLintIssue> issues)
    {
        if (!root.TryGetProperty("knowledge_schema", out var schema) || schema.ValueKind != JsonValueKind.Object)
        {
            return; // optional
        }

        var pageClasses = new HashSet<string>(StringComparer.Ordinal);
        if (root.TryGetProperty("genome", out var genome)
            && genome.TryGetProperty("page_classes", out var pc)
            && pc.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in pc.EnumerateObject())
            {
                pageClasses.Add(p.Name);
            }
        }

        foreach (var entry in schema.EnumerateObject())
        {
            if (entry.Name == "default" || pageClasses.Contains(entry.Name))
            {
                continue;
            }

            issues.Add(Warning($"knowledge_schema.{entry.Name}", "unrouted_class",
                $"knowledge_schema class \"{entry.Name}\" has no genome.page_classes route; it will never fire (add a page_classes pattern or rename to \"default\")."));
        }
    }

    private static void CheckMetaAndNotes(JsonElement root, List<PlaybookLintIssue> issues)
    {
        var hasTitle = root.TryGetProperty("meta", out var meta)
            && meta.ValueKind == JsonValueKind.Object
            && !string.IsNullOrWhiteSpace(GetString(meta, "title"));
        if (!hasTitle)
        {
            issues.Add(Warning("meta.title", "missing", "meta.title helps operators identify the recipe."));
        }

        if (string.IsNullOrWhiteSpace(GetString(root, "agent_notes")))
        {
            issues.Add(Info("agent_notes", "missing",
                "agent_notes guides the consuming model (hub-vs-leaf, focus_query hints); recommended."));
        }
    }

    private static PlaybookLintReport Report(List<PlaybookLintIssue> issues)
    {
        var errors = issues.Count(i => i.Severity == "error");
        var warnings = issues.Count(i => i.Severity == "warning");
        var infos = issues.Count(i => i.Severity == "info");
        var grade = errors > 0 ? "broken" : warnings > 0 ? "usable" : "ready";
        return new PlaybookLintReport(grade, errors == 0, errors, warnings, infos, [.. issues]);
    }

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

    private static PlaybookLintIssue Warning(string field, string code, string message) => new("warning", field, code, message);
    private static PlaybookLintIssue Info(string field, string code, string message) => new("info", field, code, message);
}

public sealed record PlaybookLintIssue(string Severity, string Field, string Code, string Message);

/// <summary>
/// Lint outcome. <see cref="Grade"/> ∈ ready | usable | broken; <see cref="AgentReady"/> is true iff
/// there are no hard errors (i.e. save would accept the schema/hygiene gate — live verify is separate).
/// </summary>
public sealed record PlaybookLintReport(
    string Grade,
    bool AgentReady,
    int Errors,
    int Warnings,
    int Infos,
    PlaybookLintIssue[] Issues);
