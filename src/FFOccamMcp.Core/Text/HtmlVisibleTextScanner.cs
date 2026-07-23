using System.Runtime.CompilerServices;

namespace OccamMcp.Core.Text;

/// <summary>Span-based visible text length estimator for probe classifier (Tier-A path).</summary>
public static class HtmlVisibleTextScanner
{
    public static int CountVisibleText(string html) =>
        CountVisibleText(html.AsSpan());

    public static int CountVisibleText(ReadOnlySpan<char> html)
    {
        if (html.IsEmpty)
        {
            return 0;
        }

        var count = 0;
        var started = false;
        var lastWasSpace = false;
        var i = 0;

        while (i < html.Length)
        {
            var rel = VectorizedHtmlScanner.IndexOfAnyTag(html[i..]);
            if (rel < 0)
            {
                AppendText(html[i..], ref count, ref started, ref lastWasSpace);
                break;
            }

            var idx = i + rel;
            if (html[idx] == '>')
            {
                AppendText(html.Slice(i, idx - i + 1), ref count, ref started, ref lastWasSpace);
                i = idx + 1;
                continue;
            }

            AppendText(html.Slice(i, idx - i), ref count, ref started, ref lastWasSpace);

            if (TrySkipBlock(html, idx, "script", out var blockConsumed)
                || TrySkipBlock(html, idx, "style", out blockConsumed))
            {
                EmitTagSpace(ref count, ref started, ref lastWasSpace);
                i = idx + blockConsumed;
                continue;
            }

            var tagEnd = FindOpeningTagEnd(html, idx + 1);
            if (tagEnd < 0)
            {
                AppendChar(html[idx], ref count, ref started, ref lastWasSpace);
                i = idx + 1;
                continue;
            }

            EmitTagSpace(ref count, ref started, ref lastWasSpace);
            i = tagEnd + 1;
        }

        if (lastWasSpace && count > 0)
        {
            count--;
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EmitTagSpace(ref int count, ref bool started, ref bool lastWasSpace)
    {
        if (started && !lastWasSpace)
        {
            count++;
            lastWasSpace = true;
        }
    }

    private static void AppendText(ReadOnlySpan<char> text, ref int count, ref bool started, ref bool lastWasSpace)
    {
        foreach (var c in text)
        {
            AppendChar(c, ref count, ref started, ref lastWasSpace);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendChar(char c, ref int count, ref bool started, ref bool lastWasSpace)
    {
        if (IsWhitespace(c))
        {
            if (started && !lastWasSpace)
            {
                count++;
                lastWasSpace = true;
            }
        }
        else
        {
            started = true;
            lastWasSpace = false;
            count++;
        }
    }

    private static bool TrySkipBlock(
        ReadOnlySpan<char> html,
        int ltIdx,
        ReadOnlySpan<char> tagName,
        out int consumed)
    {
        consumed = 0;
        if (!IsTagOpen(html, ltIdx, tagName))
        {
            return false;
        }

        var openEnd = FindOpeningTagEnd(html, ltIdx + 1);
        if (openEnd < 0)
        {
            return false;
        }

        var searchFrom = openEnd + 1;
        var closeStart = FindTagClose(html[searchFrom..], tagName);
        if (closeStart < 0)
        {
            return false;
        }

        var closeLt = searchFrom + closeStart;
        var closeEnd = FindOpeningTagEnd(html, closeLt + 1);
        if (closeEnd < 0)
        {
            return false;
        }

        consumed = closeEnd + 1 - ltIdx;
        return true;
    }

    private static int FindTagClose(ReadOnlySpan<char> window, ReadOnlySpan<char> tagName)
    {
        var cursor = 0;
        while (cursor < window.Length)
        {
            var rel = VectorizedHtmlScanner.IndexOfAnyTag(window[cursor..]);
            if (rel < 0)
            {
                return -1;
            }

            var idx = cursor + rel;
            if (window[idx] != '<' || idx + 1 >= window.Length || window[idx + 1] != '/')
            {
                cursor = idx + 1;
                continue;
            }

            var nameStart = idx + 2;
            if (!window.Slice(nameStart).StartsWith(tagName, StringComparison.OrdinalIgnoreCase))
            {
                cursor = idx + 1;
                continue;
            }

            var after = nameStart + tagName.Length;
            if (after < window.Length && IsWordChar(window[after]))
            {
                cursor = idx + 1;
                continue;
            }

            return idx;
        }

        return -1;
    }

    private static int FindOpeningTagEnd(ReadOnlySpan<char> window, int startFrom)
    {
        var quote = '\0';
        for (var i = startFrom; i < window.Length; i++)
        {
            var c = window[i];
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
                continue;
            }

            if (c == '>')
            {
                return i;
            }
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsTagOpen(ReadOnlySpan<char> window, int ltIdx, ReadOnlySpan<char> tagName)
    {
        if (window[ltIdx] != '<')
        {
            return false;
        }

        var nameStart = ltIdx + 1;
        if (nameStart >= window.Length || window[nameStart] == '/')
        {
            return false;
        }

        if (!window.Slice(nameStart).StartsWith(tagName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var after = nameStart + tagName.Length;
        return after >= window.Length || !IsWordChar(window[after]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWhitespace(char c) =>
        c is ' ' or '\t' or '\r' or '\n';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWordChar(char c) =>
        (c >= 'a' && c <= 'z')
        || (c >= 'A' && c <= 'Z')
        || (c >= '0' && c <= '9')
        || c == '_';
}
