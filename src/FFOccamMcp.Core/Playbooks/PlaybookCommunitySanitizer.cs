using System.Text.Json;

namespace OccamMcp.Core.Playbooks;

/// <summary>PB4c export validation — secrets, denylisted selectors, agent_notes markers.</summary>
public static class PlaybookCommunitySanitizer
{
    private static readonly HashSet<string> ForbiddenHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cookie",
        "Authorization",
        "Proxy-Authorization",
        "X-Api-Key",
        "Api-Key",
    };

    private static readonly HashSet<string> DenylistSelectors = new(StringComparer.OrdinalIgnoreCase)
    {
        "body",
        "html",
        "*",
    };

    private static readonly string[] ForbiddenNoteMarkers =
    [
        "cookie:",
        "authorization:",
        "bearer ",
        "password=",
        "sid=",
    ];

    public static bool TryFindExportViolations(string json, out string[] violations)
    {
        var list = new List<string>();
        if (PlaybookCommunityHygiene.ContainsForbiddenKeys(json))
        {
            list.Add("forbidden_key");
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                list.Add("invalid_json");
            }
            else
            {
                CheckRequestHeaders(doc.RootElement, list);
                CheckSelectors(doc.RootElement, list);
                CheckAgentNotes(doc.RootElement, list);
            }
        }
        catch (JsonException)
        {
            list.Add("invalid_json");
        }

        violations = list.ToArray();
        return violations.Length == 0;
    }

    private static void CheckRequestHeaders(JsonElement document, List<string> violations)
    {
        if (!document.TryGetProperty("request", out var request)
            || !request.TryGetProperty("headers", out var headers)
            || headers.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in headers.EnumerateObject())
        {
            if (ForbiddenHeaderNames.Contains(property.Name))
            {
                violations.Add($"forbidden_header:{property.Name}");
            }
        }
    }

    private static void CheckSelectors(JsonElement document, List<string> violations)
    {
        if (!document.TryGetProperty("extract", out var extract) || extract.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var field in new[] { "contentSelectors", "domStripSelectors" })
        {
            if (!extract.TryGetProperty(field, out var selectors) || selectors.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in selectors.EnumerateArray())
            {
                var selector = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
                if (string.IsNullOrWhiteSpace(selector))
                {
                    continue;
                }

                if (IsDenylistedSelector(selector))
                {
                    violations.Add($"denylist_selector:{selector}");
                }
            }
        }
    }

    private static void CheckAgentNotes(JsonElement document, List<string> violations)
    {
        if (!document.TryGetProperty("agent_notes", out var notesEl)
            || notesEl.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var notes = notesEl.GetString();
        if (string.IsNullOrWhiteSpace(notes))
        {
            return;
        }

        foreach (var marker in ForbiddenNoteMarkers)
        {
            if (notes.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                violations.Add("forbidden_agent_notes");
                return;
            }
        }
    }

    private static bool IsDenylistedSelector(string selector)
    {
        var normalized = selector.Trim();
        if (DenylistSelectors.Contains(normalized))
        {
            return true;
        }

        return normalized.Contains("[style*=", StringComparison.OrdinalIgnoreCase);
    }
}
