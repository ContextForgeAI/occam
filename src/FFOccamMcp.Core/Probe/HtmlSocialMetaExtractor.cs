using OccamMcp.Core.Text;

namespace OccamMcp.Core.Probe;

public sealed record SocialMeta(
    string? Title,
    string? Description,
    string? Image,
    string? TwitterCard,
    string? SiteName,
    string? Lang);

public static class HtmlSocialMetaExtractor
{
    public static SocialMeta Extract(string html, string? pageUrl = null)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return new SocialMeta(null, null, null, null, null, null);
        }

        var fields = HtmlHeadScanner.Scan(html.AsSpan());

        var ogTitle = FirstDecoded(fields.OgTitle, fields.NameTitle);
        var ogDesc = FirstDecoded(fields.OgDescription, fields.NameDescription);
        var ogImage = Decode(fields.OgImage);
        var site = Decode(fields.SiteName);
        var twitter = FirstDecoded(fields.TwitterCardProperty, fields.TwitterCardName);
        var lang = Decode(fields.Lang);

        if (!string.IsNullOrWhiteSpace(ogImage) && pageUrl is not null
            && Uri.TryCreate(pageUrl, UriKind.Absolute, out var baseUri)
            && Uri.TryCreate(baseUri, ogImage, out var absImage))
        {
            ogImage = absImage.ToString();
        }

        return new SocialMeta(
            Trim(ogTitle),
            Trim(ogDesc),
            Trim(ogImage),
            Trim(twitter),
            Trim(site),
            Trim(lang));
    }

    private static string? FirstDecoded(ReadOnlySpan<char> primary, ReadOnlySpan<char> fallback)
    {
        var value = Decode(primary);
        return value ?? Decode(fallback);
    }

    private static string? Decode(ReadOnlySpan<char> value)
    {
        var trimmed = TrimSpan(value);
        return trimmed.IsEmpty ? null : System.Net.WebUtility.HtmlDecode(trimmed.ToString());
    }

    private static ReadOnlySpan<char> TrimSpan(ReadOnlySpan<char> value)
    {
        var start = 0;
        var end = value.Length - 1;
        while (start <= end && char.IsWhiteSpace(value[start]))
        {
            start++;
        }

        while (end >= start && char.IsWhiteSpace(value[end]))
        {
            end--;
        }

        return value.Slice(start, end - start + 1);
    }

    private static string? Trim(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
