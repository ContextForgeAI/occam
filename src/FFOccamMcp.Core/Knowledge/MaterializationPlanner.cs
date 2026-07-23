using OccamMcp.Core.Compile;
using OccamMcp.Core.Knowledge.Canonical;
using OccamMcp.Core.Knowledge.Extraction;
using OccamMcp.Core.Knowledge.Legacy;
using OccamMcp.Core.Routing;

namespace OccamMcp.Core.Knowledge;

/// <summary>
/// What the planner is allowed to weigh when preparing knowledge for a model (ADR-0002). Semantic
/// inputs only — how to serialize is the codec's job.
/// </summary>
public sealed record MaterializationPolicy(
    int? MaxTokens = null,
    string? FocusQuery = null,
    string? ProvenancePolicy = null)
{
    public static readonly MaterializationPolicy None = new();
}

/// <summary>
/// Materialization Planner: turns an <see cref="ExtractedKnowledgeBundle"/> (Canonical + document IR +
/// opaque source surface) into a task-shaped <see cref="MaterializedKnowledgeView"/>. Owns SEMANTIC
/// retention (selectors, fit/focus, <b>surface</b> token budget for markdown + document IR, Canonical
/// claim / evidence retention). Does <b>not</b> own whole-response sidecar trim — that is
/// <c>Compile.ResponseBudgetPlanner</c> via <c>Compile.BudgetOwnership</c> (Occam 1.1 R6). Codecs never
/// select or drop. Live transcode wires this before the default codec.
/// </summary>
public sealed class MaterializationPlanner
{
    /// <summary>
    /// Authoritative plan for the live path: apply semantic compile to the source surface, budget the
    /// document IR, and retain Canonical refs under <see cref="MaterializationRequest.ProvenancePolicy"/>.
    /// </summary>
    public MaterializationResult Plan(MaterializationRequest request, ExtractedKnowledgeBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(bundle);

        var compiled = TranscodeCompiler.Apply(bundle.SourceSurface.Text, request.ToCompileOptions());
        var plannedDoc = bundle.Document;
        if (!plannedDoc.IsEmpty && request.MaxTokens is int budget)
        {
            plannedDoc = RetainWithinBudget(plannedDoc, budget);
        }

        var retainedCanonical = CanonicalRetention.Retain(
            bundle.Canonical,
            request.ProvenancePolicy,
            request.MaxTokens,
            request.FocusQuery);

        var view = BuildView(
            SourceSurface.Markdown(compiled.Markdown),
            plannedDoc,
            retainedCanonical);

        var assessment = MaterializationAssessmentEvaluator.Evaluate(
            bundle.SourceSurface.Text,
            compiled.Markdown,
            request.FocusQuery,
            request.FocusFragment,
            compiled.Truncated);

        return new MaterializationResult(
            view,
            compiled.SelectorsMatched,
            compiled.Truncated,
            compiled.TokensEstimated,
            compiled.TruncationStrategy,
            compiled.Omitted,
            assessment);
    }

    /// <summary>
    /// Compatibility overload used by unit tests and table materializer: policy-only IR budgeting with
    /// an already-produced surface (no selectors/fit re-application).
    /// </summary>
    public MaterializedKnowledgeView Plan(
        string markdown,
        KnowledgeDocument? document,
        MaterializationPolicy policy,
        IReadOnlyList<Source>? sourceRefs = null,
        IReadOnlyList<Evidence>? evidenceRefs = null,
        IReadOnlyList<ClaimCandidate>? claims = null,
        IReadOnlyList<KnowledgeProvenance>? provenance = null)
    {
        KnowledgeDocument? planned = document;
        if (document is not null && !document.IsEmpty && policy.MaxTokens is int budget)
        {
            planned = RetainWithinBudget(document, budget);
        }

        CanonicalExtract? input = null;
        if (sourceRefs is { Count: > 0 } || evidenceRefs is { Count: > 0 } || claims is { Count: > 0 } || provenance is { Count: > 0 })
        {
            var source = sourceRefs is { Count: > 0 }
                ? sourceRefs[0]
                : Source.Create(
                    SourceId.New(),
                    SourceKind.WebPage,
                    "urn:occam:planner-compat",
                    DateTimeOffset.UnixEpoch);
            input = new CanonicalExtract(
                source,
                evidenceRefs ?? Array.Empty<Evidence>(),
                claims ?? Array.Empty<ClaimCandidate>(),
                provenance ?? Array.Empty<KnowledgeProvenance>());
        }

        var retained = CanonicalRetention.Retain(
            input,
            policy.ProvenancePolicy,
            policy.MaxTokens,
            policy.FocusQuery);

        return BuildView(SourceSurface.Markdown(markdown), planned, retained);
    }

