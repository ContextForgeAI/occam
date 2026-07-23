using System.Text.Json;

namespace OccamMcp.Core.Routing;

public static class ContentSelectorsParser
{
    public static bool TryParse(string? value, out string[] selectors, out string? error)
    {
        selectors = [];
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        if (!trimmed.StartsWith('['))
        {
            selectors = trimmed
                .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => s.Length > 0)
                .ToArray();
            return true;
        }

        return JsonStringArrayParser.TryParse(trimmed, out selectors, out error);
    }
}

internal static class JsonStringArrayParser
{
    public static bool TryParse(string json, out string[] values, out string? error)
    {
        values = [];
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                error = "Expected a JSON string array.";
                return false;
            }

            var list = new List<string>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.String)
                {
                    error = "Array entries must be strings.";
                    return false;
                }

                var s = el.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    list.Add(s);
                }
            }

            values = list.ToArray();
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
