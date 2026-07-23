namespace OccamMcp.Core.Routing;

/// <summary>Substring helpers with word-boundary checks for post-extraction heuristics.</summary>
public static class TextNeedle
{
    /// <summary>True when <paramref name="needle"/> appears as a whole word (not inside identifiers like <c>ssl_password_file</c>).</summary>
    public static bool ContainsWord(string lowerText, string needle)
    {
        if (string.IsNullOrEmpty(lowerText) || string.IsNullOrEmpty(needle))
        {
            return false;
        }

        var start = 0;
        while (start <= lowerText.Length - needle.Length)
        {
            var idx = lowerText.IndexOf(needle, start, StringComparison.Ordinal);
            if (idx < 0)
            {
                return false;
            }

            if (IsWordBoundaryBefore(lowerText, idx) && IsWordBoundaryAfter(lowerText, idx + needle.Length))
            {
                return true;
            }

            start = idx + 1;
        }

        return false;
    }

    public static bool ContainsAnyPhrase(string lowerText, params string[] phrases)
    {
        foreach (var phrase in phrases)
        {
            if (lowerText.Contains(phrase, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWordBoundaryBefore(string text, int index) =>
        index == 0 || !IsWordChar(text[index - 1]);

    private static bool IsWordBoundaryAfter(string text, int index) =>
        index >= text.Length || !IsWordChar(text[index]);

    private static bool IsWordChar(char c) =>
        char.IsLetterOrDigit(c) || c is '_' or '-';
}
