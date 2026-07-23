using System.Text.Json;
using OccamMcp.Core.Extract;
using OccamMcp.Core.Playbooks;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Session;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Services;

public sealed class KnowledgeExtractService(
    PlaybookSeedResolver playbookSeedResolver,
    CssExtractWorker cssExtractWorker,
    WorkerPaths workerPaths)
{
    public KnowledgeExtractResult Extract(
        string url,
        OccamBackendPolicy backendPolicy = OccamBackendPolicy.HttpThenBrowser,
        string? sessionProfile = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(url))
        {
            return KnowledgeExtractResult.Failed(url ?? string.Empty, "invalid_arguments", "url is required.");
        }

        var preflight = FetchPreflight.Prepare(url.Trim(), sessionProfile);
        if (!preflight.Ok)
        {
            return KnowledgeExtractResult.Failed(
                url.Trim(),
                preflight.FailureCode ?? "invalid_arguments",
                preflight.FailureMessage ?? "Invalid URL.");
        }

        var resolved = playbookSeedResolver.ResolveExtended(new PlaybookResolveOptions(url.Trim()));
        if (!resolved.Ok)
        {
            return KnowledgeExtractResult.Failed(
                url.Trim(),
                resolved.FailureCode ?? "playbook_not_found",
                resolved.Message ?? "No playbook for URL.",
                playbookId: resolved.PlaybookId);
        }

        if (string.IsNullOrWhiteSpace(resolved.RawWinningPlaybookJson))
        {
            return KnowledgeExtractResult.Failed(
                url.Trim(),
                "playbook_not_found",
                "No playbook document for URL.",
                playbookId: resolved.PlaybookId);
        }

        var playbookRoot = PlaybookGenomeMerger.ParseRoot(resolved.RawWinningPlaybookJson);
        if (!KnowledgeSchemaPlanner.TryMatch(playbookRoot, url.Trim(), out var schemaMatch, out var schemaFailure))
        {
            return KnowledgeExtractResult.Failed(
                url.Trim(),
                schemaFailure ?? "knowledge_schema_missing",
                FormatSchemaFailureMessage(schemaFailure),
                playbookId: resolved.PlaybookId,
                pageClass: resolved.PageClass);
        }

        FieldExtractionPlan plan;
        try
        {
            plan = FieldSpecParser.ParseFromSchemaFields(schemaMatch!.SchemaFields);
        }
        catch (ArgumentException ex)
        {
            return KnowledgeExtractResult.Failed(
                url.Trim(),
                "invalid_arguments",
                ex.Message,
                playbookId: resolved.PlaybookId,
                pageClass: schemaMatch!.PageClass);
        }

        if (!workerPaths.IsCssExtractConfigured)
        {
            return KnowledgeExtractResult.Failed(
                url.Trim(),
                "workers_unavailable",
                "Occam CSS extract worker is not installed. Run occam doctor.",
                playbookId: resolved.PlaybookId,
                pageClass: schemaMatch.PageClass);
        }

        using (preflight.HeadersScope)
        {
            var headersFile = preflight.ActiveHeadersFile;
            var effectivePolicy = ResolveEffectiveBackendPolicy(backendPolicy, resolved.PreferredBackend);
            var browserFallback = effectivePolicy is OccamBackendPolicy.Browser or OccamBackendPolicy.HttpThenBrowser;

            var extract = cssExtractWorker.Extract(
                url.Trim(),
                plan,
                browserFallback: false,
                headersFile: headersFile);

            if (!extract.Ok
                && browserFallback
                && effectivePolicy == OccamBackendPolicy.HttpThenBrowser
                && ShouldRetryWithBrowser(extract.FailureCode))
            {
                extract = cssExtractWorker.Extract(
                    url.Trim(),
                    plan,
                    browserFallback: true,
                    headersFile: headersFile);
            }

            if (!extract.Ok)
            {
                var code = FailureCodeStrings.Normalize(extract.FailureCode ?? "extraction_failed");
                if (code == "content_extraction_failed")
                {
                    code = "extraction_failed";
                }

                return KnowledgeExtractResult.Failed(
                    url.Trim(),
                    code,
                    FailureCodeStrings.FormatExtractKnowledgeMessage(code, extract.FailureMessage),
                    playbookId: resolved.PlaybookId,
                    pageClass: schemaMatch.PageClass,
                    latencyMs: extract.LatencyMs,
                    partialFacts: BuildFacts(plan, extract.Data));
            }

            var facts = BuildFacts(plan, extract.Data);
            return new KnowledgeExtractResult
            {
                Ok = true,
                Url = url.Trim(),
                PlaybookId = resolved.PlaybookId ?? string.Empty,
                PageClass = schemaMatch.PageClass,
                Facts = facts,
                Meta = new KnowledgeExtractMeta(KnowledgeObjectIds.ComputeKoId(url.Trim())),
                LatencyMs = extract.LatencyMs,
                Backend = extract.Backend,
            };
        }
    }

    private static OccamBackendPolicy ResolveEffectiveBackendPolicy(
        OccamBackendPolicy requested,
        string? playbookPreferredBackend)
    {
        if (string.IsNullOrWhiteSpace(playbookPreferredBackend))
        {
            return requested;
        }

        if (requested != OccamBackendPolicy.HttpThenBrowser)
        {
            return requested;
        }

        return OccamBackendPolicyParser.TryParse(playbookPreferredBackend, out var parsed)
            ? parsed
            : requested;
    }

    private static bool ShouldRetryWithBrowser(string? failureCode)
    {
        var normalized = FailureCodeStrings.Normalize(failureCode);
        return normalized is "http_401" or "http_403" or "http_429" or "timeout" or "extraction_failed";
    }

    private static string FormatSchemaFailureMessage(string? code) => code switch
    {
        "playbook_not_found" => "No playbook for URL.",
        "knowledge_schema_missing" => "Playbook has no knowledge_schema block.",
        "page_class_unmatched" => "URL did not match any page_class and no default schema exists.",
        "knowledge_schema_empty" => "Matched page class has zero schema fields.",
        _ => $"Knowledge schema not applicable: {code}.",
    };

    private static IReadOnlyList<KnowledgeFact> BuildFacts(FieldExtractionPlan plan, JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<KnowledgeFact>();
        }

        var facts = new List<KnowledgeFact>();
        foreach (var field in plan.Fields)
        {
            if (!data.TryGetProperty(field.Key, out var value))
            {
                continue;
            }

            var text = FormatFactValue(value);
            facts.Add(new KnowledgeFact(field.Key, text, field.Value.Selector));
        }

        return facts;
    }

    private static string FormatFactValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => string.Empty,
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Array => string.Join(
            "; ",
            value.EnumerateArray()
                .Select(FormatFactValue)
                .Where(v => !string.IsNullOrWhiteSpace(v))),
        _ => value.GetRawText(),
    };
}

public sealed record KnowledgeFact(string Name, string Value, string Selector);

public sealed record KnowledgeExtractMeta(string KoId);

public sealed class KnowledgeExtractResult
{
    public required bool Ok { get; init; }
    public required string Url { get; init; }
    public string? PlaybookId { get; init; }
    public string? PageClass { get; init; }
    public IReadOnlyList<KnowledgeFact>? Facts { get; init; }
    public KnowledgeExtractMeta? Meta { get; init; }
    public int LatencyMs { get; init; }
    public string? Backend { get; init; }
    public double Confidence { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureMessage { get; init; }
    public IReadOnlyList<KnowledgeFact>? PartialFacts { get; init; }

    public static KnowledgeExtractResult Failed(
        string url,
        string failureCode,
        string message,
        string? playbookId = null,
        string? pageClass = null,
        int latencyMs = 0,
        IReadOnlyList<KnowledgeFact>? partialFacts = null) =>
        new()
        {
            Ok = false,
            Url = url,
            FailureCode = failureCode,
            FailureMessage = message,
            PlaybookId = playbookId,
            PageClass = pageClass,
            LatencyMs = latencyMs,
            PartialFacts = partialFacts,
        };
}
