using System.Text.Json;

namespace OccamMcp.Core.Playbooks;

public sealed record PlaybookDocument(
    string SchemaVersion,
    string Id,
    string[] Hosts,
    string? PreferredBackend)
{
    public static PlaybookDocument? TryParse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var schemaVersion = root.TryGetProperty("schema_version", out var versionEl)
                ? versionEl.GetString()
                : null;
            var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(schemaVersion)
                || string.IsNullOrWhiteSpace(id)
                || !schemaVersion.StartsWith("1.", StringComparison.Ordinal))
            {
                return null;
            }

            if (!root.TryGetProperty("hosts", out var hostsEl) || hostsEl.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var hosts = hostsEl.EnumerateArray()
                .Select(h => h.GetString())
                .Where(h => !string.IsNullOrWhiteSpace(h))
                .Select(h => h!)
                .ToArray();
            if (hosts.Length == 0)
            {
                return null;
            }

            string? preferredBackend = null;
            if (root.TryGetProperty("routing", out var routingEl)
                && routingEl.ValueKind == JsonValueKind.Object
                && routingEl.TryGetProperty("preferred_backend", out var backendEl))
            {
                preferredBackend = backendEl.GetString();
            }

            return new PlaybookDocument(schemaVersion, id, hosts, preferredBackend);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool HostMatches(string verifyHost, IEnumerable<string> playbookHosts)
    {
        var normalized = NormalizeHost(verifyHost);
        return playbookHosts.Any(h => MatchesHost(normalized, h));
    }

    public static string NormalizeHost(string host)
    {
        var lower = host.ToLowerInvariant();
        return lower.StartsWith("www.", StringComparison.Ordinal) ? lower[4..] : lower;
    }

    private static bool MatchesHost(string host, string pattern)
    {
        var normalized = NormalizeHost(pattern);
        return host == normalized || host.EndsWith($".{normalized}", StringComparison.Ordinal);
    }

    public static string AppendLesson(
        string playbookJson,
        string note,
        string? failureReason,
        int? verifyScore,
        string? hostId)
    {
        using var doc = JsonDocument.Parse(playbookJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("playbook_json must be a JSON object.");
        }

        var lessons = new List<JsonElement>();
        if (doc.RootElement.TryGetProperty("lessons", out var lessonsEl)
            && lessonsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in lessonsEl.EnumerateArray())
            {
                lessons.Add(item.Clone());
            }
        }

        lessons.Add(PlaybookJsonElementWriter.CreateLesson(note, failureReason, verifyScore, hostId));

        while (lessons.Count > PlaybookHealPolicy.MaxLessonsPerFile)
        {
            lessons.RemoveAt(0);
        }

        return PlaybookJsonElementWriter.ReplaceRootProperty(
            doc.RootElement,
            "lessons",
            PlaybookJsonElementWriter.CreateArray(lessons),
            indented: true);
    }
}
