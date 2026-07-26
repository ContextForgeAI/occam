// P6-06 runtime reproduction harness (audit-only, docs-audit/ scope).
// Each case targets one ENGINEERING-FINDINGS entry and prints machine-greppable lines:
//   CASE <id> | <label> | OBSERVED=<value> | <PASS|FAIL>
// PASS means "the finding reproduced as described"; FAIL means it did not.
using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using OccamMcp.Core.Caching;
using OccamMcp.Core.Compile;
using OccamMcp.Core.Composition;
using OccamMcp.Core.Playbooks;
using OccamMcp.Core.Receipts;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Watch;
using OccamMcp.Core.Workers;

var only = args.FirstOrDefault(a => a.StartsWith("--case=", StringComparison.Ordinal))?["--case=".Length..];
var failures = 0;

void Result(string caseId, string label, string observed, bool reproduced)
{
    Console.WriteLine($"CASE {caseId} | {label} | OBSERVED={observed} | {(reproduced ? "PASS" : "FAIL")}");
    if (!reproduced)
    {
        failures++;
    }
}

bool Selected(string id) => only is null || only == id;

if (Selected("EF-045"))
{
    var opts = new OccamTranscodeOptions { PlaybookPolicy = "off" };
    const string urlA = "https://example.com/guide#installation";
    const string urlB = "https://example.com/guide#uninstall";

    var focusA = FocusIntent.FromUrl(urlA);
    var focusB = FocusIntent.FromUrl(urlB);
    Result("EF-045", "FocusIntent fragments differ",
        $"{focusA.Fragment}|{focusB.Fragment}", focusA.Fragment != focusB.Fragment);

    var ckA = TranscodeCacheKey.Compute(urlA, "http", opts);
    var ckB = TranscodeCacheKey.Compute(urlB, "http", opts);
    // Post EF-045 FIX_NOW: fragments must produce distinct cache keys (repro formerly asserted collision).
    Result("EF-045", "TranscodeCacheKey distinct across fragments",
        ckA[..16] + "!=" + ckB[..16], ckA != ckB);

    var mkA = MaterializationKey.Compute(urlA, "http", opts);
    var mkB = MaterializationKey.Compute(urlB, "http", opts);
    Result("EF-045", "MaterializationKey distinct across fragments",
        mkA[7..23] + "!=" + mkB[7..23], mkA != mkB);

    // Control: a declared focus_query DOES split the key, so the collision is fragment-specific.
    var ckFocus = TranscodeCacheKey.Compute("https://example.com/guide", "http", opts with { FocusQuery = "installation" });
    var ckFocus2 = TranscodeCacheKey.Compute("https://example.com/guide", "http", opts with { FocusQuery = "uninstall" });
    Result("EF-045", "control: focus_query does split the key",
        ckFocus == ckFocus2 ? "collide" : "distinct", ckFocus != ckFocus2);
}

