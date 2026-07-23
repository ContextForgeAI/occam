namespace OccamMcp.Core.Compile;

/// <summary>Separates a URL fragment used as local focus intent from the URL sent to a backend.</summary>
public sealed record FocusIntent(string FetchUrl, string? Fragment)
{
    private const int MaxFragmentCharacters = 512;

    public static FocusIntent FromUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Fragment))
        {
            return new FocusIntent(url, null);
        }

        var raw = uri.Fragment.TrimStart('#');
        string decoded;
        try
        {
            decoded = Uri.UnescapeDataString(raw);
        }
        catch (UriFormatException)
        {
            decoded = raw;
        }

        decoded = decoded.Length <= MaxFragmentCharacters
            ? decoded
            : decoded[..MaxFragmentCharacters];
        var builder = new UriBuilder(uri) { Fragment = string.Empty };
        return new FocusIntent(builder.Uri.AbsoluteUri, string.IsNullOrWhiteSpace(decoded) ? null : decoded);
    }
}
