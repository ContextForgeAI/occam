using System.Text.Json;
using ModelContextProtocol.Protocol;
using OccamMcp.Core.Transport;

namespace OccamMcp.L0Gate;

internal static class OccamMcpToolWireEnricherUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        var raw = new CallToolResult
        {
            Content = [new TextContentBlock { Text = "{\"ok\":true,\"url\":\"https://example.com/\"}" }],
            // ModelContextProtocol 2.2 infers a JSON string here after tools/list advertises
            // outputSchema for a string-returning handler. The wire contract requires a record.
            StructuredContent = JsonDocument.Parse(
                "\"{\\\"ok\\\":true,\\\"url\\\":\\\"https://example.com/\\\"}\"").RootElement.Clone(),
        };
        var enriched = OccamMcpToolWireEnricher.EnrichCallToolResult(raw);
        assert(
            "wire enricher: structuredContent is an object/record, not a JSON string",
            enriched.StructuredContent is { ValueKind: JsonValueKind.Object }
            && enriched.StructuredContent.Value.TryGetProperty("ok", out var ok)
            && ok.ValueKind == JsonValueKind.True);
        assert(
            "wire enricher: ok:true does not set isError",
            enriched.IsError is not true);

        var failRaw = new CallToolResult
        {
            Content = [new TextContentBlock { Text = "{\"ok\":false,\"failureCode\":\"thin_extract\"}" }],
        };
        var failEnriched = OccamMcpToolWireEnricher.EnrichCallToolResult(failRaw);
        assert(
            "wire enricher: ok:false sets isError:true",
            failEnriched.IsError is true);
        assert(
            "wire enricher: ok:false still mirrors structuredContent",
            failEnriched.StructuredContent is { ValueKind: JsonValueKind.Object }
            && failEnriched.StructuredContent.Value.TryGetProperty("ok", out var failOk)
            && failOk.ValueKind == JsonValueKind.False);

        var list = new ListToolsResult
        {
            Tools =
            [
                new Tool { Name = "occam_probe", InputSchema = JsonDocument.Parse("{\"type\":\"object\"}").RootElement },
            ],
        };
        var listed = OccamMcpToolWireEnricher.EnrichListToolsResult(list);
        var probe = listed.Tools![0];
        assert(
            "wire enricher: outputSchema advertised",
            probe.OutputSchema is { ValueKind: JsonValueKind.Object });
        assert(
            "wire enricher: readOnlyHint on probe",
            probe.Annotations?.ReadOnlyHint == true);

        Console.WriteLine("L_MCP_WIRE_ENRICHER_OK");
    }
}
