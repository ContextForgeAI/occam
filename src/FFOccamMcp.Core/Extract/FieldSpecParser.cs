using System.Text.Json;

namespace OccamMcp.Core.Extract;

public static class FieldSpecParser
{
    public static FieldExtractionPlan Parse(string fieldsJson)
    {
        if (string.IsNullOrWhiteSpace(fieldsJson))
        {
            throw new ArgumentException("fields JSON is required.");
        }

        using var doc = JsonDocument.Parse(fieldsJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("fields must be a JSON object.");
        }

        var fields = new Dictionary<string, FieldSpec>(StringComparer.Ordinal);
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            fields[property.Name] = ParseSpec(property.Value);
        }

        if (fields.Count == 0)
        {
            throw new ArgumentException("fields object is empty.");
        }

        return new FieldExtractionPlan { Fields = fields };
    }

    public static FieldExtractionPlan ParseFromSchemaFields(JsonElement schemaFields)
    {
        if (schemaFields.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("schema fields must be a JSON object.");
        }

        var fields = new Dictionary<string, FieldSpec>(StringComparer.Ordinal);
        foreach (var property in schemaFields.EnumerateObject())
        {
            fields[property.Name] = ParseSpec(property.Value);
        }

        if (fields.Count == 0)
        {
            throw new ArgumentException("schema fields object is empty.");
        }

        return new FieldExtractionPlan { Fields = fields };
    }

    private static FieldSpec ParseSpec(JsonElement node)
    {
        if (!node.TryGetProperty("selector", out var selectorNode)
            || string.IsNullOrWhiteSpace(selectorNode.GetString()))
        {
            throw new ArgumentException("Each field requires a non-empty selector.");
        }

        var attr = node.TryGetProperty("attr", out var attrNode)
            ? attrNode.GetString() ?? "text"
            : "text";
        var multiple = node.TryGetProperty("multiple", out var multipleNode) && multipleNode.GetBoolean();
        int? divide = node.TryGetProperty("divide", out var divideNode) && divideNode.TryGetInt32(out var d) ? d : null;
        return new FieldSpec
        {
            Selector = selectorNode.GetString()!,
            Attribute = attr,
            Multiple = multiple,
            Divide = divide,
        };
    }
}
