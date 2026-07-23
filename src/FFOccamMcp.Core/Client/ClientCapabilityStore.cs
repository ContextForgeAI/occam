using OccamMcp.Core.Configuration;

namespace OccamMcp.Core.Client;

/// <summary>
/// Process-scoped LLM client budget: context window → default extract output size.
/// MCP does not expose model context size; the agent (or operator via env) must declare it once.
/// Subsequent <c>occam_transcode</c>/<c>occam_digest</c> calls that omit <c>max_tokens</c> inherit
/// <see cref="OutputBudgetTokens"/>.
/// </summary>
public sealed class ClientCapabilityStore
{
    public const int MinContextTokens = 1_024;
    public const int MaxContextTokens = 2_000_000;
    public const int MinOutputBudget = 512;
    public const int MaxOutputBudget = 16_384;
    public const double OutputFractionOfContext = 0.20;

    private readonly object _gate = new();
    private ClientCapabilitySnapshot _snapshot;

    public ClientCapabilityStore()
    {
        _snapshot = ReadEnvBootstrap();
    }

    public ClientCapabilitySnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return _snapshot;
            }
        }
    }

    /// <summary>Declare or refresh the client context window. Returns the applied snapshot.</summary>
    public ClientCapabilitySnapshot Configure(int contextTokens, string? modelId = null, string source = "tool")
    {
        if (contextTokens < MinContextTokens || contextTokens > MaxContextTokens)
        {
            throw new ArgumentOutOfRangeException(
                nameof(contextTokens),
                $"context_tokens must be in [{MinContextTokens}..{MaxContextTokens}].");
        }

        var snap = BuildSnapshot(contextTokens, modelId, source);
        lock (_gate)
        {
            _snapshot = snap;
            return _snapshot;
        }
    }

    /// <summary>Clear tool/session override; re-read env bootstrap (may be empty).</summary>
    public ClientCapabilitySnapshot Clear()
    {
        lock (_gate)
        {
            _snapshot = ReadEnvBootstrap();
            return _snapshot;
        }
    }

    /// <summary>
    /// When the caller omitted <paramref name="explicitMaxTokens"/> and a budget is known,
    /// return the ambient output budget; otherwise return the explicit value (or null).
    /// </summary>
    public int? ResolveMaxTokens(int? explicitMaxTokens)
    {
        if (explicitMaxTokens is not null)
        {
            return explicitMaxTokens;
        }

        var snap = Current;
        return snap.Configured ? snap.OutputBudgetTokens : null;
    }

    public static int ComputeOutputBudget(int contextTokens)
    {
        var raw = (int)Math.Round(contextTokens * OutputFractionOfContext);
        return Math.Clamp(raw, MinOutputBudget, MaxOutputBudget);
    }

    /// <summary>Suggested <c>OCCAM_PROFILE</c> for this context size (advisory only).</summary>
    public static string SuggestProfile(int contextTokens) =>
        contextTokens < 8_192 ? "reader"
        : contextTokens < 32_768 ? "researcher"
        : "full";

    private static ClientCapabilitySnapshot ReadEnvBootstrap()
    {
        var raw = OccamEnvironment.Get("OCCAM_CLIENT_CONTEXT_TOKENS");
        if (string.IsNullOrWhiteSpace(raw) || !int.TryParse(raw.Trim(), out var tokens))
        {
            return ClientCapabilitySnapshot.Empty;
        }

        if (tokens < MinContextTokens || tokens > MaxContextTokens)
        {
            Console.Error.WriteLine(
                $"[occam.config] OCCAM_CLIENT_CONTEXT_TOKENS={tokens} out of range [{MinContextTokens}..{MaxContextTokens}] — ignored.");
            return ClientCapabilitySnapshot.Empty;
        }

        return BuildSnapshot(tokens, modelId: OccamEnvironment.Get("OCCAM_CLIENT_MODEL_ID"), source: "env");
    }

    private static ClientCapabilitySnapshot BuildSnapshot(int contextTokens, string? modelId, string source) =>
        new(
            Configured: true,
            ContextTokens: contextTokens,
            OutputBudgetTokens: ComputeOutputBudget(contextTokens),
            SuggestedProfile: SuggestProfile(contextTokens),
            ModelId: string.IsNullOrWhiteSpace(modelId) ? null : modelId.Trim(),
            Source: source);
}

public sealed record ClientCapabilitySnapshot(
    bool Configured,
    int? ContextTokens,
    int? OutputBudgetTokens,
    string? SuggestedProfile,
    string? ModelId,
    string? Source)
{
    public static ClientCapabilitySnapshot Empty { get; } = new(false, null, null, null, null, null);
}
