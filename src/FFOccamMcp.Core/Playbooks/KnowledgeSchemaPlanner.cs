using System.Text.Json;

namespace OccamMcp.Core.Playbooks;

public sealed record KnowledgeSchemaMatch(
    string PageClass,
    JsonElement SchemaFields);

public static class KnowledgeSchemaPlanner
{
    public static bool TryMatch(
        JsonElement playbookRoot,
        string url,
        out KnowledgeSchemaMatch? match,
        out string? failureCode)
    {
        match = null;
        failureCode = null;

        if (playbookRoot.ValueKind != JsonValueKind.Object)
        {
            failureCode = "playbook_not_found";
            return false;
        }

        if (!playbookRoot.TryGetProperty("knowledge_schema", out var knowledgeSchema)
            || knowledgeSchema.ValueKind != JsonValueKind.Object
            || !knowledgeSchema.EnumerateObject().Any())
        {
            failureCode = "knowledge_schema_missing";
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            failureCode = "invalid_arguments";
            return false;
        }

        var path = uri.AbsolutePath;
        string? matchedClass = null;

        if (playbookRoot.TryGetProperty("genome", out var genome)
            && genome.TryGetProperty("page_classes", out var pageClasses)
            && pageClasses.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in pageClasses
                         .EnumerateObject()
                         .Select(p => (Name: p.Name, Pattern: ReadString(p.Value)))
                         .Where(p => !string.IsNullOrWhiteSpace(p.Pattern))
                         .OrderByDescending(p => p.Pattern!.Length))
            {
                if (!PageClassMatcher.Matches(path, entry.Pattern!)
                    || !knowledgeSchema.TryGetProperty(entry.Name, out _))
                {
                    continue;
                }

                matchedClass = entry.Name;
                break;
            }
        }

        matchedClass ??= knowledgeSchema.TryGetProperty("default", out _) ? "default" : null;
        if (matchedClass is null)
        {
            failureCode = "page_class_unmatched";
            return false;
        }

        if (!knowledgeSchema.TryGetProperty(matchedClass, out var fieldsObj)
            || fieldsObj.ValueKind != JsonValueKind.Object
            || !fieldsObj.EnumerateObject().Any())
        {
            failureCode = "knowledge_schema_empty";
            return false;
        }

        match = new KnowledgeSchemaMatch(matchedClass, fieldsObj.Clone());
        return true;
    }

    private static string? ReadString(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
