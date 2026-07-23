namespace OccamMcp.Core.Agent;

public sealed record FailureAgentHintsInfo(ProbeDecision[] Decisions);

public static class FailureAgentHints
{
    public static FailureAgentHintsInfo? ForCode(string failureCode)
    {
        var decisions = TranscodeAgentDecisions.ForFailure(failureCode);
        return decisions.Length > 0 ? new FailureAgentHintsInfo(decisions) : null;
    }
}
