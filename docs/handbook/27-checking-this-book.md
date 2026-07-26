# Chapter 27 — Checking this book yourself

**Status:** INTERNAL (handbook-only) · **Prerequisites:** all prior chapters

---

## Mental model

**A book about honesty must be falsifiable.** When observation disagrees with text, precedence is: **executable code wins**, then the Wave-4 correction layer, then canonical audit ledgers, then this handbook.

---

## Explanation

### Why this chapter exists

Every handbook chapter carries a **CHECK** — a runnable observation that can refute a load-bearing claim. This chapter collects them into a protocol and states what to do when code and prose diverge.

### Precedence rule

1. **Code** at cited paths/lines.
2. **Wave-4 engineering findings** (`docs-audit/ENGINEERING-FINDINGS.md`, EF-*).
3. **Canonical ledgers** (`TRUST-MODEL.md`, `ENTRYPOINT-MODEL.md`, etc.).
4. **This handbook.**

A disagreement is **first a book bug** until re-reading the cited source decides otherwise. "The tool is broken" is not the default conclusion.

### Register of checks (by chapter)

| Ch | Tag | Check summary |
|----|-----|---------------|
| 3 | LOCAL | `OCCAM_RECEIPTS=off` — key still in `~/.occam/keys/` |
| 4 | LOCAL | `OCCAM_LOG`: transcode vs probe stderr work differs |
| 5 | NETWORK | 404 no browser; public-ref no browser; SPA has browser attempt |
| 6 | LOCAL | Same session_profile: transcode vs probe storageState |
| 7 | NETWORK | Two `max_tokens` → different `contentHash` + `compile.omitted` |
| 8 | NETWORK | `cache_ttl_s`: fragment `#a` vs `#b` collision risk |
| 9 | NETWORK | search without provider fails closed; map ≤64 links |
| 10 | NETWORK | Digest with one 404 — per-item failure, reduced receipt |
| 11 | NETWORK | playbook auto vs off diff |
| 12 | LOCAL | Edit unsigned v1 `provenance.verify.score` — still "verifies" |
| 13 | NETWORK | extract ignores max_tokens; telemetry confidence 0.0 |
| 14 | LOCAL | receipts off — hash, key, playbook save still signs |
| 15 | LOCAL | Foreign receipt via MCP without pubkey; unsigned watch chain |
| 16 | NETWORK | Paraphrase claim_check miss |
| 17 | LOCAL | crosscheck visible under reader + consensus flag |
| 18 | LOCAL | tools/list per profile |
| 19 | LOCAL | keys export empty dir mints key |
| 20 | NETWORK | OCCAM_LOG browser automatics |
| 21 | LOCAL | Filesystem diff vs state inventory |
| 22 | NETWORK | proxy ignored by probe |
| 23 | LOCAL | extract_knowledge vs transcode on private IP |
| 24 | NETWORK | Three rejected chains |
| 25 | LOCAL | unset OCCAM_HOME → workers_unavailable |
| 26 | LOCAL | dead-register grep |
| 27 | meta | Refute one sentence without reading C# |

**Tag `NETWORK`:** checks against live third-party sites decay — pair with LOCAL alternatives where listed.

**Tag `SOURCE-PROVEN`:** some claims were code-proven but not runtime-reproduced in audit (e.g. WS pool kill, fragment cache collision, Docker health, certain tamper constructions). Treat as hypothesis until you reproduce locally.

### Running checks as one session

1. Stand up install ([Chapter 3](03-standing-up-an-install.md)).
2. Walk Task R thread: transcode → probe → digest → trust tools → operator verbs.
3. Record each CHECK outcome in a log: PASS / FAIL / NETWORK-SKIP / SOURCE-PROVEN-PENDING.
4. On FAIL: cite handbook sentence, observation, and code path you looked up.

### Reading a disagreement

| Observation | Likely meaning |
|-------------|----------------|
| CHECK fails consistently | Handbook or public docs bug — fix docs after code confirm |
| CHECK flaky on NETWORK | Site changed — update example URL or mark NETWORK-deprecated |
| Code changed since audit | Expected drift — re-run gate, update handbook |
| CHECK passes but contradicts older docs | Older docs wrong — handbook wins if code-aligned |

### What is not checkable (honest limits)

- **Tokenizer error bounds** — unmeasured; no check asserts token counts.
- **Truth / origin / identity** — no check can prove these; forbidden claims stay forbidden.
- **Third-party site behavior forever** — NETWORK checks expire.
- **GitHub org branch protection** — marketplace trust (OD-1) requires external evidence.

### Success criterion for this chapter

You can refute at least one handbook sentence using only observations and cited code paths — without accepting prose on faith.

---

## CHECK

**META.** Pick one load-bearing claim from [Chapter 14](14-what-a-receipt-proves.md) or [Chapter 20](20-automatic-behaviors.md). Design an observation that would falsify it. Run the observation. If it passes, state exactly what the claim still does **not** license you to say.

---

## Common misconception

**"If the book and the tool disagree, the tool is broken."** The handbook is downstream of code. Disagreement is first a book bug; only re-reading cited `path:line` decides.

---

## Limitations

- Falsification protocol does not replace the integration gate for release claims.
- Some CHECKs require operator care (refresh kill, connect mutations).
- Audit incompleteness is documented in `docs-audit/CANONICAL-AUDIT-INDEX.md` — not every EF has a CHECK yet.

---

## Links

- All handbook chapters — per-chapter CHECK fields
- `docs-audit/DISCOVERABILITY-GATE.md` §3 — mechanical doc checks
- `docs-audit/CANONICAL-AUDIT-INDEX.md` — known incompleteness
- User docs lint: `node scripts/check-docs.mjs`
