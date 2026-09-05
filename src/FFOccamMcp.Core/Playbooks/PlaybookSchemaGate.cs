using System.Text.Json;
using OccamMcp.Core.Routing;

namespace OccamMcp.Core.Playbooks;

/// <summary>
/// Shared structural gate for playbook JSON — the same hard errors for
/// <c>occam_playbook_lint</c> and <c>occam_playbook_save</c> (EF-015 convergence).
/// Live verify / quality score remain save-only and network-bound.
/// </summary>
public static class PlaybookSchemaGate
{
    /// <summary>
    /// Collect save/lint hard errors (severity=error). Empty list means the document is
    /// structurally acceptable for save (before live verify).
    /// </summary>
    public static IReadOnlyList<PlaybookLintIssue> CollectHardErrors(string? playbookJson)
    {
        var issues = new List<PlaybookLintIssue>();
        if (string.IsNullOrWhiteSpace(playbookJson))
        {
            issues.Add(Error("(root)", "empty_input", "playbook_json is empty."));
            return issues;
        }

        if (PlaybookCommunityHygiene.ContainsForbiddenKeys(playbookJson))
        {
            // Invalid JSON also trips ContainsForbiddenKeys — distinguish below.
            try
            {
                _ = JsonDocument.Parse(playbookJson);
                issues.Add(Error("(root)", "forbidden_secret_key",
                    "playbook_json contains forbidden secret keys (cookie/authorization/token/…); remove them before save."));
            }
            catch (JsonException)
            {
                // Fall through to JSON parse below for a clearer json_invalid error.
            }
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(playbookJson);
        }
        catch (JsonException ex)
        {
            issues.Clear();
            issues.Add(Error("(root)", "json_invalid", $"Not valid JSON: {ex.Message}"));
            return issues;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                issues.Clear();
                issues.Add(Error("(root)", "not_object", "Top level must be a JSON object."));
                return issues;
            }

            CollectDocumentErrors(root, issues);
        }

        return issues;
    }

    public static void CollectDocumentErrors(JsonElement root, List<PlaybookLintIssue> issues)
    {
        var version = GetString(root, "schema_version");
        if (string.IsNullOrWhiteSpace(version))
        {
            issues.Add(Error("schema_version", "missing", "schema_version is required (e.g. \"1.0\")."));
        }
        else if (!version.StartsWith("1.", StringComparison.Ordinal))
        {
            issues.Add(Error("schema_version", "unsupported",
                $"schema_version \"{version}\" is not 1.x; resolve/save only accept the 1.x line."));
        }

        if (string.IsNullOrWhiteSpace(GetString(root, "id")))
        {
            issues.Add(Error("id", "missing", "id is required (usually the primary host, e.g. \"docs.docker.com\")."));
        }

        if (!root.TryGetProperty("hosts", out var hosts) || hosts.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Error("hosts", "missing", "hosts must be a non-empty array of bare hostnames."));
        }
        else
        {
            var any = hosts.EnumerateArray()
                .Any(h => h.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(h.GetString()));
            if (!any)
            {
                issues.Add(Error("hosts", "empty", "hosts must contain at least one non-empty hostname."));
            }
        }

        if (!TryGetContentSelectors(root, out var selectors) || selectors.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Error("extract.contentSelectors", "missing",
                "extract.contentSelectors (or extract.content_selectors) is required — without it the playbook cannot drive an extraction."));
        }
        else
        {
            var count = selectors.EnumerateArray()
                .Count(s => s.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(s.GetString()));
            if (count == 0)
            {
                issues.Add(Error("extract.contentSelectors", "empty",
                    "extract.contentSelectors has no usable selector."));
            }
        }
    }

    /// <summary>True when <see cref="CollectHardErrors"/> is empty (schema + hygiene).</summary>
    public static bool IsSaveAcceptable(string? playbookJson) => CollectHardErrors(playbookJson).Count == 0;

    public static bool TryGetContentSelectors(JsonElement root, out JsonElement selectors)
    {
        selectors = default;
        if (!root.TryGetProperty("extract", out var extract) || extract.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (extract.TryGetProperty("contentSelectors", out selectors) && selectors.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        if (extract.TryGetProperty("content_selectors", out selectors) && selectors.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        return false;
    }

    public static bool IsKnownBackend(string? backend) =>
        !string.IsNullOrWhiteSpace(backend) && OccamBackendPolicyParser.TryParse(backend, out _);

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

    private static PlaybookLintIssue Error(string field, string code, string message) =>
        new("error", field, code, message);
}
