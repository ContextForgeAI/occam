namespace OccamMcp.Core.Compile;

/// <summary>
/// Script-aware token estimate for budgets — AOT-safe, no SharpToken/ICU.
///
/// The old estimate was a flat <c>ceil(len / 4)</c>. That is only calibrated for ASCII/Latin
/// (~4 chars per BPE token); it undercounts every non-Latin script — a CJK character is roughly a
/// whole token, Cyrillic/Greek/Arabic ~2 chars per token — so budgets silently let through several
/// times the intended token count on non-English pages and receipts reported far too few tokens.
/// We weight each character by script instead: ASCII 0.25, CJK/Kana/Hangul 1.0, other non-ASCII 0.5.
/// Still a heuristic (hence <see cref="EstimatorId"/> for honest provenance), but no longer blind
/// to language.
/// </summary>
public static class TokenEstimator
{
    /// <summary>Legacy ASCII calibration point — kept for byte-count pre-sizing only.</summary>
    public const int ApproxCharsPerToken = 4;

    /// <summary>Names the estimator in telemetry/receipts so consumers know counts are heuristic.</summary>
    public const string EstimatorId = "heuristic-unicode-v1";

    // Per-character token cost. ASCII (incl. spaces/punct) ~4 chars/token; CJK ideographs, Kana and
    // Hangul are ~1 token each; every other non-ASCII letter (Cyrillic, Greek, Arabic, Hebrew, …)
    // sits around 2 chars/token. Surrogate halves count 0.5 each → ~1.0 per astral code point.
    private static double CharCost(char c)
    {
        if (c <= 0x7F)
        {
            return 0.25;
        }

        if ((c >= 0x4E00 && c <= 0x9FFF)    // CJK Unified Ideographs
            || (c >= 0x3400 && c <= 0x4DBF) // CJK Extension A
            || (c >= 0x3040 && c <= 0x30FF) // Hiragana + Katakana
            || (c >= 0xAC00 && c <= 0xD7A3) // Hangul syllables
            || (c >= 0xF900 && c <= 0xFAFF)) // CJK Compatibility Ideographs
        {
            return 1.0;
        }

        return 0.5;
    }

    public static int Estimate(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        double cost = 0;
        foreach (var c in text)
        {
            cost += CharCost(c);
        }

        return Math.Max(1, (int)Math.Ceiling(cost));
    }

    /// <summary>Pre-extract HTML size estimate (bytes, script unknown) — conservative ASCII calibration.</summary>
    public static int EstimateFromByteCount(int byteCount) =>
        byteCount <= 0
            ? 0
            : Math.Max(1, (int)Math.Ceiling(byteCount / (double)ApproxCharsPerToken));

    /// <summary>Length of the longest prefix of <paramref name="text"/> whose estimate ≤ <paramref name="maxTokens"/>.</summary>
    public static int CharBudgetForTokens(string text, int maxTokens)
    {
        if (string.IsNullOrEmpty(text) || maxTokens <= 0)
        {
            return 0;
        }

        double cost = 0;
        for (var i = 0; i < text.Length; i++)
        {
            cost += CharCost(text[i]);
            if (cost > maxTokens)
            {
                return i;
            }
        }

        return text.Length;
    }

    /// <summary>Length of the longest suffix of <paramref name="text"/> whose estimate ≤ <paramref name="maxTokens"/>.</summary>
    public static int CharBudgetForTokensFromEnd(string text, int maxTokens)
    {
        if (string.IsNullOrEmpty(text) || maxTokens <= 0)
        {
            return 0;
        }

        double cost = 0;
        for (var i = text.Length - 1; i >= 0; i--)
        {
            cost += CharCost(text[i]);
            if (cost > maxTokens)
            {
                return text.Length - 1 - i;
            }
        }

        return text.Length;
    }
}
