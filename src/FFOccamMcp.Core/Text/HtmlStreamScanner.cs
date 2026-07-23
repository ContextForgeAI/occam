using System.Runtime.CompilerServices;

namespace OccamMcp.Core.Text;

/// <summary>Span-based HTML anchor scan for map/probe link extraction (Tier-A SIMD path).</summary>
public static class HtmlStreamScanner
{
    public ref struct AnchorEnumerator
    {
        private ReadOnlySpan<char> _window;

        public ReadOnlySpan<char> Href { get; private set; }

        public ReadOnlySpan<char> InnerText { get; private set; }

        internal AnchorEnumerator(ReadOnlySpan<char> html) => _window = html;

        public bool MoveNext()
        {
            while (!_window.IsEmpty)
            {
                var tagIdx = VectorizedHtmlScanner.IndexOfAnyTag(_window);
                if (tagIdx < 0)
                {
                    return false;
                }

                if (_window[tagIdx] == '>')
                {
                    _window = _window[(tagIdx + 1)..];
                    continue;
                }

                if (TryParseAnchor(_window, tagIdx, out var href, out var inner, out var consumed))
                {
                    Href = href;
                    InnerText = inner;
                    _window = _window[consumed..];
                    return true;
                }

                _window = _window[(tagIdx + 1)..];
            }

            return false;
        }
    }

    public static AnchorEnumerator EnumerateAnchors(ReadOnlySpan<char> html) => new(html);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseAnchor(
        ReadOnlySpan<char> window,
        int ltIdx,
        out ReadOnlySpan<char> href,
        out ReadOnlySpan<char> inner,
        out int consumed)
    {
        href = default;
        inner = default;
        consumed = 0;

        if (!IsAnchorOpen(window, ltIdx))
        {
            return false;
        }

        var openEnd = FindOpeningTagEnd(window, ltIdx + 1);
        if (openEnd < 0)
        {
            return false;
        }

        var tagInner = window.Slice(ltIdx + 1, openEnd - ltIdx - 1);
        if (!TryExtractHref(tagInner, out href))
        {
            return false;
        }

        if (!TryFindClosingAnchor(window, openEnd + 1, out var closeStart, out var closeEnd))
        {
            return false;
        }

        inner = window.Slice(openEnd + 1, closeStart - openEnd - 1);
        consumed = closeEnd - ltIdx;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAnchorOpen(ReadOnlySpan<char> window, int ltIdx)
    {
        if (window[ltIdx] != '<')
        {
            return false;
        }

        if (ltIdx + 2 > window.Length)
        {
            return false;
        }

        var name = window[ltIdx + 1];
        if (name is not ('a' or 'A'))
        {
            return false;
        }

        if (ltIdx + 2 < window.Length && IsWordChar(window[ltIdx + 2]))
        {
            return false;
        }

        return true;
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

    private static bool TryExtractHref(ReadOnlySpan<char> tagInner, out ReadOnlySpan<char> href)
    {
        href = default;
        for (var i = 0; i <= tagInner.Length - 4; i++)
        {
            if (!IsHrefNameAt(tagInner, i))
            {
                continue;
            }

            var cursor = i + 4;
            if (TryReadAttributeValue(tagInner, cursor, out href))
            {
                return true;
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsHrefNameAt(ReadOnlySpan<char> tagInner, int i)
    {
        if (i > 0 && IsWordChar(tagInner[i - 1]))
        {
            return false;
        }

        if (!tagInner.Slice(i).StartsWith("href", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var after = i + 4;
        return after >= tagInner.Length || !IsWordChar(tagInner[after]);
    }

    private static bool TryReadAttributeValue(ReadOnlySpan<char> tagInner, int start, out ReadOnlySpan<char> value)
    {
        value = default;
        var i = start + VectorizedHtmlScanner.SkipWhitespaceVectorized(tagInner[start..]);
        if (i >= tagInner.Length || tagInner[i] != '=')
        {
            return false;
        }

        i++;
        i += VectorizedHtmlScanner.SkipWhitespaceVectorized(tagInner[i..]);
        if (i >= tagInner.Length)
        {
            return false;
        }

        var quote = tagInner[i];
        if (quote == '"')
        {
            i++;
            var end = tagInner[i..].IndexOf('"');
            if (end < 0)
            {
                return false;
            }

            value = tagInner.Slice(i, end);
            return true;
        }

        if (quote == '\'')
        {
            i++;
            var end = tagInner[i..].IndexOf('\'');
            if (end < 0)
            {
                return false;
            }

            value = tagInner.Slice(i, end);
            return true;
        }

        var j = i;
        while (j < tagInner.Length && !IsUnquotedHrefTerminator(tagInner[j]))
        {
            j++;
        }

        value = tagInner.Slice(i, j - i);
        return true;
    }

    private static bool TryFindClosingAnchor(
        ReadOnlySpan<char> window,
        int searchFrom,
        out int innerEndExclusive,
        out int closeEndExclusive)
    {
        innerEndExclusive = 0;
        closeEndExclusive = 0;
        var cursor = searchFrom;

        while (cursor < window.Length)
        {
            var rel = VectorizedHtmlScanner.IndexOfAnyTag(window[cursor..]);
            if (rel < 0)
            {
                return false;
            }

            var idx = cursor + rel;
            if (window[idx] != '<')
            {
                cursor = idx + 1;
                continue;
            }

            if (TryMatchClosingAnchor(window, idx, out closeEndExclusive))
            {
                innerEndExclusive = idx;
                return true;
            }

            cursor = idx + 1;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryMatchClosingAnchor(ReadOnlySpan<char> window, int idx, out int closeEndExclusive)
    {
        closeEndExclusive = 0;
        if (idx + 4 > window.Length)
        {
            return false;
        }

        if (window[idx] != '<' || window[idx + 1] != '/')
        {
            return false;
        }

        if (window[idx + 2] is not ('a' or 'A'))
        {
            return false;
        }

        if (window[idx + 3] != '>')
        {
            return false;
        }

        closeEndExclusive = idx + 4;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsUnquotedHrefTerminator(char c) =>
        c is ' ' or '\t' or '\r' or '\n' or '>';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWordChar(char c) =>
        (c >= 'a' && c <= 'z')
        || (c >= 'A' && c <= 'Z')
        || (c >= '0' && c <= '9')
        || c == '_';
}
