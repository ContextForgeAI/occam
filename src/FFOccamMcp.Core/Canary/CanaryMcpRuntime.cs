using System.Text;
using System.Text.RegularExpressions;
using OccamMcp.Core.Configuration;
using OccamMcp.Core.Routing;

namespace OccamMcp.Core.Canary;

/// <summary>
/// Process-wide canary backing for MCP tools: one <see cref="CanaryService"/>, optional loopback
/// probe host, and an in-process materializer so <c>occam_transcode</c> can read the issued URL
/// without crossing the private-URL SSRF boundary.
/// </summary>
public sealed class CanaryMcpRuntime : IAsyncDisposable
{
    private static readonly Regex ProbePath = new(
        @"^/probe/canary/(?<session>[A-Za-z0-9._\-]+)/?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly object _gate = new();
    private readonly CanaryService _service;
    private readonly bool _ownsService;
    private CanaryProbeServerHost? _host;
    private Task<string>? _startTask;
    private bool _disposed;

    /// <summary>Creates a runtime over environment-tuned options.</summary>
    public CanaryMcpRuntime()
        : this(CanaryOptions.ReadFromEnvironment())
    {
    }

    /// <summary>Creates a runtime over caller-supplied options (tests).</summary>
    public CanaryMcpRuntime(CanaryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _service = new CanaryService(options);
        _ownsService = true;
    }

    /// <summary>Creates a runtime over a fixed service (tests; service ownership stays with the caller).</summary>
    public CanaryMcpRuntime(CanaryService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _ownsService = false;
    }

    /// <summary>Shared canary service for this MCP process.</summary>
    public CanaryService Service => _service;

    /// <summary>
    /// Issues a canary session for an agent. Returns the probe URL and metadata — never the sentinel.
    /// </summary>
    /// <param name="sessionId">Optional caller session; generated when null/empty.</param>
    /// <param name="ttlSeconds">Optional advisory TTL for <c>expiresAt</c>; crypto still uses buckets.</param>
    /// <param name="cancellationToken">Cancels probe-host startup.</param>
    public async Task<CanaryMcpIssueResult> IssueForAgentAsync(
        string? sessionId,
        int? ttlSeconds,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var id = string.IsNullOrWhiteSpace(sessionId)
            ? CanaryService.NewSessionId()
            : sessionId.Trim();

        if (!CanarySentinel.IsValidSessionId(id, _service.Options.MaxSessionIdLength))
        {
            throw new ArgumentException(
                $"Session id must be 1..{_service.Options.MaxSessionIdLength} chars of [A-Za-z0-9._-].",
                nameof(sessionId));
        }

        var listenUrl = await EnsureProbeHostAsync(cancellationToken).ConfigureAwait(false);
        var issue = _service.Issue(id, clientIdentifier: "mcp", userAgent: "occam-mcp-canary-issue");
        var expiresAt = issue.ExpiresAt;
        if (ttlSeconds is > 0)
        {
            var advisory = issue.IssuedAt.AddSeconds(Math.Clamp(ttlSeconds.Value, 30, 86_400));
            if (advisory < expiresAt)
            {
                expiresAt = advisory;
            }
        }

        return new CanaryMcpIssueResult(
            Url: $"{listenUrl}/probe/canary/{Uri.EscapeDataString(id)}",
            SessionId: id,
            ExpiresAt: expiresAt,
            Bucket: issue.Bucket);
    }

    /// <summary>Verifies a sentinel claim for a session.</summary>
    public CanaryVerification VerifyForAgent(string sessionId, string sentinel)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _service.Verify(sessionId, sentinel);
    }

