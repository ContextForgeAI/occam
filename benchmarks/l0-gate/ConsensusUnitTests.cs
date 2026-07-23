using OccamMcp.Core.Consensus;

namespace OccamMcp.L0Gate;

/// <summary>
/// L_CONSENSUS — SI-14 consensus / cloaking-detection core. Pure, deterministic verdicts over
/// injected vantage observations (no network): agreement, root divergence, access-walling, and the
/// block-overlap magnitude. The live 2-backend cross-check stays a smoke-only concern.
/// </summary>
public static class ConsensusUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        static VantageObservation Ok(string label, string backend, string root, string[]? leaves = null) =>
            new(label, backend, true, null, false, "sha256:c-" + root, "sha256:" + root, leaves);
        static VantageObservation Wall(string label, string backend, string code) =>
            new(label, backend, false, code, true, null, null, null);
        static VantageObservation Transient(string label, string backend) =>
            new(label, backend, false, "timeout", false, null, null, null);

        // All witnesses agree → consensus.
        assert("consensus when roots identical",
            ConsensusEvaluator.Evaluate([Ok("http", "http", "AAAA"), Ok("browser", "browser", "AAAA")]).Verdict
                == ConsensusEvaluator.Consensus);

        // Roots differ → divergent (cloaking / personalization).
        var divergent = ConsensusEvaluator.Evaluate([Ok("http", "http", "AAAA"), Ok("browser", "browser", "BBBB")]);
        assert("divergent when roots differ", divergent.Verdict == ConsensusEvaluator.Divergent);
        assert("divergent emits one pair with rootsMatch=false",
            divergent.Pairs.Count == 1 && !divergent.Pairs[0].RootsMatch);

        // One witness walled, another saw content → access_divergent (strongest cloaking signal).
        assert("access_divergent when one witness walled",
            ConsensusEvaluator.Evaluate([Ok("http", "http", "AAAA"), Wall("browser", "browser", "captcha_or_challenge")]).Verdict
                == ConsensusEvaluator.AccessDivergent);

        // Fewer than two usable witnesses → inconclusive.
        assert("inconclusive with a single witness",
            ConsensusEvaluator.Evaluate([Ok("http", "http", "AAAA")]).Verdict == ConsensusEvaluator.Inconclusive);
        assert("inconclusive when the other witness transient-failed",
            ConsensusEvaluator.Evaluate([Ok("http", "http", "AAAA"), Transient("browser", "browser")]).Verdict
                == ConsensusEvaluator.Inconclusive);

        // Order independence.
        assert("verdict is order-independent",
            ConsensusEvaluator.Evaluate([Ok("browser", "browser", "BBBB"), Ok("http", "http", "AAAA")]).Verdict
                == ConsensusEvaluator.Divergent);

        // Block-overlap magnitude: 2 of 3 leaves shared → common=2, total=4 (union of {a,b,c} & {a,b,d}).
        var overlap = ConsensusEvaluator.Evaluate([
            Ok("http", "http", "AAAA", ["a", "b", "c"]),
            Ok("browser", "browser", "BBBB", ["a", "b", "d"])]);
        assert("block overlap counts common + union total",
            overlap.Pairs.Count == 1 && overlap.Pairs[0].BlocksCommon == 2 && overlap.Pairs[0].BlocksTotal == 4);

        // A walled witness alongside divergent content still reports access_divergent (wall takes priority).
        assert("access_divergent takes priority over divergent",
            ConsensusEvaluator.Evaluate([
                Ok("http", "http", "AAAA"),
                Ok("browser", "browser", "BBBB"),
                Wall("http+session", "http", "requires_login")]).Verdict
                == ConsensusEvaluator.AccessDivergent);

        Console.WriteLine("L_CONSENSUS_OK");
    }
}
