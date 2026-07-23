using System.Text.Json;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Playbooks;

public sealed record PlaybookSeedDocument(
    string SchemaVersion,
    string Id,
    string[] Hosts,
    string? PreferredBackend,
    string[] ContentSelectors,
    string? AgentNotes);

public sealed record PlaybookSeedResolveResult(
    bool Ok,
    string Requested,
    string? MatchedHost,
    string? PlaybookId,
    string? SchemaVersion,
    string? Provenance,
    string? SourcePath,
    string[]? ContentSelectors,
    string? PreferredBackend,
    string? AgentNotes,
    string? FailureCode,
    string? Message,
    JsonElement? Genome = null,
    JsonElement? KnowledgeSchema = null,
    string? PageClass = null,
    GenomeFetchMetadata? GenomeFetch = null,
    JsonElement? Lessons = null,
    string? SchemaVersionWarning = null,
    string? RawWinningPlaybookJson = null);

public sealed class PlaybookSeedResolver(WellKnownGenomeFetcher genomeFetcher)
{
    private readonly object _cacheLock = new();
    private IReadOnlyList<PlaybookSeedEntry>? _cache;
    private readonly string? _occamHome = WorkerPaths.ResolveOccamHome();
    private readonly WellKnownGenomeFetcher _genomeFetcher = genomeFetcher;

    public PlaybookSeedResolveResult Resolve(string urlOrHost) =>
        ResolveExtended(new PlaybookResolveOptions(urlOrHost));

