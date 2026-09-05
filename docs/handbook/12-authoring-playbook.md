# Chapter 12 — Authoring a playbook: heal → draft → lint → save

**Part D — Site-specific** · Prerequisites: [Ch 2](02-honesty-contract.md), [Ch 11](11-playbooks-resolution.md) · Next: [Chapter 13](13-typed-field-extraction.md)

---

## Mental model

**The loop has a human-shaped hole in the middle.**

`occam_playbook_heal` returns DOM skeleton + selector **candidates**—not finished playbook JSON. **You** draft the recipe. `occam_playbook_lint` is advisory. **`occam_playbook_save(verify:true)` is the gate** that persists a signed portable JSON file.

Only run this loop on **genuine extraction failures** (`thin_extract`, etc.)—not on `short_quality` successes ([Chapter 2](02-honesty-contract.md)).

---

## Explanation

### Heal → draft → lint → save

1. **`occam_playbook_heal(url)`** — Browser-backed skeleton capture, candidate selectors, optional mechanical `draftPlaybookJson` stub. Does not LLM-author a recipe; review the stub (or write your own) before save. `--consent-aggressive` worker flag unreachable from MCP.
2. **Draft** — Prefer editing `draftPlaybookJson` when present; otherwise write playbook JSON from candidates (selectors, interaction steps, schema hooks).
3. **`occam_playbook_lint(playbook_json)`** — Structural/advisory checks. **Different parser from save/resolve**—lint passing does not guarantee save acceptance.
4. **`occam_playbook_save(..., verify:true)`** — Runs save-time gate (`verify.score`, `passesGate`, noise leakage). Signs on success.

Re-test with [Chapter 11](11-playbooks-resolution.md) comparison (`auto` vs prior `off`).

### Playbook signatures v1 vs v2 (OD-4)

| Version | Signed bytes | Honest reading |
|---------|--------------|----------------|
| **v1 (legacy)** | Recipe body hash only; whole top-level `provenance` excluded | `verify.score`, `passesGate`, `keyId`, `signedAt` **unsigned**—editable without invalidating v1 verify |
| **v2 (new saves)** | Domain-separated preimage covers `keyId`, `alg`, `contentHash`, `signedAt`, and `verify{score,passesGate,noiseLeakage}` | Gate snapshot is **tamper-evident** relative to key—not a proof of objective quality |

Never call `verify.score` a quality proof—even when signed in v2, it is a local heuristic gate snapshot.

### Signing policy

- **`occam_playbook_save` always signs**, ignoring `OCCAM_RECEIPTS`.
- Signature proves integrity relative to **local key**, not author identity or marketplace trust.
- `PlaybookCommunitySanitizer` is not on the local save path—nothing publish-sanitizes automatically.

### Task R step 9

Pricing page still `thin_extract` after browser → heal → draft → lint → save → re-transcode with `playbook_policy=auto`.

---

## CHECK

**LOCAL**

1. Save a playbook with `verify:true`.
2. On disk, edit `provenance.verify.score` (v1) or signed verify block fields (v2—should break verify).
3. Re-inspect with resolve/save verify path:
   - **v1:** score edit still "verifies" — proves score was never in v1 signed bytes.
   - **v2:** tamper should surface `invalid` / failed inspect.

Also: editing unsigned `provenance.keyId` on v1 self-signed playbooks could downgrade tamper to innocuous `unknown_key` before verify runs—treat inspect verdicts carefully.

---

## Common misconception

**"Heal produces a finished playbook and save stores it."**

Heal emits skeleton + candidates and may attach a mechanical `draftPlaybookJson` stub. The stub is not verified — lint and `occam_playbook_save(verify:true)` remain the gate.
---

## Limitations

- Playbooks can execute page JS—untrusted URLs + untrusted recipes = code execution risk ([trust-and-safety](../trust-and-safety.md)).
- Lint vs save parser mismatch (EF-015)—always run save as final gate.
- Marketplace CI auto-merge is not trusted validation (OD-1).
- Healing a `short_quality` page wastes resources and may harm good extracts.

---

## Links

**Public docs:** [Structured extraction](../guides/structured-extraction.md) · [Tools: occam_playbook_heal](../tools/occam_playbook_heal.md) · [Tools: occam_playbook_save](../tools/occam_playbook_save.md) · [Tools: occam_playbook_lint](../tools/occam_playbook_lint.md)

**Next chapter:** [Chapter 13 — Typed field extraction](13-typed-field-extraction.md)
