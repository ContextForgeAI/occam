using System.Runtime.CompilerServices;

namespace OccamMcp.Core.Text;

/// <summary>Span-based scan of document head for probe social meta (Tier-A SIMD path).</summary>
public static class HtmlHeadScanner
{
    public ref struct HeadMetaFields
    {
        public ReadOnlySpan<char> Lang;
        public ReadOnlySpan<char> OgTitle;
        public ReadOnlySpan<char> NameTitle;
        public ReadOnlySpan<char> OgDescription;
        public ReadOnlySpan<char> NameDescription;
        public ReadOnlySpan<char> OgImage;
        public ReadOnlySpan<char> SiteName;
        public ReadOnlySpan<char> TwitterCardProperty;
        public ReadOnlySpan<char> TwitterCardName;
    }

    public static HeadMetaFields Scan(ReadOnlySpan<char> html)
    {
        var fields = new HeadMetaFields();
        if (html.IsEmpty)
        {
            return fields;
        }

        fields.Lang = TryExtractHtmlLang(html);
        var head = ResolveHeadWindow(html);
        ScanHeadTags(head, ref fields);
        return fields;
    }

    private static ReadOnlySpan<char> ResolveHeadWindow(ReadOnlySpan<char> html)
    {
        var headOpen = FindTagOpen(html, "head");
        ReadOnlySpan<char> window;
        if (headOpen >= 0)
        {
            var openEnd = FindOpeningTagEnd(html, headOpen + 1);
            if (openEnd < 0)
            {
                return default;
            }

            window = html[(openEnd + 1)..];
        }
        else
        {
            window = html;
        }

        var headClose = FindTagClose(window, "head");
        if (headClose >= 0)
        {
            window = window[..headClose];
        }
        else
        {
            var bodyOpen = FindTagOpen(window, "body");
            if (bodyOpen >= 0)
            {
                window = window[..bodyOpen];
            }
        }

        return window;
    }

    private static void ScanHeadTags(ReadOnlySpan<char> head, ref HeadMetaFields fields)
    {
        var window = head;
        while (!window.IsEmpty)
        {
            var tagIdx = VectorizedHtmlScanner.IndexOfAnyTag(window);
            if (tagIdx < 0)
            {
                return;
            }

            if (window[tagIdx] == '>')
            {
                window = window[(tagIdx + 1)..];
                continue;
            }

            if (IsMetaOpen(window, tagIdx))
            {
                var openEnd = FindOpeningTagEnd(window, tagIdx + 1);
                if (openEnd >= 0)
                {
                    var tagInner = window.Slice(tagIdx + 1, openEnd - tagIdx - 1);
                    ApplyMetaTag(tagInner, ref fields);
                    window = window[(openEnd + 1)..];
                    continue;
                }
            }

            window = window[(tagIdx + 1)..];
        }
    }

    private static void ApplyMetaTag(ReadOnlySpan<char> tagInner, ref HeadMetaFields fields)
    {
        if (!TryReadMetaAttributes(tagInner, out var property, out var name, out var content))
        {
            return;
        }

        if (property.IsEmpty && name.IsEmpty)
        {
            return;
        }

        if (!content.IsEmpty)
        {
            if (property.Equals("og:title".AsSpan(), StringComparison.OrdinalIgnoreCase) && fields.OgTitle.IsEmpty)
            {
                fields.OgTitle = content;
            }
            else if (property.Equals("og:description".AsSpan(), StringComparison.OrdinalIgnoreCase) && fields.OgDescription.IsEmpty)
            {
                fields.OgDescription = content;
            }
            else if (property.Equals("og:image".AsSpan(), StringComparison.OrdinalIgnoreCase) && fields.OgImage.IsEmpty)
            {
                fields.OgImage = content;
            }
            else if (property.Equals("og:site_name".AsSpan(), StringComparison.OrdinalIgnoreCase) && fields.SiteName.IsEmpty)
            {
                fields.SiteName = content;
            }
            else if (property.Equals("twitter:card".AsSpan(), StringComparison.OrdinalIgnoreCase) && fields.TwitterCardProperty.IsEmpty)
            {
                fields.TwitterCardProperty = content;
            }

            if (name.Equals("title".AsSpan(), StringComparison.OrdinalIgnoreCase) && fields.NameTitle.IsEmpty)
            {
                fields.NameTitle = content;
            }
            else if (name.Equals("description".AsSpan(), StringComparison.OrdinalIgnoreCase) && fields.NameDescription.IsEmpty)
            {
                fields.NameDescription = content;
            }
            else if (name.Equals("twitter:card".AsSpan(), StringComparison.OrdinalIgnoreCase) && fields.TwitterCardName.IsEmpty)
            {
                fields.TwitterCardName = content;
            }
        }
    }