if (Selected("EF-058"))
{
    var signer = ReceiptSigner.CreateEphemeral();
    var pub = signer.ExportPublicKeyPem();
    const string body = """
    {"id":"example.com","version":1,"selectors":{"content":"main article"}}
    """;

    var signed = PlaybookSignature.BuildSignedJson(body, score: 41, passesGate: false, noise: 0.42, signer);
    var baseline = PlaybookSignature.Inspect(signed, signer.KeyId, pub);
    Result("EF-058", "baseline signed playbook inspects verified",
        $"{baseline.Status},score={baseline.Score},passesGate={baseline.PassesGate}",
        baseline is { Status: "verified", Score: 41, PassesGate: false });

    // (a) Forge the quality claim inside the unsigned provenance block.
    var forged = JsonNode.Parse(signed)!;
    forged["provenance"]!["verify"]!["score"] = 100;
    forged["provenance"]!["verify"]!["passesGate"] = true;
    forged["provenance"]!["verify"]!["noiseLeakage"] = 0.0;
    var forgedJson = forged.ToJsonString();
    var stillValid = PlaybookSignature.Verify(forgedJson, pub);
    var forgedStatus = PlaybookSignature.Inspect(forgedJson, signer.KeyId, pub);
    Result("EF-058", "forged verify{} still passes signature Verify",
        stillValid.ToString(), stillValid);
    Result("EF-058", "forged verify{} inspects as verified with forged score",
        $"{forgedStatus.Status},score={forgedStatus.Score},passesGate={forgedStatus.PassesGate}",
        forgedStatus is { Status: "verified", Score: 100, PassesGate: true });

    // (b) Forge signedAt (unsigned).
    var reDated = JsonNode.Parse(signed)!;
    reDated["provenance"]!["signedAt"] = "2099-01-01T00:00:00Z";
    Result("EF-058", "forged signedAt still passes signature Verify",
        PlaybookSignature.Verify(reDated.ToJsonString(), pub).ToString(),
        PlaybookSignature.Verify(reDated.ToJsonString(), pub));

    // (c) Swap the claimed keyId → Inspect downgrades to unknown_key, not invalid.
    var reKeyed = JsonNode.Parse(signed)!;
    reKeyed["provenance"]!["keyId"] = "0000000000000000";
    var reKeyedStatus = PlaybookSignature.Inspect(reKeyed.ToJsonString(), signer.KeyId, pub);
    Result("EF-058", "keyId swap yields unknown_key (not invalid)",
        $"{reKeyedStatus.Status},keyId={reKeyedStatus.KeyId},score={reKeyedStatus.Score}",
        reKeyedStatus.Status == "unknown_key");

    // (d) Control: tampering the SIGNED body is caught.
    var bodyTampered = JsonNode.Parse(signed)!;
    bodyTampered["selectors"]!["content"] = "body";
    var bodyStatus = PlaybookSignature.Inspect(bodyTampered.ToJsonString(), signer.KeyId, pub);
    Result("EF-058", "control: signed-body tamper is caught as invalid",
        bodyStatus.Status, bodyStatus.Status == "invalid");
}

if (Selected("EF-059"))
{
    var u0 = WatchHistoryChain.Append([], WatchHistoryEntry.EventFirstSeen, "sha256:aa", null, null, "2026-01-01T00:00:00Z", signer: null);
    var u1 = WatchHistoryChain.Append([u0], WatchHistoryEntry.EventChanged, "sha256:bb", null, 12, "2026-01-02T00:00:00Z", signer: null);
    var u2 = WatchHistoryChain.Append([u0, u1], WatchHistoryEntry.EventChanged, "sha256:cc", null, 7, "2026-01-03T00:00:00Z", signer: null);
    var chain = new[] { u0, u1, u2 };

    var unrelatedPub = ReceiptSigner.CreateEphemeral().ExportPublicKeyPem();
    var verdict = WatchHistoryChain.Verify(chain, unrelatedPub);
    Result("EF-059", "wholly unsigned chain verifies against an unrelated key",
        $"entries={chain.Length},signed={chain.Count(e => e.Sig is not null)},verify={verdict}",
        verdict && chain.All(e => e.Sig is null));

    // The chain link is still enforced, so this is link integrity — not signature verification.
    var broken = new[] { u0, u1 with { PrevEntryHash = "sha256:00" } };
    Result("EF-059", "control: broken link still fails",
        WatchHistoryChain.Verify(broken, unrelatedPub).ToString(),
        !WatchHistoryChain.Verify(broken, unrelatedPub));

    // Anyone can mint a fresh, self-consistent unsigned chain with arbitrary content hashes.
    var f0 = WatchHistoryChain.Append([], WatchHistoryEntry.EventFirstSeen, "sha256:deadbeef", null, null, "1999-01-01T00:00:00Z", signer: null);
    var f1 = WatchHistoryChain.Append([f0], WatchHistoryEntry.EventChanged, "sha256:feedface", null, 1, "1999-01-02T00:00:00Z", signer: null);
    Result("EF-059", "fabricated unsigned chain also verifies",
        WatchHistoryChain.Verify([f0, f1], unrelatedPub).ToString(),
        WatchHistoryChain.Verify([f0, f1], unrelatedPub));

    // Hand-built camelCase JSON (the Core serializer contexts are internal); this is the exact
    // shape `occam verify --mode history --input` parses.
    var arr = new JsonArray();
    foreach (var e in chain)
    {
        var node = new JsonObject
        {
            ["seq"] = e.Seq,
            ["observedAt"] = e.ObservedAt,
            ["event"] = e.Event,
            ["contentHash"] = e.ContentHash,
            ["contentDeltaTokens"] = e.ContentDeltaTokens,
            ["prevEntryHash"] = e.PrevEntryHash,
            ["keyId"] = e.KeyId,
            ["alg"] = e.Alg,
            ["sig"] = e.Sig,
        };
        arr.Add(node);
    }

    var outPath = Path.Combine(AppContext.BaseDirectory, "ef059-chain.json");
    File.WriteAllText(outPath, arr.ToJsonString());
    Console.WriteLine($"NOTE EF-059 | unsigned chain written for CLI verify: {outPath}");
    var pubPath = Path.Combine(AppContext.BaseDirectory, "ef059-unrelated.pem");
    File.WriteAllText(pubPath, unrelatedPub);
    Console.WriteLine($"NOTE EF-059 | unrelated public key written: {pubPath}");
}

