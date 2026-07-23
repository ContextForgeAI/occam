namespace OccamMcp.Core.Routing;

public static class LoginWallDetector
{
    public static bool LooksLikeLoginWall(string markdown, string url)
    {
        if (DomainTierRegistry.IsLoginPath(url))
        {
            return true;
        }

        if (DomainTierRegistry.IsPublicReferencePage(url))
        {
            return false;
        }

        var lower = markdown.ToLowerInvariant();
        if (TextNeedle.ContainsAnyPhrase(lower,
                "sign in to continue",
                "log in to continue",
                "authentication required",
                "please log in",
                "please sign in"))
        {
            return true;
        }

        // Word-boundary on "password" avoids nginx/RFC directive prose (ssl_password_file, etc.).
        // Bare "login" is omitted — it false-positives on hostnames like login.example.com in code samples.
        return TextNeedle.ContainsWord(lower, "password")
            && TextNeedle.ContainsAnyPhrase(lower, "sign in", "log in", "log in to", "sign in to");
    }
}
