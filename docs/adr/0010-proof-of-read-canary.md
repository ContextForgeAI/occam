# ADR-0010 — Proof-of-read via HMAC time-bucket sentinels

**Status:** Accepted
**Date:** 2026-09-15
**Supersedes:** none
**Specification:** [`PROBE_PROTOCOL.md`](https://github.com/ContextForgeAI/occam/blob/main/PROBE_PROTOCOL.md)

## Context

Occam's trust model already refuses to guess: a failed fetch returns `ok: false` and a typed failure
code rather than a plausible summary. That protects against the host lying about what it fetched. It
does nothing about the next link in the chain — whether the fetched bytes actually reached the
model's context before the model wrote its answer.

Three existing signals were considered and rejected as answers to that question:

- **Content hashes / receipts** (already shipped here as Receipt v1) prove *what the fetcher
  received*. A model can ignore the payload entirely and the receipt still verifies.
- **Retrieval citations** prove *a span exists in a document the pipeline holds*. Again upstream of
  the model's context.
- **Self-report** ("I read the page") is unfalsifiable and is exactly what the problem is.

We needed a signal that is unforgeable by the model, unavailable from training data, and cheap to
check.

## Decision

Serve a probe document embedding a **sentinel**: `HMAC-SHA256(subkey, canonical(bucket, sessionId))`
truncated to 16–32 bytes and encoded as unpadded base64url, where
`bucket = floor(unixSeconds / 300)`. Ask the agent to report it. Adjudicate the report into one of
four states — `READ_VERIFIED`, `READ_STALE`, `HALLUCINATED`, `REPLAY_SUSPECT`.

Specific choices, each with a reason:

- **Keyed hash, not a random nonce in a table.** A nonce table is state that must be stored,
  replicated and expired; a MAC is stateless and verifiable by recomputation. The only state kept is
  the issuance log, and that exists for a *different* purpose (replay detection), so losing it
  degrades one verdict rather than breaking verification.
- **Time buckets, not per-request nonces.** A bucket makes the sentinel stable for a window, which
  is what lets `READ_STALE` exist as a distinct verdict: "you really read this, just not now" is a
  materially different finding from "you made this up", and a per-request nonce cannot express it.
- **HKDF to two subkeys.** The audit log needs to hash client IPs. Using the same key for that and
  for sentinels would mean the component that pepper-hashes addresses could forge proofs.
- **Injective MAC framing** (`label ‖ 0x00 ‖ int64be(bucket) ‖ int32be(len) ‖ sessionId`). Plain
  concatenation of a variable-length id with a number is re-partitionable, which is a forgery
  primitive as soon as a future revision appends a field.
- **`TimeProvider` everywhere.** Bucket transitions and stale horizons must be testable without
  sleeping, or they will not be tested.
- **Process-scoped secret, never persisted.** Restarting invalidates outstanding sentinels. That is
  a feature: it bounds the lifetime of leaked proof material to one process.

## Consequences

**Good.** A falsifiable read signal with a published protocol and test vectors. Verification is
recomputation, so it needs no database. The whole subsystem is deterministic and side-effect free
apart from its own bounded log, which made 91 % line coverage and byte-identical cross-platform
vectors achievable.

**Costs.** The protocol is clock-dependent: a verifier whose clock jumps backwards past a bucket
boundary reports `HALLUCINATED` for sentinels it issued moments ago. Both endpoints are oracles and
must be rate limited. The probe document is unique per session and uncacheable, which makes it
tracking-adjacent and therefore subject to the privacy constraints in PROBE_PROTOCOL.md §7.7.

**Bounded claim.** `READ_VERIFIED` proves a value transited into the reporting entity's context
inside a time window. It does not prove comprehension, does not prove the *whole* page was read, and
does not survive a human relaying the value by hand. The specification states this explicitly
because the failure mode of this feature is a downstream system treating the verdict as ground
truth.

## Alternatives considered

**Embed a hash of the page and ask the agent to echo it.** Rejected: the hash is a function of
public content, so a model that has memorised the page can produce it without reading.

**Ask the agent to quote a random span of the page.** Rejected: unfalsifiable at the margin (was the
paraphrase close enough?), and a model with prior knowledge of the URL can approximate it. The
canary is binary by construction.

**Per-request nonce stored server-side.** Rejected: introduces storage and replication as a
correctness dependency, and cannot express `READ_STALE`.

**Signing sentinels with the existing Receipt v1 ECDsa key.** Rejected: signatures are for third
parties to verify offline with a public key, which is the opposite of what is needed here — a
sentinel must be *unpredictable*, so only the issuer may be able to produce it. A public key would
let anyone mint one.
