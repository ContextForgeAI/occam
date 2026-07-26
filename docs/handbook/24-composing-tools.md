# Chapter 24 — Composing tools: what chains, and what does not

**Status:** STABLE · **Prerequisites:** [Chapter 10](10-many-sources-digest.md), [Chapter 13](13-typed-field-extraction.md), [Chapter 16](16-evidence-for-claims.md), [Chapter 17](17-opt-in-surfaces.md)

---

## Mental model

**Five composition classes** — direct, shared-subsystem, artifact-handoff, operator-workflow, implicit — and only **artifact-handoff** means "the output of A is a valid input to B." Most tool names that sound like pipelines are not wired that way in code.

---

## Explanation

### Composition classes

| Class | Meaning | Example |
|-------|---------|---------|
| Direct | Same call carries params | `occam_transcode` with `session_profile` |
| Shared-subsystem | Tools touch same service, no handoff | probe and transcode both use HTTP client |
| Artifact-handoff | Output of A is input to B | Receipt JSON → `occam_verify` |
| Operator-workflow | Human/script steps between tools | heal → draft JSON → save |
| Implicit | Side effects affect later calls | `occam_client_capabilities` changes ambient budget |

### Valid artifact handoffs (join keys)

| From | To | Join key |
|------|-----|----------|
| transcode / claim_check | `occam_verify` | Receipt envelope + optional markdown + pubkey |
| transcode blocks | `occam_verify mode=prove/citation` | `blockLeaves`, block text, index |
| dataset_export | CLI `verify --mode manifest` | Export JSON + manifest sig + pubkey |
| watch history | `occam_verify mode=history` | History JSON + pubkey |
| playbook_save file | resolve / transcode | Playbook id on disk |

### Eight rejected chains (do not build these)

| Attempted chain | Why it fails |
|-----------------|--------------|
| transcode markdown → `extract_knowledge` | Extract requires URL + schema; no markdown input |
| batch results → `dataset_export` | No batch-row export parameter |
| claim_check JSON → `attest` | Attest accepts claim text; re-runs fetch internally |
| `facts[]` → `claim_check` | Different artifact types |
| heal response → `save` | Heal emits skeleton/candidates, not `playbook_json` |
| MCP verify of dataset manifest | `manifest` mode is CLI-only |
| crosscheck verdict → verify | Verdict is unsigned |
| extract `Receipt` telemetry → verify | Not Receipt v1 — unsigned telemetry (OD-5) |

Calling **`claim_check` then `attest`** is not a pipeline — attest re-runs claim-check; double live fetch with no composition benefit and divergent receipt risk.

### Silent trust degradations (watch for these)

- Two different `max_tokens` → two legitimate `contentHash` values for one page.
- Cache replay serves stored signed envelope — not freshness proof.
- Session tier 2/3 tools drop `storageState` silently.
- `OCCAM_PROFILE=reader` historically hid verify — check current profile table.
- `OCCAM_RECEIPTS=off` + playbook save still signs.
- Unsigned watch entries — chain integrity ≠ signed history.
- Ambient budget change mid-session breaks hash continuity.
- Crosscheck verdict unsigned — cannot chain to verify.

---

## CHECK

**NETWORK.** Attempt three rejected chains against a live host and record the exact error or mismatch each returns:

1. Pass heal output directly to `occam_playbook_save` without drafting JSON.
2. Pass claim_check response JSON to `occam_attest`.
3. Pass extract_knowledge `Receipt` object to `occam_verify`.

---

## Common misconception

**"`claim_check` then `attest` is a pipeline."** `attest` internally re-runs claim-check and accepts claim text, not claim-check JSON. Calling both is a double live fetch with no composition benefit.

---

## Limitations

- Join-key table is not exhaustive — when in doubt, read tool input schemas in `MCP_API_SPEC.md`.
- Operator workflows (heal → draft → lint → save) require a human-shaped hole — no automated emitter.
- Batch and watch outputs are weak or unsigned trust objects.
- No tool chain proves truth — only bytes-and-keys integrity where signed artifacts hand off.

---

## Links

- [Chapter 10 — Digest](10-many-sources-digest.md)
- [Chapter 12 — Playbook authoring](12-authoring-playbook.md)
- [Chapter 16 — Evidence](16-evidence-for-claims.md)
- [Chapter 17 — Opt-in surfaces](17-opt-in-surfaces.md)
- User docs: [Choosing a tool](../choosing-a-tool.md) · [Examples](../examples/index.md)
- Audit: `docs-audit/COMPOSITION-MODEL.md`
