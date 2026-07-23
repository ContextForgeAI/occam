using System.Text.Json;

namespace OccamMcp.Core.Playbooks;

/// <summary>Rejects community playbook JSON that may carry session secrets.</summary>
public static class PlaybookCommunityHygiene
{
    private static readonly HashSet<string> ForbiddenPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "cookie",
        "cookies",
        "authorization",
        "set-cookie",
        "set_cookie",
        "bearer",
        "bearer_token",
        "api_key",
        "apikey",
        "password",
        "secret_key",
        "session_token",
        "access_token",
        "refresh_token",
    };

    public static bool ContainsForbiddenKeys(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return Walk(doc.RootElement);
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private static bool Walk(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (ForbiddenPropertyNames.Contains(property.Name))
                    {
                        return true;
                    }

                    if (Walk(property.Value))
                    {
                        return true;
                    }
                }

                return false;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (Walk(item))
                    {
                        return true;
                    }
                }

                return false;
            default:
                return false;
        }
    }
}
