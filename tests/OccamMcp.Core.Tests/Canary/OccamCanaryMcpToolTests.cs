using System.Text.Json;
using System.Text.RegularExpressions;
using OccamMcp.Core.Canary;
using OccamMcp.Core.Time;
using OccamMcp.Core.Tools;
using OccamMcp.Core.Transport;
using Xunit;

namespace OccamMcp.Core.Tests.Canary;

/// <summary>
/// MCP canary tools: issue (no sentinel), verify (four verdicts), and issue→materialize→verify.
/// </summary>
public sealed class OccamCanaryMcpToolTests
{
    private const long BucketAlignedUnixSeconds = 1_800_000_000;

    [Fact]
    public async Task Issue_ReturnsUrlAndSession_WithoutSentinel()
    {
        await using var runtime = new CanaryMcpRuntime(new CanaryOptions());
        var tool = new OccamCanaryIssueTool(runtime);

        var json = await tool.Issue(session_id: null, ttl_seconds: null);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("ok").GetBoolean());
        var url = root.GetProperty("url").GetString();
        var sessionId = root.GetProperty("sessionId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(url));
        Assert.False(string.IsNullOrWhiteSpace(sessionId));
        Assert.Contains("/probe/canary/", url, StringComparison.Ordinal);
        Assert.True(root.TryGetProperty("expiresAt", out _));
        Assert.True(root.TryGetProperty("bucket", out _));

        // Sentinel must never appear in the issue response.
        var issue = runtime.Service.Issue(sessionId!);
        Assert.DoesNotContain(issue.Sentinel, json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_RejectsInvalidTtl()
    {
        await using var runtime = new CanaryMcpRuntime(new CanaryOptions());
        var tool = new OccamCanaryIssueTool(runtime);

        var json = await tool.Issue(ttl_seconds: 5);
        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("invalid_arguments", doc.RootElement.GetProperty("failure").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Verify_ReadVerified_WhenSentinelFresh()
    {
        await using var runtime = new CanaryMcpRuntime(new CanaryOptions());
        var issueTool = new OccamCanaryIssueTool(runtime);
        var verifyTool = new OccamCanaryVerifyTool(runtime);

        var issued = JsonDocument.Parse(await issueTool.Issue());
        var sessionId = issued.RootElement.GetProperty("sessionId").GetString()!;
        Assert.True(runtime.TryMaterializeProbe(issued.RootElement.GetProperty("url").GetString()!, out var page));
        var sentinel = ExtractSentinelFromMarkdown(page.Markdown!);

        var json = await verifyTool.Verify(sessionId, sentinel);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(CanaryVerdictStrings.ReadVerified, doc.RootElement.GetProperty("verdict").GetString());
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Verify_Hallucinated_OnWrongSentinel()
    {
        await using var runtime = new CanaryMcpRuntime(new CanaryOptions());
        var issueTool = new OccamCanaryIssueTool(runtime);
        var verifyTool = new OccamCanaryVerifyTool(runtime);

        var issued = JsonDocument.Parse(await issueTool.Issue());
        var sessionId = issued.RootElement.GetProperty("sessionId").GetString()!;

        var json = await verifyTool.Verify(sessionId, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(CanaryVerdictStrings.Hallucinated, doc.RootElement.GetProperty("verdict").GetString());
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Verify_ReplaySuspect_WhenAuthenticButNeverIssued()
    {
        var options = new CanaryOptions();
        var clock = ManualClock.AtUnixSeconds(BucketAlignedUnixSeconds);
        using var secret = CanarySecret.FromRootKey(CanaryCliVerbs.FixedRootKey());
        using var service = new CanaryService(secret, options, timeProvider: clock);
        await using var runtime = new CanaryMcpRuntime(service);
        var verifyTool = new OccamCanaryVerifyTool(runtime);

        var sessionId = "replay-session-1";
        var bucket = CanarySentinel.BucketFor(clock.GetUtcNow(), options.BucketSeconds);
        var forged = secret.DeriveSentinel(bucket, sessionId, options.SentinelBytes);

        var json = await verifyTool.Verify(sessionId, forged);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(CanaryVerdictStrings.ReplaySuspect, doc.RootElement.GetProperty("verdict").GetString());
    }

    [Fact]
    public async Task Verify_ReadStale_WhenPastFreshWindow()
    {
        var options = new CanaryOptions();
        var clock = ManualClock.AtUnixSeconds(BucketAlignedUnixSeconds);
        using var secret = CanarySecret.FromRootKey(CanaryCliVerbs.FixedRootKey());
        using var service = new CanaryService(secret, options, timeProvider: clock);
        await using var runtime = new CanaryMcpRuntime(service);
        var verifyTool = new OccamCanaryVerifyTool(runtime);

        var sessionId = "stale-session-1";
        var issue = service.Issue(sessionId);
        clock.AdvanceBuckets(options.FreshBucketTolerance + 1, options.BucketSeconds);

        var json = await verifyTool.Verify(sessionId, issue.Sentinel);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(CanaryVerdictStrings.ReadStale, doc.RootElement.GetProperty("verdict").GetString());
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Integration_Issue_Materialize_Verify_ReadVerified()
    {
        await using var runtime = new CanaryMcpRuntime(new CanaryOptions());
        var issueTool = new OccamCanaryIssueTool(runtime);
        var verifyTool = new OccamCanaryVerifyTool(runtime);

        var issuedJson = await issueTool.Issue();
        using var issued = JsonDocument.Parse(issuedJson);
        var url = issued.RootElement.GetProperty("url").GetString()!;
        var sessionId = issued.RootElement.GetProperty("sessionId").GetString()!;

        Assert.True(runtime.TryMaterializeProbe(url, out var page));
        Assert.True(page.Ok);
        Assert.Equal("canary", page.Backend);
        var sentinel = ExtractSentinelFromMarkdown(page.Markdown!);

        // Also reachable over HTTP (probe host).
        using var client = new HttpClient();
        var html = await client.GetStringAsync(url);
        Assert.Contains(sentinel, html, StringComparison.Ordinal);

        var verifyJson = await verifyTool.Verify(sessionId, sentinel);
        using var verified = JsonDocument.Parse(verifyJson);
        Assert.Equal(CanaryVerdictStrings.ReadVerified, verified.RootElement.GetProperty("verdict").GetString());
    }

    [Fact]
    public void Profiles_ExposeCanaryOnReaderAndFull_NotMinimal()
    {
        Assert.True(OccamToolProfile.IsExposed("occam_canary_issue", OccamToolProfile.Reader));
        Assert.True(OccamToolProfile.IsExposed("occam_canary_verify", OccamToolProfile.Reader));
        Assert.True(OccamToolProfile.IsExposed("occam_canary_issue", OccamToolProfile.Full));
        Assert.True(OccamToolProfile.IsExposed("occam_canary_verify", OccamToolProfile.Full));
        Assert.False(OccamToolProfile.IsExposed("occam_canary_issue", OccamToolProfile.Minimal));
        Assert.False(OccamToolProfile.IsExposed("occam_canary_verify", OccamToolProfile.Minimal));
        Assert.False(OccamToolProfile.IsExposed("occam_canary_issue", OccamToolProfile.Basic));

        Assert.Equal(11, OccamToolProfile.GetExposedToolNames(OccamToolProfile.Reader).Length);
        Assert.Equal(18, OccamMcpServerRegistration.OccamToolNames.Length);
        Assert.Contains("occam_canary_issue", OccamMcpServerRegistration.OccamToolNames);
        Assert.Contains("occam_canary_verify", OccamMcpServerRegistration.OccamToolNames);
    }

    private static string ExtractSentinelFromMarkdown(string markdown)
    {
        var match = Regex.Match(markdown, @"Sentinel:\s*`([^`]+)`");
        Assert.True(match.Success, "markdown must embed Sentinel: `…`");
        return match.Groups[1].Value;
    }
}