if (Selected("EF-060"))
{
    (string Text, string? SourceSelector)[] three =
    [
        ("alpha", "#a"),
        ("beta", "#b"),
        ("gamma", "#c"),
    ];
    (string Text, string? SourceSelector)[] fourDupLast =
    [
        ("alpha", "#a"),
        ("beta", "#b"),
        ("gamma", "#c"),
        ("gamma", "#c"),
    ];

    var r3 = MerkleTree.Root(three);
    var r4 = MerkleTree.Root(fourDupLast);
    Result("EF-060", "3-leaf root == 4-leaf duplicate-last root",
        $"{r3?[7..23]}=={r4?[7..23]}", r3 == r4 && r3 is not null);

    // Second-preimage shape: leaf count is not bound by the root, so "how many blocks" is unsigned.
    Result("EF-060", "leaf count is not recoverable from the root",
        $"count3={three.Length},count4={fourDupLast.Length},sameRoot={r3 == r4}", r3 == r4);

    // The same collision reaches the leaf-hash API used by receipts / live verify.
    var lh3 = MerkleTree.LeafHashesHex(three);
    var lh4 = MerkleTree.LeafHashesHex(fourDupLast);
    var rl3 = MerkleTree.RootFromLeafHashes(lh3);
    var rl4 = MerkleTree.RootFromLeafHashes(lh4);
    Result("EF-060", "RootFromLeafHashes collides identically",
        $"{rl3?[7..23]}=={rl4?[7..23]}", rl3 == rl4);

    // A membership proof built over the padded leaf set validates against the shorter set's root.
    var proof = MerkleTree.Proof(lh4, 3);
    Result("EF-060", "proof for the phantom 4th leaf verifies against the 3-leaf root",
        MerkleTree.VerifyProof(lh4[3], proof, r3!).ToString(),
        MerkleTree.VerifyProof(lh4[3], proof, r3!));

    // Control: a genuinely different leaf set gives a different root.
    var rDifferent = MerkleTree.Root([("alpha", "#a"), ("beta", "#b"), ("delta", "#c")]);
    Result("EF-060", "control: different content -> different root",
        rDifferent == r3 ? "collide" : "distinct", rDifferent != r3);
}

if (Selected("EF-041"))
{
    await RunEf041();
}

Console.WriteLine(failures == 0 ? "P6_REPRO_ALL_REPRODUCED" : $"P6_REPRO_FAILURES={failures}");
return failures == 0 ? 0 : 1;