    private static bool TryReadMetaAttributes(
        ReadOnlySpan<char> tagInner,
        out ReadOnlySpan<char> property,
        out ReadOnlySpan<char> name,
        out ReadOnlySpan<char> content)
    {
        property = default;
        name = default;
        content = default;

        if (!TrySkipTagName(tagInner, "meta", out var cursor))
        {
            return false;
        }

        while (cursor < tagInner.Length)
        {
            cursor += VectorizedHtmlScanner.SkipWhitespaceVectorized(tagInner[cursor..]);
            if (cursor >= tagInner.Length || tagInner[cursor] is '/' or '>')
            {
                break;
            }

            if (!TryReadAttributeName(tagInner, cursor, out var attrName, out var nameEnd))
            {
                break;
            }

            cursor = nameEnd;
            cursor += VectorizedHtmlScanner.SkipWhitespaceVectorized(tagInner[cursor..]);
            if (cursor >= tagInner.Length || tagInner[cursor] != '=')
            {
                continue;
            }

            cursor++;
            if (!TryReadAttributeValue(tagInner, cursor, out var value, out var valueEnd))
            {
                continue;
            }

            cursor = valueEnd;
            if (attrName.Equals("property".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                property = value;
            }
            else if (attrName.Equals("name".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                name = value;
            }
            else if (attrName.Equals("content".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                content = value;
            }
        }

        return true;
    }

    private static ReadOnlySpan<char> TryExtractHtmlLang(ReadOnlySpan<char> html)
    {
        var htmlOpen = FindTagOpen(html, "html");
        if (htmlOpen < 0)
        {
            return default;
        }

        var openEnd = FindOpeningTagEnd(html, htmlOpen + 1);
        if (openEnd < 0)
        {
            return default;
        }

        var tagInner = html.Slice(htmlOpen + 1, openEnd - htmlOpen - 1);
        if (!TrySkipTagName(tagInner, "html", out var cursor))
        {
            return default;
        }

        while (cursor < tagInner.Length)
        {
            cursor += VectorizedHtmlScanner.SkipWhitespaceVectorized(tagInner[cursor..]);
            if (cursor >= tagInner.Length || tagInner[cursor] == '>')
            {
                break;
            }

            if (!TryReadAttributeName(tagInner, cursor, out var attrName, out var nameEnd))
            {
                break;
            }

            cursor = nameEnd;
            cursor += VectorizedHtmlScanner.SkipWhitespaceVectorized(tagInner[cursor..]);
            if (cursor >= tagInner.Length || tagInner[cursor] != '=')
            {
                continue;
            }

            cursor++;
            if (!TryReadAttributeValue(tagInner, cursor, out var value, out _))
            {
                continue;
            }

            if (attrName.Equals("lang".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsMetaOpen(ReadOnlySpan<char> window, int ltIdx) =>
        IsTagOpen(window, ltIdx, "meta");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsTagOpen(ReadOnlySpan<char> window, int ltIdx, ReadOnlySpan<char> tagName)
    {
        if (window[ltIdx] != '<')
        {
            return false;
        }

        var nameStart = ltIdx + 1;
        if (nameStart >= window.Length)
        {
            return false;
        }

        if (window[nameStart] == '/')
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

    private static int FindTagOpen(ReadOnlySpan<char> window, ReadOnlySpan<char> tagName)
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
            if (window[idx] != '<')
            {
                cursor = idx + 1;
                continue;
            }

            if (IsTagOpen(window, idx, tagName))
            {
                return idx;
            }

            cursor = idx + 1;
        }

        return -1;
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

    private static bool TrySkipTagName(ReadOnlySpan<char> tagInner, ReadOnlySpan<char> tagName, out int cursor)
    {
        cursor = VectorizedHtmlScanner.SkipWhitespaceVectorized(tagInner);
        if (!tagInner.Slice(cursor).StartsWith(tagName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        cursor += tagName.Length;
        if (cursor < tagInner.Length && IsWordChar(tagInner[cursor]))
        {
            return false;
        }

        return true;
    }

    private static bool TryReadAttributeName(
        ReadOnlySpan<char> tagInner,
        int start,
        out ReadOnlySpan<char> name,
        out int end)
    {
        name = default;
        end = start;
        var i = start;
        while (i < tagInner.Length && !IsAttributeNameTerminator(tagInner[i]))
        {
            i++;
        }

        if (i == start)
        {
            return false;
        }

        name = tagInner.Slice(start, i - start);
        end = i;
        return true;
    }

    private static bool TryReadAttributeValue(
        ReadOnlySpan<char> tagInner,
        int start,
        out ReadOnlySpan<char> value,
        out int end)
    {
        value = default;
        end = start;
        var i = start + VectorizedHtmlScanner.SkipWhitespaceVectorized(tagInner[start..]);
        if (i >= tagInner.Length)
        {
            return false;
        }

        var quote = tagInner[i];
        if (quote == '"')
        {
            i++;
            var close = tagInner[i..].IndexOf('"');
            if (close < 0)
            {
                return false;
            }

            value = tagInner.Slice(i, close);
            end = i + close + 1;
            return true;
        }

        if (quote == '\'')
        {
            i++;
            var close = tagInner[i..].IndexOf('\'');
            if (close < 0)
            {
                return false;
            }

            value = tagInner.Slice(i, close);
            end = i + close + 1;
            return true;
        }

        var j = i;
        while (j < tagInner.Length && !IsUnquotedAttributeTerminator(tagInner[j]))
        {
            j++;
        }

        value = tagInner.Slice(i, j - i);
        end = j;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAttributeNameTerminator(char c) =>
        c is ' ' or '\t' or '\r' or '\n' or '=' or '/' or '>';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsUnquotedAttributeTerminator(char c) =>
        c is ' ' or '\t' or '\r' or '\n' or '>';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWordChar(char c) =>
        (c >= 'a' && c <= 'z')
        || (c >= 'A' && c <= 'Z')
        || (c >= '0' && c <= '9')
        || c == '_';
}
