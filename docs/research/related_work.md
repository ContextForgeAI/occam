# Related work

> **Citation integrity notice.** This file distinguishes three kinds of entry:
> **[verified]** — a primary source the author has read and can cite precisely;
> **[concept]** — an idea that is established practice with no single canonical citation;
> **[unverified]** — a claim, name or attribution the author has *not* confirmed against a primary
> source, recorded here as a literature-search task.
>
> Nothing in the **[unverified]** set may be cited in a paper, a README or a talk until it has been
> checked. An invented citation is worse than a missing one.

---

## 1. Canary tokens and honeytokens

**[concept]** Planting a unique, unguessable value where only a genuine reader would encounter it,
then watching for that value to surface, is long-standing security practice: honeytokens in
databases, canary tokens in documents and DNS, unique watermarks in leaked drafts. The detection
logic is identical to the one this protocol uses — the value's appearance is the signal.

**What is different here.** The classic threat model is exfiltration: the token appearing means an
*attacker* touched the resource. Here the polarity is inverted. The token appearing is the
**desired** outcome, reported *voluntarily* by the party under test, and the absence of the token is
the finding. That inversion changes the design in two concrete ways:

- the token must be *easy to report verbatim* (hence base64url and the truncation trade-off in
  PROBE_PROTOCOL.md §7.3), which no exfiltration canary cares about;
- the protocol needs a state for "authentic but not issued by us" (`REPLAY_SUSPECT`), because a
  cooperative reporter and an out-of-band relay are indistinguishable by the value alone.

**[unverified]** Whether a published paper already frames canary tokens as a *cooperative*
attestation primitive rather than an adversarial detector. This is the most important gap to close
before any publication claim of novelty.

## 2. Verifiable retrieval and citation grounding

**[concept]** Attributed generation and citation-grounded QA: make the model point at the span it
used, then check the span exists. Widely deployed in RAG systems.

**Where the gap is.** These verify the *pipeline's* possession of a document and the *existence* of
a cited span. They are silent on whether the document's bytes entered the model's context window
before generation. A system can produce a valid citation to a retrieved chunk the model never
attended to. The canary targets the transit, not the provenance.

This repository already ships the provenance half: Receipt v1 signs what the fetcher received,
`occam_claim_check` grounds a claim in source blocks, `occam_attest` batch-checks a report against
its own citations. The canary is the missing complement — those prove *what was fetched*, the canary
probes *what arrived*.

## 3. Cryptographic MAC constructions

**[verified]** RFC 2104 (HMAC), RFC 5869 (HKDF), RFC 4648 §5 (base64url), RFC 2119 (requirement
levels). Used directly and cited precisely in `PROBE_PROTOCOL.md §12`.

**[concept]** Time-bucketed HMAC as a stateless, short-lived token is the same construction as
TOTP (RFC 6238) and as signed-URL expiry schemes: quantise time, include the bucket in the MAC
input, accept a small skew window. The canary's contribution is not the construction — it is
standard — but the four-state adjudication built on top of it, in particular separating
*authenticity* from *provenance*.

## 4. Progressive disclosure of tool surfaces

**[concept]** Reducing an agent's visible action space to improve selection accuracy is an active
practical concern in agent frameworks; the MCP specification provides
`notifications/tools/list_changed` precisely so a server can vary its advertised surface at runtime.

**[verified]** Model Context Protocol specification — `tools/list`, `notifications/tools/list_changed`.

**[unverified]** Named prior work on *capability-conditioned* tool disclosure — that is, sizing the
surface to a measured property of the client rather than to a static role. The author has not
confirmed that "Progressive Disclosure" or "Tool Attention" exist as named results in the
literature; they were working labels, not citations. **Literature search required** across agent
tool-use, action-space reduction and curriculum/scaffolding work before any novelty claim.

**[unverified]** "NabaOS" — appeared in the project's early notes as possible prior art. The author
cannot confirm it exists. **Do not cite until identified**; if it cannot be found, remove it rather
than hedging it.

## 5. Capability elicitation and agent benchmarking

**[concept]** Benchmark suites measure what an agent can do offline, at publication time. The exam
proposed here measures a *specific client, in-session, before granting capability* — closer to a
feature gate than to a benchmark. The design analogy the author drew is a game tutorial that
withholds mechanics until the player demonstrates the prerequisite.

**[unverified]** Whether in-session capability probing for authorisation (as opposed to offline
evaluation) has been formalised. **Literature search required.**

## 6. Where the game-engineering analogies come from

Stated plainly, since they are the reason these particular designs exist:

| Game pattern | Occam mechanism | Where the analogy breaks |
|---|---|---|
| Anti-cheat server-side validation: never trust the client's claim, validate against server state | Canary: never trust the agent's claim of having read, validate against a server-derived token | A cheating client is adversarial and adaptive; a hallucinating model is not trying to defeat the check, it is pattern-completing. Detection is easier; deterrence may be impossible for the same reason. |
| Replay detection: a valid-looking packet with no matching server record | `REPLAY_SUSPECT` | A multi-agent system where one agent reads and another reports is a *legitimate* flow that this verdict mislabels. The protocol has no notion of delegated reads (PROBE_PROTOCOL.md §11.4). |
| Tutorial gating: withhold mechanics until the player demonstrates the prerequisite | Capability exam → tool tier | A player improves; a model's capability is fixed within a session. The exam measures, it does not teach. |
| Level of detail: spend budget where the viewer can perceive it | Cascade: cheap HTTP path first, escalate only when the content demands it | LOD has a ground-truth error metric (screen-space pixels). "Is this extraction good enough" has no such metric, which is why the cascade needs post-processors and confidence scores rather than a threshold. |

These analogies are presented as *design provenance*, not as evidence. A pattern working in a
rendering pipeline is not an argument that it works on language models.

## 7. Immediate literature tasks

1. Search for cooperative/attestation framings of canary tokens (§1). Blocks any novelty claim.
2. Search for capability-conditioned tool disclosure (§4). Blocks H2's framing.
3. Identify or drop "NabaOS", "Tool Receipts", "Tool Attention" as named prior art (§4).
4. Search for in-session capability probing for authorisation (§5). Blocks H3's framing.
5. Check whether "proof of read" is already a term of art in a different field (DRM, e-learning
   compliance, audit) that would collide with this usage.
