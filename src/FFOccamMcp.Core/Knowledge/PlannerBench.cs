using System.Diagnostics;
using System.Text;
using OccamMcp.Core.Codecs;
using OccamMcp.Core.Compile;
using OccamMcp.Core.Knowledge.Extraction;

namespace OccamMcp.Core.Knowledge;

/// <summary>
/// One planner-policy case for <see cref="PlannerBench"/> (evaluation harness — not a public MCP profile).
/// </summary>
public sealed record PlannerBenchCase(string CaseId, MaterializationRequest Request);

/// <summary>Metrics collected from one planned <see cref="MaterializedKnowledgeView"/> (R3 expanded).</summary>
public sealed record PlannerBenchMetrics(
    // Surface
    int SurfaceChars,
    int SurfaceUtf8Bytes,
    int SurfaceTokensEstimated,
    string SurfaceMediaType,
    bool SurfaceEmpty,
    // Token economy vs input surface
    int InputSurfaceTokensEstimated,
    /// <summary>1 − retained/input; null when input tokens are 0. Higher ⇒ more reduction.</summary>
    double? TokenReductionRatio,
    // Structural
    int RetainedBlocks,
    int RetainedTables,
    int RetainedHeadings,
    int RetainedListItems,
    int RetainedCodeBlocks,
    int RetainedQuotes,
    int? OmittedBlocks,
    int InputBlocks,
    int InputTables,
    /// <summary>1 − retainedBlocks/inputBlocks; null when input blocks are 0.</summary>
    double? BlockRetentionRatio,
    // Canonical refs
    int SourceRefs,
    int EvidenceRefs,
    int Claims,
    int ProvenanceItems,
    int InputClaims,
    int InputEvidence,
    /// <summary>retainedClaims/inputClaims; null when input claims are 0.</summary>
    double? ClaimRetentionRatio,
    /// <summary>retainedEvidence/inputEvidence; null when input evidence is 0.</summary>
    double? EvidenceRetentionRatio,
    // Integrity
    bool ClaimEvidenceRefsResolve,
    bool EvidenceSourceRefsResolve,
    bool ProvenanceConsistent,
    bool NoDuplicateIds,
    bool IntegrityOk,
    // Focus (null when no focus query)
    double? FocusTermCoverage,
    int? FocusMatchingBlocks,
    double? FocusMatchingBlockRatio,
    bool? DefinitionalAnchorPreserved,
    /// <summary>Fraction of retained claims whose statement matches FocusQuery; null when no focus or no claims.</summary>
    double? FocusClaimHitRatio,
    // Execution
    bool DeterministicOk,
    /// <summary>True when N≥3 Plan invocations produced identical views (planner stability).</summary>
    bool StabilityOk,
    int PlannerInvocations,
    string TokenEstimatorId,
    /// <summary>
    /// Honest notes: request fields with no planner effect, policy behaviour, etc.
    /// </summary>
    IReadOnlyList<string> Notes);

/// <summary>One planner case result.</summary>
public sealed record PlannerBenchResult(
    string CaseId,
    MaterializationRequest Request,
    MaterializationResult PlanningResult,
    PlannerBenchMetrics Metrics,
    TimeSpan Duration);

/// <summary>
/// Combined planner × codec matrix row: planner runs once; codecs encode the planned view.
/// </summary>
public sealed record PlannerCodecBenchResult(
    PlannerBenchResult PlannerCase,
    PlannerBenchMetrics PlannerMetrics,
    IReadOnlyList<CodecBenchRow> CodecResults);

/// <summary>
/// Relative comparison of one policy against a baseline (usually <c>compat</c>).
/// </summary>
public sealed record PlannerBenchDelta(
    string CaseId,
    string BaselineCaseId,
    int SurfaceTokens,
    int BaselineSurfaceTokens,
    /// <summary>1 − case/baseline surface tokens; null when baseline is 0.</summary>
    double? RelativeTokenReduction,
    int Claims,
    int BaselineClaims,
    double? RelativeClaimRetention,
    int EvidenceRefs,
    int BaselineEvidence,
    bool IntegrityOk,
    bool DeterministicOk,
    bool StabilityOk);

