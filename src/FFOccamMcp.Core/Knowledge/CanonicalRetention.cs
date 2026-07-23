using OccamMcp.Core.Compile;
using OccamMcp.Core.Knowledge.Canonical;
using OccamMcp.Core.Knowledge.Legacy;

namespace OccamMcp.Core.Knowledge;

/// <summary>
/// Canonical-driven retention (Occam 1.1 R2). Planner-owned: codecs never call this.
/// Policies:
/// <list type="bullet">
/// <item><c>default</c> — budget-aware claim retention (share of <c>MaxTokens</c>); focus-ranked when
/// <c>FocusQuery</c> is set; orphan evidence/provenance trimmed to the kept closure.</item>
/// <item><c>evidence-preserving</c> — keep the full claim→evidence→source→provenance graph.</item>
/// </list>
/// Unknown policy tokens fall back to <c>default</c> (live-safe). Null/empty <c>MaxTokens</c> under
/// <c>default</c> retains everything (compat with uncapped live path).
/// </summary>
internal static class CanonicalRetention
{
    public const string PolicyDefault = "default";
    public const string PolicyEvidencePreserving = "evidence-preserving";

    /// <summary>
    /// Fraction of the surface MaxTokens reserved for claim-statement retention under default policy.
    /// </summary>
    private const int DefaultCanonicalBudgetDivisor = 4;

    private const int MinCanonicalBudgetTokens = 8;

    public static CanonicalExtract? Retain(
        CanonicalExtract? canonical,
        string? provenancePolicy,
        int? maxTokens,
        string? focusQuery)
    {
        if (canonical is null)
        {
            return null;
        }

        if (IsEvidencePreserving(provenancePolicy) || maxTokens is null)
        {
            return canonical;
        }

        var cap = Math.Max(MinCanonicalBudgetTokens, maxTokens.Value / DefaultCanonicalBudgetDivisor);
        return RetainWithinClaimBudget(canonical, cap, focusQuery);
    }

    public static bool IsEvidencePreserving(string? provenancePolicy) =>
        string.Equals(
            provenancePolicy?.Trim(),
            PolicyEvidencePreserving,
            StringComparison.OrdinalIgnoreCase);

    private static CanonicalExtract RetainWithinClaimBudget(
        CanonicalExtract canonical,
        int claimTokenCap,
        string? focusQuery)
    {
        var ranked = canonical.Claims
            .Select((c, i) => (Claim: c, Index: i, Cost: Math.Max(1, TokenEstimator.Estimate(c.Statement)), Score: FocusScore(c, focusQuery)))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Index)
            .ToList();

        var keptClaims = new List<ClaimCandidate>();
        var used = 0;
        foreach (var x in ranked)
        {
            if (used + x.Cost > claimTokenCap)
            {
                // Focus/salience path: skip oversized and try a cheaper later claim.
                if (x.Score > 0 || !string.IsNullOrWhiteSpace(focusQuery))
                {
                    continue;
                }

                break;
            }

            used += x.Cost;
            keptClaims.Add(x.Claim);
        }

        // Restore input order for determinism of the materialized view.
        var claimOrder = canonical.Claims
            .Select((c, i) => (c.Id.Value, i))
            .ToDictionary(x => x.Value, x => x.i, StringComparer.Ordinal);
        keptClaims.Sort((a, b) => claimOrder[a.Id.Value].CompareTo(claimOrder[b.Id.Value]));

        var keptEvidenceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var claim in keptClaims)
        {
            foreach (var eid in claim.EvidenceRefs)
            {
                keptEvidenceIds.Add(eid.Value);
            }
        }

        // If every claim was dropped but evidence exists, keep nothing Canonical except Source
        // when we still want a locator — drop claims/evidence/provenance for honesty under budget.
        var keptEvidence = canonical.Evidence
            .Where(e => keptEvidenceIds.Contains(e.Id.Value))
            .ToList();

        // Always retain the primary source (acquisition identity) even if all claims dropped.
        var keptProvenance = new List<KnowledgeProvenance>();
        foreach (var p in canonical.Provenance)
        {
            var remaining = p.EvidenceIds
                .Where(id => keptEvidenceIds.Contains(id.Value))
                .ToList();
            if (remaining.Count == 0)
            {
                continue;
            }

            if (remaining.Count == p.EvidenceIds.Count)
            {
                keptProvenance.Add(p);
            }
            else
            {
                keptProvenance.Add(KnowledgeProvenance.Create(
                    p.Id,
                    p.SourceId,
                    remaining,
                    p.ObservedAt,
                    p.ExtractionMethod,
                    p.ExtractionVersion,
                    p.ValidationHint,
                    p.ReceiptContentHash,
                    p.BlockLeafHash));
            }
        }

        return new CanonicalExtract(
            canonical.Source,
            keptEvidence,
            keptClaims,
            keptProvenance);
    }

    private static double FocusScore(ClaimCandidate claim, string? focusQuery)
    {
        if (string.IsNullOrWhiteSpace(focusQuery))
        {
            return 0;
        }

        if (FocusMatcher.Matches(claim.Statement, focusQuery))
        {
            return 1.0;
        }

        // Soft overlap: count focus terms present in the statement.
        var terms = focusQuery.Split([' ', '\t', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (terms.Length == 0)
        {
            return 0;
        }

        var lower = claim.Statement.ToLowerInvariant();
        var hits = terms.Count(t => t.Length >= 3 && lower.Contains(t, StringComparison.OrdinalIgnoreCase));
        return hits / (double)terms.Length;
    }
}
