# Playbooks

Site extraction recipes (playbooks / genomes) tune selectors, routing, and structured schemas.
This page covers resolution, authoring, and what playbook signatures actually prove.

## Tools

| Tool | Role |
|------|------|
| [`occam_playbook_resolve`](tools/occam_playbook_resolve.md) | Read-only lookup + signature status |
| [`occam_playbook_lint`](tools/occam_playbook_lint.md) | Static schema check before save |
| [`occam_playbook_heal`](tools/occam_playbook_heal.md) | Draft recipe fixes from live extract evidence |
| [`occam_playbook_save`](tools/occam_playbook_save.md) | Write local playbook + sign |
| [`occam_transcode`](tools/occam_transcode.md) | `playbook_policy=auto` applies resolved overlay internally |

## Resolution order

When `playbook_policy=auto` (or when you call resolve), Occam searches tiers in order until a host match:

1. **Local** — playbooks you saved on this machine
2. **`WT_PLAYBOOKS_PATH`** — operator-provided directory (if set)
3. **Community** — curated seed corpus (integrity-checked by manifest hash; not authenticated publisher identity)
4. **Seeds** — bundled popular-host seeds

First match wins. [`occam_playbook_resolve`](tools/occam_playbook_resolve.md) exposes `provenance` (which tier) and `sourcePath`.

Community and seeds are **not** a trusted auto-merge registry — treat them as starting points, not certified authors.

## Auto overlay on transcode

With `playbook_policy=auto`, a resolved playbook merges into the extract path (selectors, routing hints,
genome overlays). With `off`, plain transcode runs without overlay.

[`occam_claim_check`](tools/occam_claim_check.md), [`occam_attest`](tools/occam_attest.md), and
[`occam_dataset_export`](tools/occam_dataset_export.md) force `playbook_policy=auto` internally.

## Authoring loop

```
occam_playbook_heal  →  draft JSON  →  occam_playbook_lint  →  occam_playbook_save
```

- **Heal** proposes selector / routing changes from skeleton evidence.
- **Lint** catches schema errors cheaply.
- **Save** optionally dry-runs transcode (`verify=true`) and rejects recipes that fail the local gate heuristic.

Playbook save **always signs** — independent of `OCCAM_RECEIPTS=off`.

---

## Signature semantics (v1 vs v2)

Playbooks carry optional `provenance.signature`. Inspect via resolve returns
`signature.status` and `signature.sigVersion`.

### What a playbook signature proves

!!! success "What this proves"
    - **Recipe body integrity** — the JSON body matches `contentHash` and was signed by the holder of the local private key.
    - **v2 only:** `keyId`, `signedAt`, and the save-time gate snapshot (`verify.score`, `verify.passesGate`, `verify.noiseLeakage` when present) are **tamper-evident** under the signature.

!!! failure "What this does not prove"
    - Author identity, origin authenticity, or membership in a trusted registry
    - That the recipe is safe to run (browser playbooks can drive `page.evaluate` against untrusted pages)
    - That `verify.score` means high quality — it is a **local heuristic snapshot**, not a guarantee (even when signed in v2)

### v1 (`playbook-sig-v1` or absent `sigScheme`)

- Signs only `utf8(contentHash)` where `contentHash` hashes the recipe body with top-level `provenance` **excluded**.
- Fields inside `provenance` — including `keyId`, `signedAt`, `verify.score`, `verify.passesGate` — are **unsigned** and editable without invalidating the signature.
- Legacy artifacts remain verifiable under v1 rules.

### v2 (`playbook-sig-v2`)

- Signs a versioned assertion object: `v`, `alg`, `keyId`, `contentHash`, `signedAt`, `verify{…}` under domain-separated preimage `occam-playbook-sig-v2\n…`.
- Mutating any signed field or the recipe body yields `invalid` (same key) or `wrong_key` (foreign key).
- New saves emit v2. v1 artifacts are never silently reinterpreted as v2.

### Inspect verdicts

| `status` | Meaning |
|----------|---------|
| `unsigned` | No signature |
| `verified` | Valid under supplied/local key; body hash matches |
| `invalid` | Signature fails; same-key tamper suspected |
| `wrong_key` | Signature fails; claimed `keyId` differs from local |
| `key_mismatch` | Signature verifies but key id mismatch (defence path) |
| `unsupported_version` | Unknown `sigScheme` |

---

## Quality gate score (`verify.score`)

At save time (when `verify=true`), Occam dry-runs a transcode and records:

- `score` — heuristic quality score
- `passesGate` — whether the recipe passed the local gate
- `noiseLeakage` — noise metric when computed

**Meaning:** a recorded snapshot of how the recipe performed on the verify URL at save time.

**Not:** proof the recipe works on all pages, proof of safety, or third-party certification.

Under v1, treat displayed scores as **unsigned claims** unless you independently re-run verify.
Under v2, scores are **integrity-protected** against edit-after-sign — still heuristic, not proof.

---

## Related

- [Trust & Safety](trust-and-safety.md)
- [Receipts](receipts.md) — Receipt v1 (separate from playbook signatures)
- [Structured extraction](guides/structured-extraction.md)
- [Concepts — playbooks](concepts.md)
