using System.Text.Json;
using OccamMcp.Core.Transport;

namespace OccamMcp.L0Gate;

/// <summary>
/// Offline classifier / envelope tests for <see cref="McpArgumentBindingGuard"/>.
/// Live MCP host coverage lives in rc2-regression binding cases.
/// </summary>
internal static class McpArgumentBindingGuardUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        var missingUrl = new ArgumentException(
            "The arguments dictionary is missing a value for the required parameter 'url'.",
            "arguments");
        assert(
            "binding guard: missing required url is client-input failure",
            McpArgumentBindingGuard.IsClientInputBindingFailure(missingUrl));

        var missingClaims = new ArgumentException(
            "The arguments dictionary is missing a value for the required parameter 'claims'.",
            "arguments");
        assert(
            "binding guard: missing required claims is client-input failure",
            McpArgumentBindingGuard.IsClientInputBindingFailure(missingClaims));

        var wrongType = new JsonException(
            "The JSON value could not be converted to System.String. Path: $ | LineNumber: 0 | BytePositionInLine: 3.");
        assert(
            "binding guard: declared-type conversion JsonException is client-input failure",
            McpArgumentBindingGuard.IsClientInputBindingFailure(wrongType));

        var wrapped = new InvalidOperationException("wrapper", wrongType);
        assert(
            "binding guard: conversion failure still detected through InnerException",
            McpArgumentBindingGuard.IsClientInputBindingFailure(wrapped));

        assert(
            "binding guard: unrelated ArgumentException is NOT mapped",
            !McpArgumentBindingGuard.IsClientInputBindingFailure(
                new ArgumentException("unexpected internal state")));

        assert(
            "binding guard: ArgumentException with other ParamName is NOT mapped",
            !McpArgumentBindingGuard.IsClientInputBindingFailure(
                new ArgumentException("bad value", "url")));

        assert(
            "binding guard: NullReferenceException is NOT mapped",
            !McpArgumentBindingGuard.IsClientInputBindingFailure(new NullReferenceException()));

        assert(
            "binding guard: InvalidOperationException without conversion inner is NOT mapped",
            !McpArgumentBindingGuard.IsClientInputBindingFailure(
                new InvalidOperationException("pipeline invariant broken")));

        var result = McpArgumentBindingGuard.ToTypedInvalidArgumentsResult(missingUrl, "occam_claim_check");
        assert(
            "binding guard: MCP isError is not true (handled typed result, not invoke failure)",
            result.IsError is not true);
        assert("binding guard: result has one text content block", result.Content is { Count: 1 });
        var text = (result.Content![0] as ModelContextProtocol.Protocol.TextContentBlock)?.Text;
        assert("binding guard: payload is JSON object", text?.TrimStart().StartsWith('{') == true);
        assert("binding guard: payload ok=false", text!.Contains("\"ok\":false", StringComparison.Ordinal));
        assert(
            "binding guard: payload failureCode=invalid_arguments",
            text.Contains("\"failureCode\":\"invalid_arguments\"", StringComparison.Ordinal));
        assert(
            "binding guard: payload mentions missing url",
            text.Contains("required parameter 'url'", StringComparison.OrdinalIgnoreCase));
        assert(
            "binding guard: payload mentions tool name",
            text.Contains("occam_claim_check", StringComparison.Ordinal));
        using (var doc = JsonDocument.Parse(text))
        {
            assert("binding guard: envelope ok property is boolean false",
                doc.RootElement.TryGetProperty("ok", out var okEl) && okEl.ValueKind == JsonValueKind.False);
            assert("binding guard: envelope failureCode is invalid_arguments",
                doc.RootElement.TryGetProperty("failureCode", out var codeEl)
                && codeEl.GetString() == McpArgumentBindingGuard.FailureCode);
        }

        Console.WriteLine("L_MCP_BINDING_GUARD_OK");
    }
}
