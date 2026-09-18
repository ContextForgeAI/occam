using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using OccamMcp.Core.Exam;

namespace OccamMcp.Core.Transport;

/// <summary>
/// MCP SDK 2.2 force-advertises <c>tools.listChanged=true</c> whenever a DI
/// <c>ToolCollection</c> is present, and always advertises an empty logging capability.
/// By default Occam's tool set is fixed at process start and does not push
/// <c>notifications/message</c> logs — rewrite advertised capabilities for honesty.
/// When <c>OCCAM_EXAM_MCP=1</c>, <c>listChanged</c> is kept <c>true</c> because the exam
/// path may send <c>notifications/tools/list_changed</c> after a graded submit.
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
            // Default path: fixed profile → honest false. Exam MCP path: dynamic surface → true.
            tools["listChanged"] = ExamMcpRuntime.IsEnabled;
        }
    }
}
