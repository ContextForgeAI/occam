using ModelContextProtocol.Protocol;

namespace OccamMcp.Core.Transport;

/// <summary>
/// Narrow guard for MCP SDK argument-binding failures that otherwise escape as
/// <c>McpServerImpl.ToolCallError</c> ("threw an unhandled exception") and opaque
/// <c>An error occurred invoking '…'</c> results.
/// </summary>
/// <remarks>
/// Only classifies known client-input binding failures from MEAI parameter marshalling.
/// Unrelated exceptions are rethrown so genuine host faults keep the SDK error path.
/// </remarks>
public static class McpArgumentBindingGuard
{
    public const string FailureCode = "invalid_arguments";

    /// <summary>
    /// Returns true only for recognized client-input binding failures thrown before/during
    /// MEAI parameter marshalling (missing required args, declared-type conversion failures).
    /// </summary>
    public static bool IsClientInputBindingFailure(Exception exception)
    {
        for (Exception? cur = exception; cur is not null; cur = cur.InnerException)
        {
            if (cur is ArgumentException argumentException
                && string.Equals(argumentException.ParamName, "arguments", StringComparison.Ordinal)
                && argumentException.Message.Contains(
                    "missing a value for the required parameter",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // MEAI/STJ conversion of a supplied JSON value into the declared CLR parameter type
            // (e.g. url:123 → string). Tool-body JsonExceptions are caught inside tools and do
            // not escape to the CallTool filter.
            if (cur is System.Text.Json.JsonException jsonException
                && jsonException.Message.Contains(
                    "could not be converted",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static CallToolResult ToTypedInvalidArgumentsResult(Exception exception, string? toolName)
    {
        var message = BuildMessage(exception, toolName);
        var payload =
            "{\"ok\":false,\"failureCode\":\"" + FailureCode + "\",\"message\":" + JsonString(message)
            + ",\"timestamp\":" + JsonString(DateTimeOffset.UtcNow.ToString("O")) + "}";

        return new CallToolResult
        {
            // Established Occam public contract: expected typed failures are normal tool *results*
            // (JSON string with ok:false + failureCode), not MCP CallToolResult.IsError=true.
            // Live digest/transcode/claim_check invalid_arguments omit isError entirely; leave
            // IsError unset so the wire shape matches those tools. Unexpected faults still use the
            // SDK ToolCallError path (opaque isError=true) outside this filter.
            Content = [new TextContentBlock { Text = payload }],
        };
    }

    public static string BuildMessage(Exception exception, string? toolName)
    {
        var detail = UnwrapMessage(exception);
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return detail;
        }

        return $"{detail} (tool: {toolName})";
    }

    public static void LogBindingRejection(string? toolName, Exception exception)
    {
        // stderr diagnostics only — must not use the SDK ToolCallError / Event Log unhandled path.
        Console.Error.WriteLine(
            $"[occam.mcp] argument binding rejected tool={toolName ?? "(unknown)"}: {UnwrapMessage(exception)}");
    }

    private static string UnwrapMessage(Exception exception)
    {
        for (Exception? cur = exception; cur is not null; cur = cur.InnerException)
        {
            if (cur is ArgumentException or System.Text.Json.JsonException)
            {
                // Prefer the leaf binding message without the "(Parameter 'arguments')" suffix noise
                // when present — keep the required-parameter name for callers.
                var msg = cur.Message;
                var idx = msg.IndexOf(" (Parameter ", StringComparison.Ordinal);
                return idx > 0 ? msg[..idx] : msg;
            }
        }

        return exception.Message;
    }

    /// <summary>Minimal AOT-safe JSON string literal encoding (no reflection serializer).</summary>
    internal static string JsonString(string value)
    {
        return "\"" + value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal)
            + "\"";
    }
}
