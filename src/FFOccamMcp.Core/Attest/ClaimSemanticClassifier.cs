using System.Text.RegularExpressions;

namespace OccamMcp.Core.Attest;

/// <summary>
/// Layer 2 — semantic support classifier. Independent of BM25 retrieval and Merkle proofs.
/// Fail-closed: when the claim shape is unparsed or evidence is ambiguous → <see cref="AttestStatus.Unknown"/>.
/// Lexical co-occurrence alone never yields <see cref="AttestStatus.Supported"/>.
/// </summary>
public static partial class ClaimSemanticClassifier
{
    /// <summary>Incompatible type heads for IsA claims (closed set; extend carefully).</summary>
    private static readonly HashSet<string> SoftTypeHeads = new(StringComparer.Ordinal)
    {
        "library", "module", "framework", "package", "api", "toolkit", "runtime",
    };

    private static readonly HashSet<string> DataTypeHeads = new(StringComparer.Ordinal)
    {
        "database", "db", "sql", "engine", "rdbms", "orm", "datastore", "store",
    };

    public static string Classify(
        string claim,
        IReadOnlyList<string> retrievedBlocks,
        bool retrievalComplete = true)
    {
        if (retrievedBlocks.Count == 0)
        {
            // Complete miss on a complete extract → unsupported; incomplete → unknown (fail-closed).
            return retrievalComplete ? AttestStatus.Unsupported : AttestStatus.Unknown;
        }

        var parsed = TryParse(claim);
        if (parsed is null)
        {
            return AttestStatus.Unknown;
        }

        var sawRelated = false;
        var sawSupported = false;
        var sawContradicted = false;

        foreach (var block in retrievedBlocks)
        {
            if (string.IsNullOrWhiteSpace(block))
            {
                continue;
            }

            var verdict = ClassifyAgainstBlock(parsed, block);
            switch (verdict)
            {
                case AttestStatus.Supported:
                    sawSupported = true;
                    break;
                case AttestStatus.Contradicted:
                    sawContradicted = true;
                    break;
                case AttestStatus.Related:
                    sawRelated = true;
                    break;
            }
        }

        // Aggregation: contradicted beats supported; related is not support; else unsupported for
        // definitional/uses claims that retrieved only non-entailing text (not unknown — we did see text).
        if (sawContradicted)
        {
            return AttestStatus.Contradicted;
        }

        if (sawSupported)
        {
            return AttestStatus.Supported;
        }

        if (sawRelated)
        {
            // Definitional / uses claims that only co-occur with topic terms are unsupported, not supported.
            // "related" is reserved for when we cannot reduce further; for IsA/Uses false positives → unsupported.
            return parsed.Kind is ClaimKind.IsA or ClaimKind.Uses
                ? AttestStatus.Unsupported
                : AttestStatus.Related;
        }

        return AttestStatus.Unsupported;
    }

    private static string ClassifyAgainstBlock(ParsedClaim claim, string block)
    {
        var norm = Normalize(block);
        var subj = claim.SubjectNormalized;
        var obj = claim.ObjectNormalized;

        var subjectHit = ContainsPhrase(norm, subj);
        var objectHit = ContainsPhrase(norm, obj);

        if (!subjectHit && !objectHit)
        {
            return AttestStatus.Unknown;
        }

        // Topic overlap without the claim subject → related at best (never support).
        if (!subjectHit)
        {
            return objectHit ? AttestStatus.Related : AttestStatus.Unknown;
        }

        return claim.Kind switch
        {
            ClaimKind.IsA => ClassifyIsA(norm, subj, obj, claim.ObjectTokens),
            ClaimKind.Uses => ClassifyUses(norm, subj, obj),
            _ => AttestStatus.Unknown,
        };
    }

    private static string ClassifyIsA(
        string normBlock,
        string subject,
        string objectPhrase,
        IReadOnlyList<string> objectTokens)
    {
        // Explicit negation of the same is-a: "X is not a Y" / "X isn't a Y"
        if (HasNegatedCopula(normBlock, subject, objectPhrase, objectTokens))
        {
            return AttestStatus.Contradicted;
        }

        // Affirmed is-a: subject near copula near object (object predicated of subject).
        if (HasAffirmedCopula(normBlock, subject, objectPhrase, objectTokens))
        {
            return AttestStatus.Supported;
        }

        // Incompatible type asserted of subject (e.g. claim says database, block says library).
        if (HasIncompatibleTypeAssertion(normBlock, subject, objectTokens))
        {
            return AttestStatus.Contradicted;
        }

        // Object terms appear (e.g. "database connection libraries") but not as is-a of subject.
        if (objectTokens.Any(t => t.Length >= 3 && normBlock.Contains(t, StringComparison.Ordinal)))
        {
            return AttestStatus.Related;
        }

        // Subject present, object absent → related topic, not support.
        return AttestStatus.Related;
    }