/// <summary>
/// ADR-0002 / Occam 1.1 R3 planner-benchmark harness: same <see cref="ExtractedKnowledgeBundle"/> →
/// different <see cref="MaterializationRequest"/> policies → compare retained knowledge. Does not
/// implement selection logic — always calls <see cref="MaterializationPlanner.Plan"/>. Not part of live runtime.
/// </summary>
public sealed class PlannerBench
{
    public const int DefaultStabilityIterations = 3;

    private readonly MaterializationPlanner _planner;

    public PlannerBench(MaterializationPlanner? planner = null)
    {
        _planner = planner ?? new MaterializationPlanner();
    }

    /// <summary>Run every case against the same immutable bundle (planner once per case, twice for determinism + stability loop).</summary>
    public IReadOnlyList<PlannerBenchResult> Run(
        ExtractedKnowledgeBundle bundle,
        IReadOnlyList<PlannerBenchCase> cases,
        int stabilityIterations = DefaultStabilityIterations)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(cases);

        var results = new List<PlannerBenchResult>(cases.Count);
        foreach (var c in cases)
        {
            results.Add(RunCase(bundle, c, stabilityIterations));
        }

        return results;
    }

    /// <summary>
    /// Plan once per case, then encode each planned view with the supplied codecs via
    /// <see cref="CodecBench.RunFixedView"/> (planner is not re-run per codec).
    /// </summary>
    public IReadOnlyList<PlannerCodecBenchResult> RunWithCodecs(
        ExtractedKnowledgeBundle bundle,
        IReadOnlyList<PlannerBenchCase> cases,
        IEnumerable<IKnowledgeCodec> codecs,
        int stabilityIterations = DefaultStabilityIterations)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentNullException.ThrowIfNull(codecs);
        var codecList = codecs as IReadOnlyList<IKnowledgeCodec> ?? codecs.ToArray();

        var rows = new List<PlannerCodecBenchResult>(cases.Count);
        foreach (var c in cases)
        {
            var planned = RunCase(bundle, c, stabilityIterations);
            var codecRows = CodecBench.RunFixedView(planned.PlanningResult.View, codecList);
            rows.Add(new PlannerCodecBenchResult(planned, planned.Metrics, codecRows));
        }

        return rows;
    }

    /// <summary>
    /// Compare each non-baseline case against <paramref name="baselineCaseId"/> (default <c>compat</c>).
    /// </summary>
    public static IReadOnlyList<PlannerBenchDelta> CompareToBaseline(
        IReadOnlyList<PlannerBenchResult> results,
        string baselineCaseId = "compat")
    {
        ArgumentNullException.ThrowIfNull(results);
        var baseline = results.FirstOrDefault(r =>
            string.Equals(r.CaseId, baselineCaseId, StringComparison.OrdinalIgnoreCase));
        if (baseline is null)
        {
            return [];
        }

        var deltas = new List<PlannerBenchDelta>();
        foreach (var r in results)
        {
            if (string.Equals(r.CaseId, baseline.CaseId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var bt = baseline.Metrics.SurfaceTokensEstimated;
            var bc = baseline.Metrics.Claims;
            var be = baseline.Metrics.EvidenceRefs;
            deltas.Add(new PlannerBenchDelta(
                r.CaseId,
                baseline.CaseId,
                r.Metrics.SurfaceTokensEstimated,
                bt,
                bt <= 0 ? null : 1.0 - (r.Metrics.SurfaceTokensEstimated / (double)bt),
                r.Metrics.Claims,
                bc,
                bc <= 0 ? null : r.Metrics.Claims / (double)bc,
                r.Metrics.EvidenceRefs,
                be,
                r.Metrics.IntegrityOk,
                r.Metrics.DeterministicOk,
                r.Metrics.StabilityOk));
        }

        return deltas;
    }

    private PlannerBenchResult RunCase(
        ExtractedKnowledgeBundle bundle,
        PlannerBenchCase c,
        int stabilityIterations)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (string.IsNullOrWhiteSpace(c.CaseId))
        {
            throw new ArgumentException("CaseId must be non-empty.", nameof(c));
        }

        var request = c.Request ?? MaterializationRequest.None;
        var iterations = Math.Max(2, stabilityIterations);

        var sw = Stopwatch.StartNew();
        var first = _planner.Plan(request, bundle);
        sw.Stop();

        var plans = new List<MaterializationResult>(iterations) { first };
        for (var i = 1; i < iterations; i++)
        {
            plans.Add(_planner.Plan(request, bundle));
        }

        var deterministicOk = true;
        var stabilityOk = true;
        for (var i = 1; i < plans.Count; i++)
        {
            var equal = ViewsEqual(plans[0].View, plans[i].View)
                && plans[0].SelectorsMatched == plans[i].SelectorsMatched
                && plans[0].Truncated == plans[i].Truncated
                && plans[0].TokensEstimated == plans[i].TokensEstimated
                && plans[0].TruncationStrategy == plans[i].TruncationStrategy;
            if (!equal)
            {
                deterministicOk = false;
                stabilityOk = false;
                break;
            }
        }

        // DeterministicOk keeps the historical meaning: at least the first pair matches.
        if (plans.Count >= 2)
        {
            deterministicOk = ViewsEqual(plans[0].View, plans[1].View)
                && plans[0].SelectorsMatched == plans[1].SelectorsMatched
                && plans[0].Truncated == plans[1].Truncated
                && plans[0].TokensEstimated == plans[1].TokensEstimated
                && plans[0].TruncationStrategy == plans[1].TruncationStrategy;
        }

        var metrics = CollectMetrics(bundle, request, first, deterministicOk, stabilityOk, iterations);

        return new PlannerBenchResult(c.CaseId, request, first, metrics, sw.Elapsed);
    }

    internal static PlannerBenchMetrics CollectMetrics(
        ExtractedKnowledgeBundle bundle,
        MaterializationRequest request,
        MaterializationResult result,
        bool deterministicOk,
        bool stabilityOk,
        int plannerInvocations)
    {
        var view = result.View;
        var surface = view.Surface.Text ?? string.Empty;
        var inputSurface = bundle.SourceSurface.Text ?? string.Empty;
        var inputSurfaceTokens = TokenEstimator.Estimate(inputSurface);
        var retainedTokens = TokenEstimator.Estimate(surface);
        double? tokenReduction = inputSurfaceTokens <= 0
            ? null
            : Math.Clamp(1.0 - (retainedTokens / (double)inputSurfaceTokens), 0.0, 1.0);

        var doc = view.Knowledge;
        var inputBlocks = bundle.Document.Blocks.Count;
        var inputTables = bundle.Document.Tables.Count;
        var retainedBlocks = doc?.Blocks.Count ?? 0;
        var retainedTables = doc?.Tables.Count ?? 0;
        double? blockRetention = inputBlocks <= 0
            ? null
            : retainedBlocks / (double)inputBlocks;

        int headings = 0, lists = 0, code = 0, quotes = 0;
        if (doc is not null)
        {
            foreach (var b in doc.Blocks)
            {
                switch (b.Type)
                {
                    case "heading": headings++; break;
                    case "list_item": lists++; break;
                    case "code": code++; break;
                    case "quote": quotes++; break;
                }
            }
        }

        int? omittedBlocks = null;
        if (doc is not null || inputBlocks > 0)
        {
            omittedBlocks = Math.Max(0, inputBlocks - retainedBlocks);
        }

        var inputClaims = bundle.Canonical?.Claims.Count ?? 0;
        var inputEvidence = bundle.Canonical?.Evidence.Count ?? 0;
        var sources = view.SourceRefs?.Count ?? 0;
        var evidence = view.EvidenceRefs?.Count ?? 0;
        var claims = view.Claims?.Count ?? 0;
        var provenance = view.Provenance?.Count ?? 0;
        double? claimRetention = inputClaims <= 0 ? null : claims / (double)inputClaims;
        double? evidenceRetention = inputEvidence <= 0 ? null : evidence / (double)inputEvidence;

        var claimOk = ClaimEvidenceResolve(view);
        var evidenceOk = EvidenceSourceResolve(view);
        var provenanceOk = ProvenanceConsistent(view);
        var noDup = NoDuplicateIds(view);
        var integrityOk = claimOk && evidenceOk && provenanceOk && noDup;

        double? focusCoverage = null;
        int? focusMatching = null;
        double? focusRatio = null;
        bool? defAnchor = null;
        double? focusClaimHit = null;
        if (!string.IsNullOrWhiteSpace(request.FocusQuery))
        {
            var terms = FocusMatcher.Tokenize(request.FocusQuery);
            var surfaceLower = surface.ToLowerInvariant();
            var hits = terms.Count(t => t.Length >= 2 && surfaceLower.Contains(t, StringComparison.Ordinal));
            focusCoverage = terms.Count == 0 ? 0.0 : (double)hits / terms.Count;

            if (doc is { Blocks.Count: > 0 })
            {
                var matching = doc.Blocks.Count(b => FocusMatcher.Matches(b.Text ?? string.Empty, request.FocusQuery));
                focusMatching = matching;
                focusRatio = (double)matching / doc.Blocks.Count;
            }
            else
            {
                focusMatching = FocusMatcher.Matches(surface, request.FocusQuery) ? 1 : 0;
                focusRatio = focusMatching;
            }

            var anchor = bundle.Document.Blocks.FirstOrDefault(b =>
                string.Equals(b.Type, "heading", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(b.Text));
            if (anchor is not null && !string.IsNullOrWhiteSpace(anchor.Text))
            {
                defAnchor = surface.Contains(anchor.Text.Trim(), StringComparison.Ordinal)
                    || (doc?.Blocks.Any(b =>
                        string.Equals(b.Text, anchor.Text, StringComparison.Ordinal)) ?? false);
            }

            if (view.Claims is { Count: > 0 })
            {
                var claimHits = view.Claims.Count(c => FocusMatcher.Matches(c.Statement, request.FocusQuery));
                focusClaimHit = claimHits / (double)view.Claims.Count;
            }
        }

        var notes = BuildNotes(request, bundle, result, tokenReduction, claimRetention, evidenceRetention);

        return new PlannerBenchMetrics(
            SurfaceChars: surface.Length,
            SurfaceUtf8Bytes: Encoding.UTF8.GetByteCount(surface),
            SurfaceTokensEstimated: retainedTokens,
            SurfaceMediaType: view.Surface.MediaType ?? string.Empty,
            SurfaceEmpty: string.IsNullOrEmpty(surface),
            InputSurfaceTokensEstimated: inputSurfaceTokens,
            TokenReductionRatio: tokenReduction is double tr ? Math.Round(tr, 4) : null,
            RetainedBlocks: retainedBlocks,
            RetainedTables: retainedTables,
            RetainedHeadings: headings,
            RetainedListItems: lists,
            RetainedCodeBlocks: code,
            RetainedQuotes: quotes,
            OmittedBlocks: omittedBlocks,
            InputBlocks: inputBlocks,
            InputTables: inputTables,
            BlockRetentionRatio: blockRetention is double br ? Math.Round(br, 4) : null,
            SourceRefs: sources,
            EvidenceRefs: evidence,
            Claims: claims,
            ProvenanceItems: provenance,
            InputClaims: inputClaims,
            InputEvidence: inputEvidence,
            ClaimRetentionRatio: claimRetention is double cr ? Math.Round(cr, 4) : null,
            EvidenceRetentionRatio: evidenceRetention is double er ? Math.Round(er, 4) : null,
            ClaimEvidenceRefsResolve: claimOk,
            EvidenceSourceRefsResolve: evidenceOk,
            ProvenanceConsistent: provenanceOk,
            NoDuplicateIds: noDup,
            IntegrityOk: integrityOk,
            FocusTermCoverage: focusCoverage,
            FocusMatchingBlocks: focusMatching,
            FocusMatchingBlockRatio: focusRatio,
            DefinitionalAnchorPreserved: defAnchor,
            FocusClaimHitRatio: focusClaimHit is double fch ? Math.Round(fch, 4) : null,
            DeterministicOk: deterministicOk,
            StabilityOk: stabilityOk,
            PlannerInvocations: plannerInvocations,
            TokenEstimatorId: TokenEstimator.EstimatorId,
            Notes: notes);
    }

    private static IReadOnlyList<string> BuildNotes(
        MaterializationRequest request,
        ExtractedKnowledgeBundle bundle,
        MaterializationResult result,
        double? tokenReduction,
        double? claimRetention,
        double? evidenceRetention)
    {
        var notes = new List<string>
        {
            $"token_estimator={TokenEstimator.EstimatorId} (heuristic, not exact tokenizer)",
            "timings=single-run Stopwatch (not a rigorous microbenchmark)",
            "affects_planning: MaxTokens, FocusQuery (with FitMarkdown), FitMarkdown, ContentSelectors, ProvenancePolicy",
        };

        if (tokenReduction is double tr)
        {
            notes.Add($"token_reduction_vs_input≈{tr:0.00} (1 − retained/input surface tokens)");
        }

        if (CanonicalRetention.IsEvidencePreserving(request.ProvenancePolicy))
        {
            notes.Add("ProvenancePolicy=evidence-preserving — full Canonical claim→evidence→source→provenance retained");
        }
        else
        {
            notes.Add("ProvenancePolicy=default — Canonical claims budgeted (MaxTokens/4) and focus-ranked when FocusQuery set");
        }

        if (claimRetention is double cr)
        {
            notes.Add($"claim_retention={cr:0.00}");
        }

        if (evidenceRetention is double er)
        {
            notes.Add($"evidence_retention={er:0.00}");
        }

        if (!string.IsNullOrWhiteSpace(request.CapabilityProfile)
            && !string.Equals(request.CapabilityProfile, "default", StringComparison.OrdinalIgnoreCase))
        {
            notes.Add("CapabilityProfile currently unused by MaterializationPlanner.Plan");
        }

        if (!string.IsNullOrWhiteSpace(request.DisclosurePolicy)
            && !string.Equals(request.DisclosurePolicy, "default", StringComparison.OrdinalIgnoreCase))
        {
            notes.Add("DisclosurePolicy currently unused by MaterializationPlanner.Plan");
        }

        if (request.ExposePublicBlocks || request.ExposePublicTables)
        {
            notes.Add("ExposePublicBlocks/Tables affect MCP projection, not planner retention");
        }

        if (bundle.Canonical is null)
        {
            notes.Add("bundle has no Canonical sidecars");
        }
        else
        {
            notes.Add($"canonical_input: claims={bundle.Canonical.Claims.Count} evidence={bundle.Canonical.Evidence.Count}");
        }

        if (result.Truncated)
        {
            notes.Add($"surface_truncated strategy={result.TruncationStrategy ?? "?"}");
        }

        if (!result.SelectorsMatched && request.ContentSelectors is { Count: > 0 })
        {
            notes.Add("content_selectors_miss (SelectorsMatched=false)");
        }

        return notes;
    }

    private static bool ViewsEqual(MaterializedKnowledgeView a, MaterializedKnowledgeView b)
    {
        if (a.Surface.MediaType != b.Surface.MediaType || a.Surface.Text != b.Surface.Text)
        {
            return false;
        }

        if (!DocumentsEqual(a.Knowledge, b.Knowledge))
        {
            return false;
        }

        return IdSeq(a.SourceRefs, s => s.Id.Value).SequenceEqual(IdSeq(b.SourceRefs, s => s.Id.Value))
            && IdSeq(a.EvidenceRefs, e => e.Id.Value).SequenceEqual(IdSeq(b.EvidenceRefs, e => e.Id.Value))
            && IdSeq(a.Claims, c => c.Id.Value).SequenceEqual(IdSeq(b.Claims, c => c.Id.Value))
            && IdSeq(a.Provenance, p => p.Id.Value).SequenceEqual(IdSeq(b.Provenance, p => p.Id.Value));
    }

    private static bool DocumentsEqual(KnowledgeDocument? a, KnowledgeDocument? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        if (a.Blocks.Count != b.Blocks.Count || a.Tables.Count != b.Tables.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Blocks.Count; i++)
        {
            if (a.Blocks[i].Type != b.Blocks[i].Type || a.Blocks[i].Text != b.Blocks[i].Text)
            {
                return false;
            }
        }

        for (var i = 0; i < a.Tables.Count; i++)
        {
            if (a.Tables[i].Caption != b.Tables[i].Caption
                || a.Tables[i].Headers.Count != b.Tables[i].Headers.Count
                || a.Tables[i].Rows.Count != b.Tables[i].Rows.Count)
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<string> IdSeq<T>(IReadOnlyList<T>? list, Func<T, string> id) =>
        list is null ? Array.Empty<string>() : list.Select(id);

    private static bool ClaimEvidenceResolve(MaterializedKnowledgeView view)
    {
        if (view.Claims is null or { Count: 0 })
        {
            return true;
        }

        var evidenceIds = new HashSet<string>(
            (view.EvidenceRefs ?? Array.Empty<Canonical.Evidence>()).Select(e => e.Id.Value),
            StringComparer.Ordinal);
        foreach (var claim in view.Claims)
        {
            foreach (var er in claim.EvidenceRefs)
            {
                if (!evidenceIds.Contains(er.Value))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool EvidenceSourceResolve(MaterializedKnowledgeView view)
    {
        if (view.EvidenceRefs is null or { Count: 0 })
        {
            return true;
        }

        var sourceIds = new HashSet<string>(
            (view.SourceRefs ?? Array.Empty<Canonical.Source>()).Select(s => s.Id.Value),
            StringComparer.Ordinal);
        foreach (var e in view.EvidenceRefs)
        {
            if (!sourceIds.Contains(e.SourceId.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ProvenanceConsistent(MaterializedKnowledgeView view)
    {
        if (view.Provenance is null or { Count: 0 })
        {
            return true;
        }

        var sourceIds = new HashSet<string>(
            (view.SourceRefs ?? Array.Empty<Canonical.Source>()).Select(s => s.Id.Value),
            StringComparer.Ordinal);
        var evidenceIds = new HashSet<string>(
            (view.EvidenceRefs ?? Array.Empty<Canonical.Evidence>()).Select(e => e.Id.Value),
            StringComparer.Ordinal);
        foreach (var p in view.Provenance)
        {
            if (!sourceIds.Contains(p.SourceId.Value))
            {
                return false;
            }

            foreach (var eid in p.EvidenceIds)
            {
                if (!evidenceIds.Contains(eid.Value))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool NoDuplicateIds(MaterializedKnowledgeView view)
    {
        return Unique(view.SourceRefs?.Select(s => s.Id.Value))
            && Unique(view.EvidenceRefs?.Select(e => e.Id.Value))
            && Unique(view.Claims?.Select(c => c.Id.Value))
            && Unique(view.Provenance?.Select(p => p.Id.Value));

        static bool Unique(IEnumerable<string>? ids)
        {
            if (ids is null)
            {
                return true;
            }

            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                if (!set.Add(id))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>Render a human-readable report (stderr / docs). Token counts labelled as estimates.</summary>
    public static string FormatReport(
        string fixtureId,
        IReadOnlyList<PlannerCodecBenchResult> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Planner × Codec bench — fixture `{fixtureId}`");
        sb.AppendLine();
        sb.AppendLine($"Token estimator: `{TokenEstimator.EstimatorId}` (heuristic). Timings: single-run local Stopwatch.");
        sb.AppendLine("Questions answered: token reduction · evidence retention · focus · determinism/stability · codec interaction.");
        sb.AppendLine();
        sb.AppendLine("## Planner policies");
        sb.AppendLine();
        sb.AppendLine("| Planner policy | surface≈ | Δinput | blocks | claims | claimΔ | evidence | focus | focusClaims | plan ms | det | stab | integ |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|---|---|");
        foreach (var row in rows)
        {
            var m = row.PlannerMetrics;
            var focus = m.FocusTermCoverage is double f ? f.ToString("0.00") : "—";
            var focusClaims = m.FocusClaimHitRatio is double fc ? fc.ToString("0.00") : "—";
            var dIn = m.TokenReductionRatio is double tr ? tr.ToString("0.00") : "—";
            var cRet = m.ClaimRetentionRatio is double cr ? cr.ToString("0.00") : "—";
            sb.AppendLine(
                $"| {row.PlannerCase.CaseId} | {m.SurfaceTokensEstimated} | {dIn} | {m.RetainedBlocks} | {m.Claims} | {cRet} | {m.EvidenceRefs} | {focus} | {focusClaims} | {row.PlannerCase.Duration.TotalMilliseconds:F3} | {m.DeterministicOk} | {m.StabilityOk} | {m.IntegrityOk} |");
        }

        var plannerOnly = rows.Select(r => r.PlannerCase).ToList();
        var deltas = CompareToBaseline(plannerOnly);
        if (deltas.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Vs baseline `compat`");
            sb.AppendLine();
            sb.AppendLine("| Policy | relative token↓ | claim retention vs compat | evidence | integ |");
            sb.AppendLine("|---|---:|---:|---:|---|");
            foreach (var d in deltas)
            {
                var rt = d.RelativeTokenReduction is double x ? x.ToString("0.00") : "—";
                var rc = d.RelativeClaimRetention is double y ? y.ToString("0.00") : "—";
                sb.AppendLine($"| {d.CaseId} | {rt} | {rc} | {d.EvidenceRefs}/{d.BaselineEvidence} | {d.IntegrityOk} |");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Codec surfaces (same planned view → codecs)");
        sb.AppendLine();
        sb.AppendLine("| Planner policy | codec | chars | bytes | tokens≈ | vs passthrough | encode ms | deterministic |");
        sb.AppendLine("|---|---|---:|---:|---:|---:|---:|---|");
        foreach (var row in rows)
        {
            var passthrough = row.CodecResults.FirstOrDefault(c =>
                string.Equals(c.CodecId, MarkdownPassthroughCodec.Id, StringComparison.OrdinalIgnoreCase));
            foreach (var c in row.CodecResults)
            {
                var vsPt = passthrough is null || passthrough.Tokens <= 0
                    ? "—"
                    : (1.0 - c.Tokens / (double)passthrough.Tokens).ToString("0.00");
                sb.AppendLine(
                    $"| {row.PlannerCase.CaseId} | {c.CodecId} | {c.Chars} | {c.Utf8Bytes} | {c.Tokens} | {vsPt} | {c.EncodingDurationMs:F3} | {c.DeterministicOk} |");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Notes");
        foreach (var note in rows.SelectMany(r => r.PlannerMetrics.Notes).Distinct(StringComparer.Ordinal))
        {
            sb.AppendLine($"- {note}");
        }

        return sb.ToString();
    }
}
