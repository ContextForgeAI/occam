# Paper draft — skeleton

**Status:** structure only. The abstract below is written to the intended shape of the paper; every
number in it is a placeholder marked `[TBD]`. **This draft must not be circulated with placeholder
numbers presented as results.**

Blocking prerequisite: no experiment has been run. See
[`methodology.md`](methodology.md) for the design and
[`hypothesis.md`](hypothesis.md) for what would falsify each claim.

---

## Working title

*Proof-of-Read: Cryptographic Verification That Retrieved Content Reached an Agent's Context*

## Abstract (150 words, placeholder figures)

Tool-using language models routinely summarise web pages they may never have received. Retrieval
citations and content hashes verify what a *pipeline* fetched; neither establishes that the fetched
bytes entered the *model's* context before generation. We present a proof-of-read protocol in which
a verifier embeds a short-lived sentinel — an HMAC over a time bucket and a session identifier — in
a probe document and asks the agent to report it. Because the sentinel is unpredictable, absent from
training data, and expires, a correct report is evidence of transit; adjudication yields four
verdicts separating authenticity from provenance and freshness. We specify the protocol, give a
reference implementation verified on three platforms with byte-identical derivation, and evaluate it
on `[TBD: N]` pages across `[TBD: M]` models. Canary-adjudicated reads agree with human fabrication
labels at `[TBD: κ]`, and fabrication falls by `[TBD: Δ]` points under canary-aware prompting.

## 1. Introduction

- The trust gap: fluent output is uncorrelated with content arrival.
- Why the obvious fixes miss: hashes and citations sit upstream of the context window.
- Contribution list:
  1. a protocol specification with an injective MAC framing and a four-state verdict precedence;
  2. a reference implementation, cross-platform verified, with published test vectors;
  3. an empirical evaluation of detection validity and of the deterrence effect. `[TBD]`
- Explicit non-contribution: the cryptographic construction is standard (time-bucketed HMAC, as in
  TOTP). The contribution is the adjudication semantics and the empirical question.

## 2. Threat model and scope

Reproduce PROBE_PROTOCOL.md §7.1–7.2 with the same discipline: what `READ_VERIFIED` proves and,
at equal length, what it does not. The bounded-claim paragraph is load-bearing for the paper's
credibility and must not be softened for the abstract.

## 3. Protocol

- Secret handling and HKDF subkey separation.
- Time buckets and floor semantics.
- Injective framing; why length-prefixing matters for future revisions.
- Truncation: forgery resistance versus echo fidelity (§7.3). Note that echo fidelity is an
  *LLM-specific* design pressure with no analogue in classical canary design — likely the most
  transferable observation in the paper.
- Verdict precedence: authenticity → provenance → freshness.

## 4. Implementation

- Native AOT .NET 10; deterministic derivation; no persisted key.
- Bounded verification work; bounded audit log; rate-limited oracles.
- Cross-platform evidence: identical vectors and identical coverage on macOS arm64, Linux x64,
  Windows x64 (real, see `docs/testing/RESULTS.md`).
- The newline defect as a short, concrete note on why byte-level cross-platform checks differ from
  semantic ones. Small, but it is a genuine finding about verifying portable artefacts.

## 5. Evaluation `[TBD — not run]`

- 5.1 Detection validity (H1a): verdict versus human label agreement.
- 5.2 Echo fidelity by model size and tag length. Must precede 5.3, since it bounds interpretation.
- 5.3 Deterrence (H1b): fabrication on non-probe pages, canary-aware versus naive.
- 5.4 Cost: latency and token overhead of the sentinel instruction.

## 6. Limitations

- Clock dependence; restart invalidation.
- Spot check, not coverage: one sentinel proves arrival, not reading.
- Delegated reads in multi-agent systems are mislabelled `REPLAY_SUSPECT`.
- Synthetic probe pages; external validity to real pages is argued, not demonstrated.
- Adversarial agents are out of scope by construction. A model *trying* to defeat the check is a
  different threat model, and the protocol does not claim to resist it.

## 7. Related work

From [`related_work.md`](related_work.md). **The four literature tasks listed there must be closed
before submission** — the novelty claim in §1 is unsupported until the cooperative-canary search is
done.

## 8. Conclusion and open problems

The four open questions from PROBE_PROTOCOL.md §11, with coverage estimation via *k* sentinels as
the most promising follow-up.

---

## Author note on framing

This work came out of a game developer applying anti-cheat reasoning to agent tooling, not out of an
ML lab. Two consequences worth stating in the paper rather than hiding:

1. **The framing is the contribution, more than the cryptography.** Nothing in §3 would surprise a
   security engineer. The observation that agent context arrival is a *validation* problem with a
   server-authoritative answer is what was missing.
2. **The evaluation must be conservative in proportion.** An outsider making a strong empirical
   claim with a thin dataset is the standard way this kind of work gets dismissed. Better to publish
   a well-specified instrument with an honest null than an overstated effect.