async Task RunEf041()
{
    var script = Path.Combine(AppContext.BaseDirectory, "fake-daemon.mjs");
    if (!File.Exists(script))
    {
        // Fall back to the source location when the harness runs from the project directory.
        script = Path.Combine(Directory.GetCurrentDirectory(), "fake-daemon.mjs");
    }

    if (!File.Exists(script))
    {
        Console.WriteLine("CASE EF-041 | fake daemon script missing | OBSERVED=blocked | BLOCKED");
        failures++;
        return;
    }

    var pidFile = Path.Combine(Path.GetTempPath(), $"p6-ef041-{Guid.NewGuid():N}.pid");
    Environment.SetEnvironmentVariable("OCCAM_BROWSER_DAEMON_SCRIPT", script);
    Environment.SetEnvironmentVariable("P6_FAKE_DAEMON_PIDFILE", pidFile);
    Environment.SetEnvironmentVariable(BrowserPoolSettings.PoolSizeVar, "1");
    Environment.SetEnvironmentVariable(BrowserPoolSettings.BasePortVar, "39311");
    Environment.SetEnvironmentVariable(BrowserPoolSettings.DaemonPortVar, null);

    var paths = WorkerPaths.Resolve();

    // Session 1 DI graph — as built per WebSocket / Remote session by AddOccamMcpServer().
    var p1 = new ServiceCollection().AddOccamCore().BuildServiceProvider();
    var m1 = p1.GetRequiredService<IBrowserPoolManager>();
    var started = await m1.TryEnsureMinimumHealthyAsync(paths);
    Result("EF-041", "session 1 pool slot healthy", $"enabled={m1.IsEnabled},started={started}", started);
    if (!started)
    {
        Console.WriteLine("NOTE EF-041 | slot never became healthy — remaining EF-041 checks are inconclusive");
        return;
    }

    var pid = int.Parse(File.ReadAllText(pidFile).Trim());
    var aliveBefore = IsAlive(pid);
    var healthyBefore = await m1.GetHealthySlotsAsync();
    Result("EF-041", "session 1 daemon alive before second DI build",
        $"pid={pid},alive={aliveBefore},healthySlots={healthyBefore}", aliveBefore && healthyBefore == 1);

    // Session 2 DI graph — a second WebSocket/Remote connection resolving IBrowserPoolManager.
    var p2 = new ServiceCollection().AddOccamCore().BuildServiceProvider();
    var m2 = p2.GetRequiredService<IBrowserPoolManager>();
    Result("EF-041", "second DI build yields a different manager instance",
        ReferenceEquals(m1, m2) ? "same" : "different", !ReferenceEquals(m1, m2));

    // InstallShared ran inside the session-2 factory and called StopAll() on session 1's manager.
    await Task.Delay(400);
    var aliveAfter = IsAlive(pid);
    var healthyAfterSession1 = await m1.GetHealthySlotsAsync();
    var healthyAfterSession2 = await m2.GetHealthySlotsAsync();
    Result("EF-041", "session 1 daemon killed by session 2 DI build",
        $"pid={pid},alive={aliveAfter}", !aliveAfter);
    Result("EF-041", "session 1 pool reports no healthy slot after session 2 DI build",
        $"s1Healthy={healthyAfterSession1},s2Healthy={healthyAfterSession2}",
        healthyAfterSession1 == 0);
    Result("EF-041", "process-wide Shared now points at the session 2 manager",
        ReferenceEquals(BrowserPoolManager.Shared, m2) ? "session2" : "other",
        ReferenceEquals(BrowserPoolManager.Shared, m2));

    m2.StopAll();
    try
    {
        File.Delete(pidFile);
    }
    catch (IOException)
    {
    }
}

static bool IsAlive(int pid)
{
    try
    {
        using var p = Process.GetProcessById(pid);
        return !p.HasExited;
    }
    catch (ArgumentException)
    {
        return false;
    }
}