    /// <summary>
    /// When <paramref name="url"/> is this process's canary probe document, issues (or re-issues)
    /// the sentinel and returns an in-process transcode success so agents can read via
    /// <c>occam_transcode</c> without outbound loopback SSRF failures.
    /// </summary>
    public bool TryMaterializeProbe(string url, out TranscodeOutcome outcome)
    {
        outcome = null!;
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!TryMatchOwnProbeUrl(url, out var sessionId))
        {
            return false;
        }

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            var issue = _service.Issue(sessionId, clientIdentifier: "mcp-transcode", userAgent: "occam-mcp-canary-read");
            var markdown = RenderMarkdown(issue);
            var latency = (int)System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            outcome = new TranscodeOutcome(
                Ok: true,
                Markdown: markdown,
                FinalUrl: url,
                Backend: "canary",
                FailureCode: null,
                Message: null,
                LatencyMs: latency,
                TokensEstimated: Math.Max(1, markdown.Length / 4),
                StatusCode: 200);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>True when the URL targets this runtime's probe document path on the live listen URL.</summary>
    public bool TryMatchOwnProbeUrl(string url, out string sessionId)
    {
        sessionId = "";
        if (_host is null || string.IsNullOrWhiteSpace(_host.ListenUrl))
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!Uri.TryCreate(_host.ListenUrl, UriKind.Absolute, out var listen))
        {
            return false;
        }

        if (!string.Equals(uri.Host, listen.Host, StringComparison.OrdinalIgnoreCase)
            || uri.Port != listen.Port)
        {
            return false;
        }

        var match = ProbePath.Match(uri.AbsolutePath);
        if (!match.Success)
        {
            return false;
        }

        sessionId = Uri.UnescapeDataString(match.Groups["session"].Value);
        return CanarySentinel.IsValidSessionId(sessionId, _service.Options.MaxSessionIdLength);
    }

    private Task<string> EnsureProbeHostAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_host is { ListenUrl.Length: > 0 })
            {
                return Task.FromResult(_host.ListenUrl);
            }

            _startTask ??= StartHostUnlockedAsync();
            return AwaitStartAsync(_startTask, cancellationToken);
        }
    }

    private static Task<string> AwaitStartAsync(Task<string> start, CancellationToken cancellationToken) =>
        start.WaitAsync(cancellationToken);

    private async Task<string> StartHostUnlockedAsync()
    {
        var port = OccamEnvironment.GetInt("OCCAM_CANARY_MCP_PORT", defaultValue: 0, min: 0, max: 65_535);
        var host = new CanaryProbeServerHost(_service, port: port);
        var listenUrl = await host.StartAsync().ConfigureAwait(false);
        lock (_gate)
        {
            _host = host;
        }

        return listenUrl;
    }

    private static string RenderMarkdown(CanaryIssue issue)
    {
        var sb = new StringBuilder(512);
        sb.AppendLine("# Occam proof-of-read probe");
        sb.AppendLine();
        sb.AppendLine(
            "This document exists to test whether an agent actually read it. It carries a short-lived sentinel derived from a server-side secret and the current time bucket.");
        sb.AppendLine();
        sb.Append("Sentinel: `").Append(issue.Sentinel).AppendLine("`");
        sb.AppendLine();
        sb.Append("Session: `").Append(issue.SessionId).Append("`. Issued at ")
            .Append(issue.IssuedAt.ToString("O")).Append("; stops counting as a fresh read after ")
            .Append(issue.ExpiresAt.ToString("O")).AppendLine(".");
        sb.AppendLine();
        sb.AppendLine(
            "Report the sentinel verbatim to prove this page entered your context. Do not guess: an unmatched value is recorded as a hallucination, not as a near miss.");
        return sb.ToString();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CanaryProbeServerHost? host;
        lock (_gate)
        {
            host = _host;
            _host = null;
        }

        if (host is not null)
        {
            await host.DisposeAsync().ConfigureAwait(false);
        }

        if (_ownsService)
        {
            _service.Dispose();
        }
    }
}

/// <summary>Agent-facing issue payload (no sentinel).</summary>
/// <param name="Url">Probe document URL the agent must fetch.</param>
/// <param name="SessionId">Session bound to the sentinel.</param>
/// <param name="ExpiresAt">When a fresh read stops verifying as <c>READ_VERIFIED</c>.</param>
/// <param name="Bucket">Time bucket the sentinel was derived for.</param>
public readonly record struct CanaryMcpIssueResult(
    string Url,
    string SessionId,
    DateTimeOffset ExpiresAt,
    long Bucket);
