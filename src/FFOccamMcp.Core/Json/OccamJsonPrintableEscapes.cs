using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace OccamMcp.Core.Json;

/// <summary>
/// System.Text.Json's default JavaScriptEncoder rewrites HTML-sensitive printable characters as
/// \u003E, \u0022, and \u0027 inside JSON string values. After MCP wraps that JSON as content[].text,
/// agents often see those escapes literally in markdown (blockquotes, quotes, apostrophes) even though
/// in-memory extract text and structured blocks[].text were correct.
///
/// This helper rewrites only those three printable escapes on already-serialized JSON.
/// It deliberately leaves \u003C and \u0026 escaped so embedding the payload in HTML keeps the XSS
/// floor of the default encoder (does not enable UnsafeRelaxedJsonEscaping).
/// </summary>
public static class OccamJsonPrintableEscapes
{
    /// <summary>Relax printable unicode escapes in a JSON document string. Idempotent for already-relaxed input.</summary>
    public static string Relax(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return json;
        }

        // Order matters: rewrite \u0022 to \" (not raw ") so the document stays valid JSON.
        return json
            .Replace("\\u003E", ">", StringComparison.Ordinal)
            .Replace("\\u003e", ">", StringComparison.Ordinal)
            .Replace("\\u0027", "'", StringComparison.Ordinal)
            .Replace("\\u0022", "\\\"", StringComparison.Ordinal);
    }

    /// <summary>Serialize with source-gen type info, then relax printable escapes for MCP wire readability.</summary>
    public static string Serialize<T>(T value, JsonTypeInfo<T> typeInfo) =>
        Relax(JsonSerializer.Serialize(value, typeInfo));
}
