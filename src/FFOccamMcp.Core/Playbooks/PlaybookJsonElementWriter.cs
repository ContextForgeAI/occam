using System.Text;
using System.Text.Json;

namespace OccamMcp.Core.Playbooks;

/// <summary>AOT-safe JSON helpers for playbook mutate/export (no JsonNode.Add).</summary>
internal static class PlaybookJsonElementWriter
{
    public static JsonElement CreateLesson(
        string note,
        string? failureReason,
        int? verifyScore,
        string? hostId)
    {
        var trimmedNote = note.Trim();
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("at", DateTime.UtcNow.ToString("O"));
            writer.WriteString("note", trimmedNote[..Math.Min(trimmedNote.Length, 500)]);

            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                writer.WriteString("failure_reason", failureReason.Trim());
            }

            if (verifyScore is not null)
            {
                writer.WriteNumber("verify_score", verifyScore.Value);
            }

            if (!string.IsNullOrWhiteSpace(hostId))
            {
                var trimmedHost = hostId.Trim();
                writer.WriteString("host", trimmedHost[..Math.Min(trimmedHost.Length, 64)]);
            }

            writer.WriteEndObject();
        }

        return ParseRootElement(stream);
    }

    public static JsonElement CreateArray(IReadOnlyList<JsonElement> items)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var item in items)
            {
                item.WriteTo(writer);
            }

            writer.WriteEndArray();
        }

        return ParseRootElement(stream);
    }

    public static string ReplaceRootProperty(
        JsonElement root,
        string propertyName,
        JsonElement newValue,
        bool indented = false) =>
        Encoding.UTF8.GetString(ReplaceRootPropertyBytes(root, propertyName, newValue, indented));

    public static JsonElement ReplaceRootPropertyElement(
        JsonElement root,
        string propertyName,
        JsonElement newValue)
    {
        using var doc = JsonDocument.Parse(ReplaceRootPropertyBytes(root, propertyName, newValue, indented: false));
        return doc.RootElement.Clone();
    }

    private static byte[] ReplaceRootPropertyBytes(
        JsonElement root,
        string propertyName,
        JsonElement newValue,
        bool indented)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = indented }))
        {
            writer.WriteStartObject();
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.NameEquals(propertyName))
                {
                    continue;
                }

                prop.WriteTo(writer);
            }

            writer.WritePropertyName(propertyName);
            newValue.WriteTo(writer);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    public static JsonElement RedactLessonHost(JsonElement lesson, Func<string?, bool> shouldRedactHost)
    {
        if (lesson.ValueKind != JsonValueKind.Object)
        {
            return lesson;
        }

        if (!lesson.TryGetProperty("host", out var hostProp) || hostProp.ValueKind != JsonValueKind.String)
        {
            return lesson;
        }

        if (!shouldRedactHost(hostProp.GetString()))
        {
            return lesson;
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var prop in lesson.EnumerateObject())
            {
                if (prop.NameEquals("host"))
                {
                    writer.WriteString("host", "redacted");
                }
                else
                {
                    prop.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        return ParseRootElement(stream);
    }

    private static JsonElement ParseRootElement(MemoryStream stream)
    {
        using var doc = JsonDocument.Parse(stream.ToArray());
        return doc.RootElement.Clone();
    }
}
