namespace OccamMcp.Core.Configuration;

internal static class OccamEnvironment
{
    public static string? Get(string primary, string? fallback = null)
    {
        var value = Environment.GetEnvironmentVariable(primary);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return string.IsNullOrWhiteSpace(fallback)
            ? null
            : Environment.GetEnvironmentVariable(fallback);
    }

    public static string? GetExistingFile(string primary, string? fallback = null)
    {
        var value = Get(primary, fallback);
        return !string.IsNullOrWhiteSpace(value) && File.Exists(value) ? value : null;
    }

    public static int GetInt(string primary, int defaultValue, int min, int max, string? fallback = null)
    {
        var raw = Get(primary, fallback);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        // A value WAS set — if it silently parses to a default or gets clamped, the operator thinks they
        // configured something they didn't. Emit one stderr line (diagnostics channel) so it's visible.
        if (!int.TryParse(raw, out var parsed))
        {
            Console.Error.WriteLine($"[occam.config] {primary}='{raw}' is not an integer — using default {defaultValue}.");
            return defaultValue;
        }

        var clamped = Math.Clamp(parsed, min, max);
        if (clamped != parsed)
        {
            Console.Error.WriteLine($"[occam.config] {primary}={parsed} is out of range [{min}..{max}] — clamped to {clamped}.");
        }

        return clamped;
    }

    public static bool GetFlag(string primary, bool defaultValue, string? fallback = null)
    {
        var raw = Get(primary, fallback);
        if (raw is null)
        {
            return defaultValue;
        }

        return string.Equals(raw, "1", StringComparison.Ordinal)
            || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
    }
}
