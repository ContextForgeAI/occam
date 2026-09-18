using System.Text.Json;
using OccamMcp.Core.Exam;
using OccamMcp.Core.Tools;
using OccamMcp.Core.Transport;
using Xunit;

namespace OccamMcp.Core.Tests.Exam;

/// <summary>
/// Tool handler tests without a live MCP session (notification path needs a real McpServer).
/// </summary>
public sealed class OccamExamSubmitToolTests
{
    private const string PerfectSubmission = """
        {
          "clientInfo": "tool-test/1.0",
          "modelHint": "mock-strong",
          "sessionId": "s1",
          "canaryVerdict": "READ_VERIFIED",
          "basicCallArguments": {"url":"https://example.com"},
          "focusBudgetArguments": {"focus_query":"x","max_tokens":800},
          "chain": {
            "calls": [
              {"url":"https://example.com"},
              {"diff_against":"sha256:abc"}
            ],
            "producedValue": "sha256:abc"
          }
        }
        """;

    [Fact]
    public void Runtime_Submit_ProducesGradeEnvelopeFields()
    {
        var surface = new SessionToolSurface(OccamToolProfile.Reader, isPinned: false);
        var runtime = new ExamMcpRuntime(new ExamResultCache(), surface);
        var outcome = runtime.Submit(PerfectSubmission);

        Assert.True(outcome.Ok);
        Assert.Equal(4, outcome.Result.Score);
        Assert.Equal("strong", outcome.Result.TierWire);
        Assert.True(outcome.Applied);
        Assert.Equal(OccamToolProfile.Full, outcome.ActiveProfile);
    }

    [Fact]
    public void FailureEnvelope_SerializesWithSourceGen()
    {
        var json = JsonSerializer.Serialize(
            new OccamExamFailureResponse(false, new OccamExamFailureInfo("invalid_arguments", "bad"), "2026-01-01T00:00:00Z"),
            OccamExamJsonContext.Default.OccamExamFailureResponse);

        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("invalid_arguments", doc.RootElement.GetProperty("failure").GetProperty("code").GetString());
    }
}
