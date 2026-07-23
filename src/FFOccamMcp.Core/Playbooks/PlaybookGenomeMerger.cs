using System.Text.Json;

namespace OccamMcp.Core.Playbooks;

public static class PlaybookGenomeMerger
{
    public static JsonElement ParseRoot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return ParseBytes("{}");
        }

        return ParseBytes(json);
    }

    public static JsonElement ToElement(JsonElement node) => node.Clone();

    public static JsonElement? ToElementOrNull(JsonElement? node) => node?.Clone();

    public static JsonElement? GetObject(JsonElement root, string key)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty(key, out var value)
            || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return value.Clone();
    }

    public static JsonElement? MergeGenome(JsonElement? playbookGenome, JsonElement? siteGenome)
    {
        if (!playbookGenome.HasValue && !siteGenome.HasValue)
        {
            return null;
        }

        if (!playbookGenome.HasValue)
        {
            return siteGenome!.Value.Clone();
        }

        if (!siteGenome.HasValue)
        {
            return playbookGenome!.Value.Clone();
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var prop in siteGenome.Value.EnumerateObject())
            {
                if (playbookGenome.Value.TryGetProperty(prop.Name, out _))
                {
                    continue;
                }

                prop.WriteTo(writer);
            }

            foreach (var prop in playbookGenome.Value.EnumerateObject())
            {
                prop.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return ParseStream(stream);
    }

    public static JsonElement? MergeKnowledgeSchema(JsonElement? playbookSchema, JsonElement? siteSchema)
    {
        if (playbookSchema.HasValue
            && playbookSchema.Value.ValueKind == JsonValueKind.Object
            && playbookSchema.Value.EnumerateObject().Any())
        {
            return playbookSchema.Value.Clone();
        }

        return siteSchema?.Clone();
    }

    private static JsonElement ParseBytes(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static JsonElement ParseStream(MemoryStream stream)
    {
        using var doc = JsonDocument.Parse(stream.ToArray());
        return doc.RootElement.Clone();
    }
}
