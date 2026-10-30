using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace OccamMcp.Core.Transport;

/// <summary>
/// MCP SDK 2.2 force-advertises <c>tools.listChanged=true</c> whenever a DI
/// <c>ToolCollection</c> is present, and always advertises an empty logging capability.
/// Occam advertises <c>listChanged</c> only on modern negotiated protocols
/// (<c>2026-07-28</c>+) — legacy initialize must stay <c>false</c> (dual-era honesty).
/// Runtime <c>notifications/tools/list_changed</c> may still fire after
/// <c>occam_client_capabilities(profile=…)</c>; agents that do not handle the
/// notification should re-list after the tool returns <c>listChangedSent</c>.
/// Logging stays honest-off (no push logs).
/// </summary>
internal static class OccamCapabilityHonesty
{
    /// <summary>First protocol version that may advertise <c>tools.listChanged=true</c>.</summary>
    public const string ListChangedMinProtocol = "2026-07-28";

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
            var negotiated = root["protocolVersion"]?.GetValue<string>();
            tools["listChanged"] = SupportsListChangedAdvertise(negotiated);
        }
    }

    internal static bool SupportsListChangedAdvertise(string? protocolVersion) =>
        !string.IsNullOrWhiteSpace(protocolVersion)
        && string.CompareOrdinal(protocolVersion.Trim(), ListChangedMinProtocol) >= 0;
}
