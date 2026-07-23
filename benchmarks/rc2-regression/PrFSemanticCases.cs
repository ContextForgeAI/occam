using OccamMcp.Core.Claims;
using OccamMcp.Core.Compile;
using OccamMcp.Core.Knowledge;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Semantics;
using OccamMcp.Core.Tools;

namespace OccamMcp.Rc2Regression;

internal static class PrFSemanticCases
{
    public static void Run(TestHarness test, string fixtureRoot)
    {
        AttemptDimensions(test);
        CompletenessMapping(test, fixtureRoot);
        ClaimRetrievalVersusVerdict(test);
        LegacyAliases(test);
    }

    private static void AttemptDimensions(TestHarness test)
    {
        var fields = typeof(TranscodeAttempt).GetProperties().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        test.Check("SEMANTIC", "backend attempt must distinguish transport from usability",
            fields.Contains("TransportOk") && fields.Contains("Usable"),
            $"fields={string.Join(',', fields.Order())}");

        var transportOnly = TranscodeAttempt.Create(
            "node_readability_turndown",
            transportOk: true,
            latencyMs: 14,
            usable: false,
            failureCode: "thin_extract");
        test.Check("SEMANTIC", "transport success can coexist with unusable extract",
            transportOnly.TransportOk && transportOnly.Ok && !transportOnly.Usable && transportOnly.FailureCode == "thin_extract",
            $"transportOk={transportOnly.TransportOk}; ok={transportOnly.Ok}; usable={transportOnly.Usable}; failure={transportOnly.FailureCode}");

        var recovered = TranscodeAttempt.Create(
            "playwright",
            transportOk: true,
            latencyMs: 40,
            usable: true,
            escalationReason: "thin_extract");
        test.Check("SEMANTIC", "browser recovery preserves prior escalation reason without rewriting transport",
            recovered.TransportOk && recovered.Usable && recovered.EscalationReason == "thin_extract" && recovered.Ok,
            $"usable={recovered.Usable}; escalation={recovered.EscalationReason}");

        var recoveryJson = System.Text.Json.JsonSerializer.Serialize(
            new OccamTranscodeRecoveryInfo(
                "http",
                Ok: true,
                LatencyMs: 12,
                TransportOk: true,
                Usable: false,
                FailureCode: "thin_extract"),
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            });
        test.Check("SEMANTIC", "recovery JSON publishes transportOk and usable beside legacy ok",
            recoveryJson.Contains("\"transportOk\":true", StringComparison.Ordinal)
            && recoveryJson.Contains("\"usable\":false", StringComparison.Ordinal)
            && recoveryJson.Contains("\"ok\":true", StringComparison.Ordinal),
            recoveryJson);
    }

    private static void CompletenessMapping(TestHarness test, string fixtureRoot)
    {
        var source = File.ReadAllText(Path.Combine(fixtureRoot, "budget-answer.md"));
        var constrained = TokenBudget.Apply(source, 128, "simple requests");
        var partial = MaterializationAssessmentEvaluator.Evaluate(
            source, constrained.Text, "simple requests", null, truncated: true);
        var incomplete = MaterializationAssessmentEvaluator.Evaluate(
            source, "## Simple requests", "simple requests", null, truncated: true);

        var focus = SemanticOutcomeMapper.MapFocus(partial, "simple requests", null);
        var completeness = SemanticOutcomeMapper.MapCompleteness(partial);
        test.Check("SEMANTIC", "retained constrained focus maps to partial completeness",
            focus.Status is "hit" or "weak"
            && completeness?.Status == "partial"
            && completeness.IncompleteReason == "context_truncated",
            $"focus={focus.Status}; completeness={completeness?.Status}; reason={completeness?.IncompleteReason}");

        var lost = SemanticOutcomeMapper.MapCompleteness(incomplete);
        test.Check("SEMANTIC", "focus body loss maps to incomplete without implying extraction failure",
            lost?.Status == "incomplete"
            && lost.IncompleteReason == "focus_body_truncated"
            && (lost.SuggestedMinTokens ?? 0) >= 128,
            $"completeness={lost?.Status}; reason={lost?.IncompleteReason}; suggested={lost?.SuggestedMinTokens}");

        var notRequested = SemanticOutcomeMapper.MapFocus(null, null, null);
        test.Check("SEMANTIC", "absent focus request is not_requested",
            notRequested.Status == "not_requested",
            $"focus={notRequested.Status}");
    }

    private static void ClaimRetrievalVersusVerdict(TestHarness test)
    {
        var props = typeof(OccamClaimCheckSuccessResponse).GetProperties()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        test.Check("SEMANTIC", "claim_check publishes retrieved beside legacy found",
            props.Contains("Found") && props.Contains("Retrieved") && props.Contains("Verdict"),
            $"fields={string.Join(',', props.Order())}");

        var response = new OccamClaimCheckSuccessResponse(
            Ok: true,
            Url: "https://example.org/contradicted",
            Claim: "The sky is green",
            Found: true,
            Retrieved: true,
            Verdict: SemanticVerdict.NotEvaluated,
            BlockMerkleRoot: null,
            KeyId: null,
            Matches: [],
            Receipt: null,
            Timestamp: "2026-01-01T00:00:00Z");
        test.Check("SEMANTIC", "retrieved true does not imply semantic support",
            response.Found
            && response.Retrieved
            && response.Verdict == SemanticVerdict.NotEvaluated,
            $"found={response.Found}; retrieved={response.Retrieved}; verdict={response.Verdict}");
    }

    private static void LegacyAliases(TestHarness test)
    {
        var attempt = new TranscodeAttempt("http", ok: true, latencyMs: 5);
        test.Check("SEMANTIC", "legacy Ok remains transport alias when only three-arg constructor is used",
            attempt.Ok && attempt.TransportOk && attempt.Usable,
            $"ok={attempt.Ok}; transportOk={attempt.TransportOk}; usable={attempt.Usable}");

        var successProps = typeof(OccamTranscodeSuccessResponse).GetProperties()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        test.Check("SEMANTIC", "public success envelope keeps ok and confidence while adding dimensions",
            successProps.Contains("Ok")
            && successProps.Contains("Confidence")
            && successProps.Contains("Access")
            && successProps.Contains("Focus")
            && successProps.Contains("Completeness")
            && successProps.Contains("Verdict"),
            $"fields={string.Join(',', successProps.Order())}");
    }
}
