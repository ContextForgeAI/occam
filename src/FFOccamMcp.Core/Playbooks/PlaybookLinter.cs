using System.Text.Json;
using System.Text.Json.Serialization;

namespace OccamMcp.Core.Playbooks;

/// <summary>
/// SI-13: a pure, network-free validator for a playbook / genome JSON against the 1.x schema. Given a
/// draft (from the heal loop, a community contributor, or CI vetting a genome file) it returns a graded
/// list of issues — errors that would break <c>resolve</c>/<c>save</c>, warnings that degrade quality,
/// and info nudges — so an agent can fix a recipe before paying for a live verify. It only reads the
/// document; it never fetches. Grade: <c>ready</c> (clean), <c>usable</c> (works, has warnings),
/// <c>broken</c> (has errors).
/// </summary>
public static class PlaybookLinter
{
    private static readonly string[] ValidBackends = ["http", "browser", "http_then_browser"];

    public static PlaybookLintReport Lint(string? playbookJson)
    {
        var issues = new List<PlaybookLintIssue>();

        if (string.IsNullOrWhiteSpace(playbookJson))
        {
            issues.Add(Error("(root)", "empty_input", "playbook_json is empty."));
            return Report(issues);
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(playbookJson);
        }
        catch (JsonException ex)
        {
            issues.Add(Error("(root)", "json_invalid", $"Not valid JSON: {ex.Message}"));
            return Report(issues);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Error("(root)", "not_object", "Top level must be a JSON object."));
                return Report(issues);
            }

            CheckSchemaVersion(root, issues);
            CheckId(root, issues);
            CheckHosts(root, issues);
            CheckExtract(root, issues);
            CheckRouting(root, issues);
            CheckKnowledgeSchema(root, issues);
            CheckMetaAndNotes(root, issues);
        }

        return Report(issues);
    }

    private static void CheckSchemaVersion(JsonElement root, List<PlaybookLintIssue> issues)
    {
        var version = GetString(root, "schema_version");
        if (string.IsNullOrWhiteSpace(version))
        {
            issues.Add(Error("schema_version", "missing", "schema_version is required (e.g. \"1.0\")."));
        }
        else if (!version.StartsWith("1.", StringComparison.Ordinal))
        {
            issues.Add(Error("schema_version", "unsupported",
                $"schema_version \"{version}\" is not 1.x; resolve only accepts the 1.x line."));
        }
    }

    private static void CheckId(JsonElement root, List<PlaybookLintIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(GetString(root, "id")))
        {
            issues.Add(Error("id", "missing", "id is required (usually the primary host, e.g. \"docs.docker.com\")."));
        }
    }

    private static void CheckHosts(JsonElement root, List<PlaybookLintIssue> issues)
    {
        if (!root.TryGetProperty("hosts", out var hosts) || hosts.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Error("hosts", "missing", "hosts must be a non-empty array of bare hostnames."));
            return;
        }

        var any = false;
        foreach (var h in hosts.EnumerateArray())
        {
            var value = h.ValueKind == JsonValueKind.String ? h.GetString() : null;
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            any = true;
            if (value.Contains("://", StringComparison.Ordinal) || value.Contains('/')
                || value.Any(char.IsWhiteSpace) || value.Any(char.IsUpper))
            {
                issues.Add(Warning("hosts", "host_not_bare",
                    $"host \"{value}\" should be a bare lowercase hostname (no scheme, path, or caps)."));
            }
        }

        if (!any)
        {
            issues.Add(Error("hosts", "empty", "hosts must contain at least one non-empty hostname."));
        }
    }

    private static void CheckExtract(JsonElement root, List<PlaybookLintIssue> issues)
    {
        if (!root.TryGetProperty("extract", out var extract) || extract.ValueKind != JsonValueKind.Object
            || !extract.TryGetProperty("contentSelectors", out var selectors)
            || selectors.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Error("extract.contentSelectors", "missing",
                "extract.contentSelectors is required — without it the playbook cannot drive an extraction."));
            return;
        }

        var count = 0;
        foreach (var s in selectors.EnumerateArray())
        {
            if (s.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(s.GetString()))
            {
                count++;
            }
            else
            {
                issues.Add(Warning("extract.contentSelectors", "selector_blank",
                    "contentSelectors contains a blank/non-string entry; it will be ignored."));
            }
        }

        if (count == 0)
        {
            issues.Add(Error("extract.contentSelectors", "empty",
                "extract.contentSelectors has no usable selector."));
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
        if (string.IsNullOrWhiteSpace(backend) || !ValidBackends.Contains(backend))
        {
            issues.Add(Warning("routing.preferred_backend", "invalid_backend",
                $"preferred_backend \"{backend}\" is not http | browser | http_then_browser; it will fall back to http_then_browser."));
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
            // "default" applies to any page and needs no page_class; other classes must be routable.
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

    private static PlaybookLintIssue Error(string field, string code, string message) => new("error", field, code, message);
    private static PlaybookLintIssue Warning(string field, string code, string message) => new("warning", field, code, message);
    private static PlaybookLintIssue Info(string field, string code, string message) => new("info", field, code, message);
}

public sealed record PlaybookLintIssue(string Severity, string Field, string Code, string Message);

/// <summary>
/// Lint outcome. <see cref="Grade"/> ∈ ready | usable | broken; <see cref="AgentReady"/> is true iff
/// there are no errors (i.e. resolve/save would accept it — quality caveats are the warnings).
/// </summary>
public sealed record PlaybookLintReport(
    string Grade,
    bool AgentReady,
    int Errors,
    int Warnings,
    int Infos,
    PlaybookLintIssue[] Issues);
