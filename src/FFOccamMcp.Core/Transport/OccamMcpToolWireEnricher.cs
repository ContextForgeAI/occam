using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace OccamMcp.Core.Transport;

/// <summary>
/// MCP 2026 wire enrichments: duplicate JSON tool bodies into <c>structuredContent</c>,
/// advertise a permissive <c>outputSchema</c>, set conservative tool annotations, and mark
/// typed <c>ok:false</c> envelopes with <c>isError:true</c> (MCP tool-level failure) while
/// keeping the JSON body for agents that read the Occam envelope.
/// </summary>
internal static class OccamMcpToolWireEnricher
{
    private static readonly JsonElement EnvelopeOutputSchema = LoadEnvelopeOutputSchema();

    private static readonly HashSet<string> ReadOnlyTools = new(StringComparer.Ordinal)
    {
        "occam_client_capabilities",
        "occam_transcode",
        "occam_probe",
        "occam_digest",
        "occam_playbook_resolve",
        "occam_map",
        "occam_extract_knowledge",
        "occam_search",
        "occam_verify",
        "occam_claim_check",
        "occam_attest",
        "occam_playbook_lint",
        "occam_dataset_export",
        "occam_watch",
        "occam_crosscheck",
        "occam_failure_atlas",
        "occam_batch_status",
        "occam_batch_results",
    };

    private static readonly HashSet<string> MutatingTools = new(StringComparer.Ordinal)
    {
        "occam_playbook_heal",
        "occam_playbook_save",
        "occam_batch_submit",
        "occam_browser_interact",
    };

    public static CallToolResult EnrichCallToolResult(CallToolResult result)
    {
        if (!TryExtractJsonObjectText(result, out var jsonText))
        {
            return result;
        }

        try
        {
            using var document = JsonDocument.Parse(jsonText);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return result;
            }

            if (result.StructuredContent is null
                || result.StructuredContent.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                result.StructuredContent = document.RootElement.Clone();
            }

            // MCP tool-level failure signal for typed Occam envelopes (ok:false). Content stays
            // the JSON string so clients can still read failureCode / agentMeta.
            if (document.RootElement.TryGetProperty("ok", out var ok)
                && ok.ValueKind == JsonValueKind.False)
            {
                result.IsError = true;
            }

            return result;
        }
        catch (JsonException)
        {
            return result;
        }
    }

    public static ListToolsResult EnrichListToolsResult(ListToolsResult result)
    {
        if (result.Tools is null || result.Tools.Count == 0)
        {
            return result;
        }

        foreach (var tool in result.Tools)
        {
            if (tool.OutputSchema is null
                || tool.OutputSchema.Value.ValueKind == JsonValueKind.Undefined)
            {
                tool.OutputSchema = EnvelopeOutputSchema;
            }

            tool.Annotations ??= new ToolAnnotations();
            if (ReadOnlyTools.Contains(tool.Name))
            {
                tool.Annotations.ReadOnlyHint = true;
                tool.Annotations.OpenWorldHint = true;
            }
            else if (MutatingTools.Contains(tool.Name))
            {
                tool.Annotations.ReadOnlyHint = false;
                tool.Annotations.OpenWorldHint = true;
                if (tool.Name is "occam_playbook_heal" or "occam_playbook_save")
                {
                    tool.Annotations.DestructiveHint = false;
                    tool.Annotations.IdempotentHint = false;
                }
            }
            else if (tool.Name == "occam_browser_interact")
            {
                tool.Annotations.ReadOnlyHint = false;
                tool.Annotations.OpenWorldHint = true;
                tool.Annotations.DestructiveHint = false;
                tool.Annotations.IdempotentHint = false;
            }
            else
            {
                tool.Annotations.OpenWorldHint = true;
            }
        }

        return result;
    }

    private static bool TryExtractJsonObjectText(CallToolResult result, out string jsonText)
    {
        jsonText = string.Empty;
        if (result.Content is null || result.Content.Count != 1)
        {
            return false;
        }

        if (result.Content[0] is not TextContentBlock textBlock
            || string.IsNullOrWhiteSpace(textBlock.Text))
        {
            return false;
        }

        var trimmed = textBlock.Text.TrimStart();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            return false;
        }

        jsonText = textBlock.Text;
        return true;
    }

    private static JsonElement LoadEnvelopeOutputSchema()
    {
        const string schema = """
            {
              "type": "object",
              "description": "Occam tool JSON envelope (camelCase). content[].text duplicates this object for legacy MCP clients.",
              "properties": {
                "ok": { "type": "boolean" }
              },
              "additionalProperties": true
            }
            """;
        using var document = JsonDocument.Parse(schema);
        return document.RootElement.Clone();
    }
}