    public PlaybookSeedResolveResult ResolveExtended(PlaybookResolveOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.UrlOrHost))
        {
            return Fail(options.UrlOrHost ?? string.Empty, "invalid_arguments", "url must be a valid absolute HTTP or HTTPS URL, or a hostname.");
        }

        var host = ExtractHost(options.UrlOrHost);
        if (string.IsNullOrWhiteSpace(host))
        {
            return Fail(options.UrlOrHost, "invalid_arguments", "url must be a valid absolute HTTP or HTTPS URL, or a hostname.");
        }

        var matches = LoadEntries()
            .Where(entry => entry.Document.Hosts.Any(h => MatchesHost(host, h)))
            .OrderByDescending(entry => entry.TierRank)
            .ToList();
        var match = matches.Count > 0 ? matches[0] : null;

        GenomeFetchMetadata? genomeFetch = null;
        JsonElement? siteRoot = null;
        string? siteRawJson = null;
        if (options.ShouldFetchSiteGenome())
        {
            var fetch = _genomeFetcher.Fetch(options.UrlOrHost.Trim(), host);
            genomeFetch = new GenomeFetchMetadata(
                fetch.Ok,
                fetch.WellKnownUrl,
                fetch.FailureCode,
                fetch.CacheHit);
            if (fetch.Ok && fetch.RawJson is not null)
            {
                siteRawJson = fetch.RawJson;
                siteRoot = PlaybookGenomeMerger.ParseRoot(fetch.RawJson);
            }
        }

        if (match is null)
        {
            if (!siteRoot.HasValue)
            {
                return Fail(options.UrlOrHost, "playbook_not_found", $"No playbook seed for host '{host}'.");
            }

            return BuildSiteOnlyResult(options, host, siteRoot.Value, siteRawJson!, genomeFetch);
        }

        var doc = match.Document;

        // Overlay-with-fallback across tiers: the winning (highest-tier) entry provides the
        // fields it specifies; any flat field it leaves empty falls back to the next matching
        // entry that does specify it — e.g. a curated repo seed underneath a learned local
        // genome. Without this, a partial learned genome (no routing.preferred_backend, or
        // selectors in a non-canonical shape) would silently erase the seed's defaults just by
        // outranking it. Provenance/SourcePath/Id stay the winner's; only the flat values fall back.
        var effectivePreferredBackend = matches
            .Select(m => m.Document.PreferredBackend)
            .FirstOrDefault(b => !string.IsNullOrWhiteSpace(b));
        var effectiveSelectors = matches
            .Select(m => m.Document.ContentSelectors)
            .FirstOrDefault(s => s is { Length: > 0 });
        var effectiveAgentNotes = matches
            .Select(m => m.Document.AgentNotes)
            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));

        var relativePath = ToRelativePath(match.SourcePath);
        var playbookRoot = PlaybookGenomeMerger.ParseRoot(match.RawJson);
        var schemaWarning = ValidateSchemaVersion(options.SchemaVersion, doc.SchemaVersion);

        var mergedGenome = PlaybookGenomeMerger.MergeGenome(
            PlaybookGenomeMerger.GetObject(playbookRoot, "genome"),
            siteRoot is null ? null : PlaybookGenomeMerger.GetObject(siteRoot.Value, "genome"));
        var mergedKnowledgeSchema = PlaybookGenomeMerger.MergeKnowledgeSchema(
            PlaybookGenomeMerger.GetObject(playbookRoot, "knowledge_schema"),
            siteRoot is null ? null : PlaybookGenomeMerger.GetObject(siteRoot.Value, "knowledge_schema"));

        string? pageClass = null;
        JsonElement? matchedSchemaElement = null;
        if (mergedKnowledgeSchema.HasValue && mergedKnowledgeSchema.Value.EnumerateObject().Any())
        {
            var mergedRoot = playbookRoot;
            if (mergedGenome.HasValue)
            {
                mergedRoot = PlaybookJsonElementWriter.ReplaceRootPropertyElement(
                    mergedRoot,
                    "genome",
                    mergedGenome.Value);
            }

            if (mergedKnowledgeSchema.HasValue)
            {
                mergedRoot = PlaybookJsonElementWriter.ReplaceRootPropertyElement(
                    mergedRoot,
                    "knowledge_schema",
                    mergedKnowledgeSchema.Value);
            }

            if (KnowledgeSchemaPlanner.TryMatch(mergedRoot, options.UrlOrHost.Trim(), out var schemaMatch, out _))
            {
                pageClass = schemaMatch!.PageClass;
                matchedSchemaElement = schemaMatch.SchemaFields.Clone();
            }
        }

        JsonElement? lessonsElement = null;
        if (options.IncludeLessons
            && match.Provenance == PlaybookProvenance.Local
            && playbookRoot.TryGetProperty("lessons", out var lessons)
            && lessons.ValueKind == JsonValueKind.Array
            && lessons.GetArrayLength() > 0)
        {
            lessonsElement = PlaybookLessonExporter.ExportRedactedLessons(lessons, LooksLikeToken);
        }

        return new PlaybookSeedResolveResult(
            Ok: true,
            Requested: options.UrlOrHost.Trim(),
            MatchedHost: host,
            PlaybookId: doc.Id,
            SchemaVersion: doc.SchemaVersion,
            Provenance: match.Provenance,
            SourcePath: relativePath,
            ContentSelectors: effectiveSelectors is { Length: > 0 } ? effectiveSelectors : null,
            PreferredBackend: effectivePreferredBackend,
            AgentNotes: effectiveAgentNotes,
            FailureCode: null,
            Message: null,
            Genome: PlaybookGenomeMerger.ToElementOrNull(mergedGenome),
            KnowledgeSchema: matchedSchemaElement,
            PageClass: pageClass,
            GenomeFetch: genomeFetch,
            Lessons: lessonsElement,
            SchemaVersionWarning: schemaWarning,
            RawWinningPlaybookJson: match.RawJson);
    }

    internal void ClearCacheForTests()
    {
        lock (_cacheLock)
        {
            _cache = null;
        }

        _genomeFetcher.ClearCacheForTests();
    }

    private static PlaybookSeedResolveResult BuildSiteOnlyResult(
        PlaybookResolveOptions options,
        string host,
        JsonElement siteRoot,
        string siteRawJson,
        GenomeFetchMetadata? genomeFetch)
    {
        var siteGenome = PlaybookGenomeMerger.GetObject(siteRoot, "genome");
        var siteId = TryGetString(siteRoot, "id") ?? host;
        var siteVersion = TryGetString(siteRoot, "schema_version") ?? "1.0";
        string? preferredBackend = null;
        if (siteRoot.TryGetProperty("routing", out var routing)
            && routing.TryGetProperty("preferred_backend", out var backendEl)
            && backendEl.ValueKind == JsonValueKind.String)
        {
            preferredBackend = backendEl.GetString();
        }

        return new PlaybookSeedResolveResult(
            Ok: true,
            Requested: options.UrlOrHost.Trim(),
            MatchedHost: host,
            PlaybookId: siteId,
            SchemaVersion: siteVersion,
            Provenance: "site",
            SourcePath: ".well-known/agent-genome.v1.json",
            ContentSelectors: null,
            PreferredBackend: preferredBackend,
            AgentNotes: null,
            FailureCode: null,
            Message: null,
            Genome: PlaybookGenomeMerger.ToElementOrNull(siteGenome),
            GenomeFetch: genomeFetch,
            RawWinningPlaybookJson: siteRawJson);
    }

    private static string? TryGetString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? ValidateSchemaVersion(string requested, string playbookVersion)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return null;
        }

        if (!Version.TryParse(requested, out var requestedVersion)
            || !Version.TryParse(playbookVersion, out var playbookParsed))
        {
            return null;
        }

        if (requestedVersion.Major != playbookParsed.Major)
        {
            return $"schema_version {requested} major mismatch with playbook {playbookVersion}";
        }

        if (requestedVersion > playbookParsed)
        {
            return $"schema_version {requested} is newer than playbook {playbookVersion}";
        }

        return null;
    }

    private static bool LooksLikeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Length >= 24
            || value.Contains("token", StringComparison.OrdinalIgnoreCase)
            || value.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || value.Contains("api_key", StringComparison.OrdinalIgnoreCase);
    }

    private static PlaybookSeedResolveResult Fail(string requested, string code, string message) =>
        new(false, requested, null, null, null, null, null, null, null, null, code, message);

    private static string? ExtractHost(string urlOrHost)
    {
        var trimmed = urlOrHost.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return NormalizeHost(uri.Host);
        }

        if (trimmed.Contains('/') || trimmed.Contains(':'))
        {
            return null;
        }

        return NormalizeHost(trimmed);
    }

    private static string NormalizeHost(string host)
    {
        var normalized = host.ToLowerInvariant();
        return normalized.StartsWith("www.", StringComparison.Ordinal) ? normalized[4..] : normalized;
    }

    private static bool MatchesHost(string host, string pattern)
    {
        var normalized = NormalizeHost(pattern);
        return host == normalized || host.EndsWith($".{normalized}", StringComparison.Ordinal);
    }

    private string? ToRelativePath(string absolutePath)
    {
        if (_occamHome is null)
        {
            return absolutePath.Replace('\\', '/');
        }

        var fullHome = Path.GetFullPath(_occamHome);
        var fullPath = Path.GetFullPath(absolutePath);
        if (fullPath.StartsWith(fullHome, StringComparison.OrdinalIgnoreCase))
        {
            return fullPath[fullHome.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace('\\', '/');
        }

        return absolutePath.Replace('\\', '/');
    }

    private IReadOnlyList<PlaybookSeedEntry> LoadEntries()
    {
        lock (_cacheLock)
        {
            if (_cache is not null)
            {
                return _cache;
            }

            var entries = new List<PlaybookSeedEntry>();
            if (_occamHome is not null)
            {
                entries.AddRange(LoadDirectory(
                    Path.Combine(_occamHome, "profiles", "playbooks", "seeds"),
                    PlaybookProvenance.Seed,
                    ["*.seed.json"]));
                entries.AddRange(LoadCommunityDirectory(
                    Path.Combine(_occamHome, "profiles", "playbooks", "community")));
            }

            entries.AddRange(LoadDirectory(
                PlaybookPaths.ResolveUserPlaybooksPath(),
                PlaybookProvenance.User,
                ["*.json", "*.playbook.json", "*.seed.json"]));
            entries.AddRange(LoadDirectory(
                PlaybookPaths.ResolveLocalRoot(),
                PlaybookProvenance.Local,
                ["*.playbook.json", "*.json"]));

            _cache = entries;
            return _cache;
        }
    }

    private static IEnumerable<PlaybookSeedEntry> LoadCommunityDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            yield break;
        }

        var fileHashes = CommunityManifest.TryLoadFileHashes(directory);
        if (fileHashes is null || fileHashes.Count == 0)
        {
            yield break;
        }

        var tierRank = PlaybookProvenance.TierRank(PlaybookProvenance.Community);
        foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
        {
            var fileName = Path.GetFileName(file);
            if (string.Equals(fileName, "manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!CommunityManifest.TryReadVerifiedPlaybook(file, fileName, fileHashes, out var json))
            {
                continue;
            }

            var document = TryParseSeed(json!);
            if (document is not null)
            {
                yield return new PlaybookSeedEntry(
                    document,
                    json!,
                    file,
                    PlaybookProvenance.Community,
                    tierRank);
            }
        }
    }

    private static IEnumerable<PlaybookSeedEntry> LoadDirectory(
        string? directory,
        string provenance,
        string[] patterns,
        bool requireHygiene = false)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            yield break;
        }

        var tierRank = PlaybookProvenance.TierRank(provenance);
        var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pattern in patterns)
        {
            foreach (var file in Directory.EnumerateFiles(directory, pattern))
            {
                if (!seenFiles.Add(file))
                {
                    continue;
                }

                if (string.Equals(Path.GetFileName(file), "manifest.json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var json = File.ReadAllText(file);
                if (requireHygiene && PlaybookCommunityHygiene.ContainsForbiddenKeys(json))
                {
                    continue;
                }

                var document = TryParseSeed(json);
                if (document is not null)
                {
                    yield return new PlaybookSeedEntry(document, json, file, provenance, tierRank);
                }
            }
        }
    }

    private static PlaybookSeedDocument? TryParseSeed(string json)
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

            var selectors = Array.Empty<string>();
            if (root.TryGetProperty("extract", out var extractEl)
                && extractEl.ValueKind == JsonValueKind.Object
                && (extractEl.TryGetProperty("contentSelectors", out var selectorsEl)
                    || extractEl.TryGetProperty("content_selectors", out selectorsEl))
                && selectorsEl.ValueKind == JsonValueKind.Array)
            {
                selectors = selectorsEl.EnumerateArray()
                    .Select(s => s.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!)
                    .ToArray();
            }

            var agentNotes = root.TryGetProperty("agent_notes", out var notesEl) ? notesEl.GetString() : null;

            return new PlaybookSeedDocument(schemaVersion, id, hosts, preferredBackend, selectors, agentNotes);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record PlaybookSeedEntry(
        PlaybookSeedDocument Document,
        string RawJson,
        string SourcePath,
        string Provenance,
        int TierRank);
}
