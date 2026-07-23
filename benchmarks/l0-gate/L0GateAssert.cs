namespace OccamMcp.L0Gate;

internal sealed class L0GateAssert
{
    private readonly List<string> _failures = [];

    public void Record(string name, bool condition)
    {
        if (condition)
        {
            Console.WriteLine($"PASS: {name}");
        }
        else
        {
            Console.Error.WriteLine($"FAIL: {name}");
            _failures.Add(name);
        }
    }

    public IReadOnlyList<string> Failures => _failures;

    public int Finish(bool smokeOnly, bool smokeFast, bool l1bRan, bool l1FailureRan = false, bool l2DigestRan = false, bool l2MapRan = false, bool l2SessionRan = false, bool l2TransportRan = false, bool l2EgressRan = false, bool l2MediaRefsRan = false, bool l3HealLearnRan = false, bool l4GenomeRan = false, bool l5BatchRan = false, bool l6BrowserPoolRan = false, bool l7ResourceSafetyRan = false)
    {
        if (_failures.Count == 0)
        {
            if (!smokeOnly)
            {
                Console.WriteLine("L0_GATE_UTF8_OK");
                Console.WriteLine("L1A_TOKEN_OK");
            }

            if (l1bRan)
            {
                Console.WriteLine("L1B_PROBE_OK");
            }

            if (l1FailureRan)
            {
                Console.WriteLine("L1_FAILURE_TAXONOMY_OK");
            }

            if (l2DigestRan)
            {
                Console.WriteLine("L2_DIGEST_OK");
            }

            if (l2MapRan)
            {
                Console.WriteLine("L2_MAP_OK");
            }

            if (l2SessionRan)
            {
                Console.WriteLine("L2_SESSION_OK");
            }

            if (l2TransportRan)
            {
                Console.WriteLine("L2_TRANSPORT_OK");
            }

            if (l2EgressRan)
            {
                Console.WriteLine("L2_EGRESS_OK");
            }

            if (l2MediaRefsRan)
            {
                Console.WriteLine("L2_MEDIA_REFS_OK");
            }

            if (l3HealLearnRan)
            {
                Console.WriteLine("L3_HEAL_LEARN_OK");
            }

            if (l4GenomeRan)
            {
                Console.WriteLine("L4_GENOME_OK");
            }

            if (l5BatchRan)
            {
                Console.WriteLine("L5_BATCH_OK");
            }

            if (l6BrowserPoolRan)
            {
                Console.WriteLine("L6_BROWSER_POOL_OK");
            }

            if (l7ResourceSafetyRan)
            {
                Console.WriteLine("L7_RESOURCE_SAFETY_OK");
            }

            if (!smokeOnly)
            {
                Console.WriteLine("L8_AGENT_FIRST_OK");
            }

            Console.WriteLine(smokeFast ? "L0_GATE_FAST_OK" : "L0_GATE_OK");
            return 0;
        }

        Console.Error.WriteLine($"L0 gate failed ({_failures.Count}): {string.Join(", ", _failures)}");
        return 1;
    }
}