    /// <summary>
    /// Convenience: attach a <see cref="CanonicalExtract"/> from the Legacy Adapter. Document-IR budget
    /// and Canonical retention both apply under <paramref name="policy"/>.
    /// </summary>
    public MaterializedKnowledgeView Plan(
        string markdown,
        KnowledgeDocument? document,
        MaterializationPolicy policy,
        CanonicalExtract? canonical)
    {
        KnowledgeDocument? planned = document;
        if (document is not null && !document.IsEmpty && policy.MaxTokens is int budget)
        {
            planned = RetainWithinBudget(document, budget);
        }

        var retained = CanonicalRetention.Retain(
            canonical,
            policy.ProvenancePolicy,
            policy.MaxTokens,
            policy.FocusQuery);

        return BuildView(SourceSurface.Markdown(markdown), planned, retained);
    }

    private static MaterializedKnowledgeView BuildView(
        SourceSurface surface,
        KnowledgeDocument? document,
        CanonicalExtract? canonical)
    {
        if (canonical is null)
        {
            return new MaterializedKnowledgeView(surface, document);
        }

        return new MaterializedKnowledgeView(
            surface,
            document,
            SourceRefs: [canonical.Source],
            EvidenceRefs: canonical.Evidence,
            Claims: canonical.Claims,
            Provenance: canonical.Provenance);
    }

    // Greedy retention: keep as many assertions as the budget allows. When the blocks carry salience
    // (#3), keep the most salient first and then restore reading order for the surface; otherwise keep a
    // prefix in document order. Tables are kept after blocks while budget remains.
    private static KnowledgeDocument RetainWithinBudget(KnowledgeDocument doc, int budget)
    {
        var indexed = doc.Blocks
            .Select((b, i) => (Block: b, Index: i, Cost: Math.Max(1, TokenEstimator.Estimate(b.Text))))
            .ToList();

        var anySalience = indexed.Any(x => x.Block.Salience is > 0);
        var priority = anySalience
            ? indexed.OrderByDescending(x => x.Block.Salience ?? 0).ThenBy(x => x.Index)
            : indexed.OrderBy(x => x.Index);

        var keptIndices = new List<int>();
        var used = 0;
        foreach (var x in priority)
        {
            if (used + x.Cost > budget)
            {
                if (anySalience)
                {
                    continue; // a smaller later block may still fit
                }

                break; // order-preserving prefix stops at the first overflow
            }

            used += x.Cost;
            keptIndices.Add(x.Index);
        }

        keptIndices.Sort(); // surface always in reading order, regardless of retention priority
        var keptBlocks = keptIndices.Select(i => doc.Blocks[i]).ToList();

        var keptTables = new List<KnowledgeTable>();
        foreach (var t in doc.Tables)
        {
            var cost = EstimateTable(t);
            if (used + cost > budget)
            {
                break;
            }

            used += cost;
            keptTables.Add(t);
        }

        return new KnowledgeDocument(keptBlocks, keptTables);
    }

    private static int EstimateTable(KnowledgeTable t)
    {
        var total = string.IsNullOrEmpty(t.Caption) ? 0 : TokenEstimator.Estimate(t.Caption);
        total += TokenEstimator.Estimate(string.Join(" ", t.Headers));
        foreach (var row in t.Rows)
        {
            total += TokenEstimator.Estimate(string.Join(" ", row));
        }

        if (t.SemanticRows is { Count: > 0 })
        {
            foreach (var s in t.SemanticRows)
            {
                total += TokenEstimator.Estimate(s.Title)
                    + TokenEstimator.Estimate(s.Url)
                    + TokenEstimator.Estimate(s.Author)
                    + TokenEstimator.Estimate(s.Site)
                    + TokenEstimator.Estimate(s.Age);
            }
        }

        return Math.Max(1, total);
    }
}
