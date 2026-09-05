using System.Text.Json;
using System.Text.Json.Serialization;

namespace OccamMcp.Core.Playbooks;

/// <summary>
/// Mechanical (non-LLM) playbook stub from heal <c>mainCandidates</c>.
/// Callers must review before <c>occam_playbook_save</c>.
/// </summary>
public static class PlaybookHealDraftBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static string? TryBuildJson(string url, PlaybookHealAnchors? anchors)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            return null;
        }

        var selectors = (anchors?.MainCandidates ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c.Selector) && c.Score >= 0.45)
            .OrderByDescending(c => c.Score)
            .Select(c => c.Selector.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToArray();

        if (selectors.Length == 0)
        {
            return null;
        }

        var host = uri.Host.Trim().TrimEnd('.').ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host["www.".Length..];
        }

        var draft = new HealDraftDocument(
            "1.0",
            host,
            [host],
            new HealDraftMeta($"heal draft for {host}", ["heal_draft"]),
            new HealDraftRouting("browser"),
            new HealDraftExtract(selectors),
            "Mechanical stub from occam_playbook_heal mainCandidates — review selectors before occam_playbook_save.");

        return JsonSerializer.Serialize(draft, JsonOptions);
    }

    private sealed record HealDraftDocument(
        [property: JsonPropertyName("schema_version")] string SchemaVersion,
        string Id,
        string[] Hosts,
        HealDraftMeta Meta,
        HealDraftRouting Routing,
        HealDraftExtract Extract,
        [property: JsonPropertyName("agent_notes")] string AgentNotes);

    private sealed record HealDraftMeta(string Title, string[] Tags);

    private sealed record HealDraftRouting(
        [property: JsonPropertyName("preferred_backend")] string PreferredBackend);

    private sealed record HealDraftExtract(
        [property: JsonPropertyName("contentSelectors")] string[] ContentSelectors);
}
