using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace OccamMcp.Core.Transport;

/// <summary>
/// MCP SDK 2.2 force-advertises <c>tools.listChanged=true</c> whenever a DI
/// <c>ToolCollection</c> is present, and always advertises an empty logging capability.
/// Occam keeps <c>listChanged=true</c> because the session surface can change at runtime
/// via <c>occam_client_capabilities(profile=…)</c> (and opt-in exam submit) and sends
/// <c>notifications/tools/list_changed</c>. Logging stays honest-off (no push logs).
/// </summary>
internal static class OccamCapabilityHonesty
{
    public static void RewriteOutgoingMessage(JsonRpcMessage? message)
    {
        if (message is not JsonRpcResponse response || response.Result is null)
        {
            return;
        }

        // Prefer mutating a JsonObject in place (AOT-safe). Other Result shapes are left alone.
        if (response.Result is not JsonObject root)
        {
            return;
        }

        // initialize / capability envelopes carry serverInfo + capabilities.
        if (!root.ContainsKey("capabilities") && !root.ContainsKey("serverInfo"))
        {
            return;
        }

        if (root["capabilities"] is not JsonObject caps)
        {
            return;
        }

        caps.Remove("logging");
        if (caps["tools"] is JsonObject tools)
        {
            tools["listChanged"] = true;
        }
    }
}