    private static string ClassifyUses(string normBlock, string subject, string objectPhrase)
    {
        if (HasNegatedUses(normBlock, subject, objectPhrase))
        {
            return AttestStatus.Contradicted;
        }

        if (HasAffirmedUses(normBlock, subject, objectPhrase))
        {
            return AttestStatus.Supported;
        }

        if (ContainsPhrase(normBlock, objectPhrase) ||
            objectPhrase.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Any(t => t.Length >= 3 && normBlock.Contains(t, StringComparison.Ordinal)))
        {
            return AttestStatus.Related;
        }

        return AttestStatus.Related;
    }

    private static bool HasAffirmedCopula(
        string norm,
        string subject,
        string objectPhrase,
        IReadOnlyList<string> objectTokens)
    {
        // Prefer full object phrase in a "subject … is/are … object" window.
        foreach (Match m in CopulaRegex().Matches(norm))
        {
            var before = norm[..m.Index];
            var after = norm[(m.Index + m.Length)..];
            if (!before.Contains(subject, StringComparison.Ordinal))
            {
                // Subject may appear immediately before copula with little gap.
                var prefix = before.Length > 80 ? before[^80..] : before;
                if (!prefix.Contains(subject, StringComparison.Ordinal))
                {
                    continue;
                }
            }

            // Object must appear in the post-copula window (predicated), not only elsewhere.
            var window = after.Length > 120 ? after[..120] : after;
            if (ContainsPhrase(window, objectPhrase) || TokensCovered(window, objectTokens, minFraction: 0.6))
            {
                // Reject if this copula is negated.
                var negProbe = before.Length > 12 ? before[^12..] : before;
                if (negProbe.Contains(" not", StringComparison.Ordinal) ||
                    negProbe.EndsWith("not", StringComparison.Ordinal) ||
                    negProbe.Contains("nt ", StringComparison.Ordinal)) // isn't / aren't (normalized)
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    private static bool HasNegatedCopula(
        string norm,
        string subject,
        string objectPhrase,
        IReadOnlyList<string> objectTokens)
    {
        foreach (Match m in NegatedCopulaRegex().Matches(norm))
        {
            var before = norm[..m.Index];
            var after = norm[(m.Index + m.Length)..];
            var prefix = before.Length > 80 ? before[^80..] : before;
            if (!prefix.Contains(subject, StringComparison.Ordinal))
            {
                continue;
            }

            var window = after.Length > 120 ? after[..120] : after;
            if (ContainsPhrase(window, objectPhrase) || TokensCovered(window, objectTokens, minFraction: 0.6))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasIncompatibleTypeAssertion(
        string norm,
        string subject,
        IReadOnlyList<string> claimObjectTokens)
    {
        var claimIsData = claimObjectTokens.Any(DataTypeHeads.Contains);
        var claimIsSoft = claimObjectTokens.Any(SoftTypeHeads.Contains);
        if (!claimIsData && !claimIsSoft)
        {
            return false;
        }

        foreach (Match m in CopulaRegex().Matches(norm))
        {
            var before = norm[..m.Index];
            var prefix = before.Length > 80 ? before[^80..] : before;
            if (!prefix.Contains(subject, StringComparison.Ordinal))
            {
                continue;
            }

            var window = norm[(m.Index + m.Length)..];
            window = window.Length > 80 ? window[..80] : window;
            var heads = Tokenize(window);
            var assertsData = heads.Any(DataTypeHeads.Contains);
            var assertsSoft = heads.Any(SoftTypeHeads.Contains);

            if (claimIsData && assertsSoft && !assertsData)
            {
                return true;
            }

            if (claimIsSoft && assertsData && !assertsSoft)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAffirmedUses(string norm, string subject, string objectPhrase)
    {
        foreach (Match m in UsesRegex().Matches(norm))
        {
            var before = norm[..m.Index];
            var after = norm[(m.Index + m.Length)..];
            var prefix = before.Length > 80 ? before[^80..] : before;
            if (!prefix.Contains(subject, StringComparison.Ordinal))
            {
                continue;
            }

            var window = after.Length > 100 ? after[..100] : after;
            if (ContainsPhrase(window, objectPhrase) ||
                objectPhrase.Replace("/", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Where(t => t.Length >= 3)
                    .All(t => window.Contains(t, StringComparison.Ordinal)))
            {
                var negProbe = before.Length > 12 ? before[^12..] : before;
                if (negProbe.Contains(" not", StringComparison.Ordinal) ||
                    negProbe.EndsWith("not", StringComparison.Ordinal) ||
                    negProbe.Contains("never", StringComparison.Ordinal))
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    private static bool HasNegatedUses(string norm, string subject, string objectPhrase)
    {
        // "X does not use Y" / "X never uses Y"
        foreach (Match m in NegatedUsesRegex().Matches(norm))
        {
            var before = norm[..m.Index];
            var after = norm[(m.Index + m.Length)..];
            var prefix = before.Length > 80 ? before[^80..] : before;
            if (!prefix.Contains(subject, StringComparison.Ordinal))
            {
                continue;
            }

            var window = after.Length > 100 ? after[..100] : after;
            if (ContainsPhrase(window, objectPhrase))
            {
                return true;
            }
        }

        return false;
    }

    internal static ParsedClaim? TryParse(string claim)
    {
        var trimmed = (claim ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        var norm = Normalize(trimmed);

        var isA = IsAClaimRegex().Match(norm);
        if (isA.Success)
        {
            var subject = isA.Groups["subj"].Value.Trim();
            var obj = isA.Groups["obj"].Value.Trim();
            if (subject.Length == 0 || obj.Length == 0)
            {
                return null;
            }

            return new ParsedClaim(ClaimKind.IsA, subject, obj, Tokenize(obj));
        }

        var uses = UsesClaimRegex().Match(norm);
        if (uses.Success)
        {
            var subject = uses.Groups["subj"].Value.Trim();
            var obj = uses.Groups["obj"].Value.Trim();
            if (subject.Length == 0 || obj.Length == 0)
            {
                return null;
            }

            return new ParsedClaim(ClaimKind.Uses, subject, obj, Tokenize(obj));
        }

        return null;
    }

    private static string Normalize(string text)
    {
        var lower = text.ToLowerInvariant();
        // Collapse punctuation that blocks phrase match; keep / for async/await as space-equivalent later.
        lower = PunctRegex().Replace(lower, " ");
        lower = SpaceRegex().Replace(lower, " ").Trim();
        // Normalize common contractions used in negation probes.
        lower = lower.Replace("isn't", "is not", StringComparison.Ordinal)
            .Replace("aren't", "are not", StringComparison.Ordinal)
            .Replace("doesn't", "does not", StringComparison.Ordinal);
        return lower;
    }

    private static bool ContainsPhrase(string haystack, string phrase)
    {
        if (phrase.Length == 0)
        {
            return false;
        }

        if (haystack.Contains(phrase, StringComparison.Ordinal))
        {
            return true;
        }

        // async/await ↔ async await after punctuation collapse
        var alt = phrase.Replace('/', ' ');
        return alt != phrase && haystack.Contains(alt, StringComparison.Ordinal);
    }

    private static bool TokensCovered(string window, IReadOnlyList<string> tokens, double minFraction)
    {
        var content = tokens.Where(t => t.Length >= 3).ToList();
        if (content.Count == 0)
        {
            return false;
        }

        var hit = content.Count(t => window.Contains(t, StringComparison.Ordinal));
        return hit >= Math.Max(1, (int)Math.Ceiling(minFraction * content.Count));
    }

    private static List<string> Tokenize(string text) =>
        WordRegex().Matches(text.ToLowerInvariant())
            .Select(m => m.Value)
            .Where(w => w.Length >= 2)
            .ToList();

    internal enum ClaimKind
    {
        IsA,
        Uses,
    }

    internal sealed record ParsedClaim(
        ClaimKind Kind,
        string SubjectNormalized,
        string ObjectNormalized,
        IReadOnlyList<string> ObjectTokens);

    // Article optional so both "asyncio is a library" and "asyncio is library" parse.
    [GeneratedRegex(@"^(?<subj>.+?)\s+is\s+(?:(?:a|an|the)\s+)?(?<obj>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex IsAClaimRegex();

    [GeneratedRegex(@"^(?<subj>.+?)\s+(?:uses|using|utilizes|utilise|utilising)\s+(?<obj>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex UsesClaimRegex();

    [GeneratedRegex(@"\b(?:is|are|was|were)\b", RegexOptions.CultureInvariant)]
    private static partial Regex CopulaRegex();

    [GeneratedRegex(@"\b(?:is|are|was|were)\s+not\b", RegexOptions.CultureInvariant)]
    private static partial Regex NegatedCopulaRegex();

    [GeneratedRegex(@"\b(?:uses|using|utilizes|utilise|utilising)\b", RegexOptions.CultureInvariant)]
    private static partial Regex UsesRegex();

    [GeneratedRegex(@"\b(?:does\s+not\s+use|do\s+not\s+use|never\s+uses|never\s+use)\b", RegexOptions.CultureInvariant)]
    private static partial Regex NegatedUsesRegex();

    [GeneratedRegex(@"[a-z0-9]+(?:/[a-z0-9]+)?", RegexOptions.CultureInvariant)]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"[^a-z0-9/]+", RegexOptions.CultureInvariant)]
    private static partial Regex PunctRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex SpaceRegex();
}
