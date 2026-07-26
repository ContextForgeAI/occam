# Chapter 11 — Playbooks in band: resolution and the auto overlay

**Part D — Site-specific** · Prerequisites: [Ch 5](05-acquisition-ladder.md), [Ch 7](07-materialization-token-contract.md) · Next: [Chapter 12](12-authoring-playbook.md)

---

## Mental model

**A playbook is a soft overlay on the acquisition spine**, resolved per call from four tiers with per-field precedence:

**local → org (`WT_PLAYBOOKS_PATH`) → community → seeds**, plus optional live genome fetch.

Transcode **re-resolves internally**—there is no "pass resolved playbook" parameter. `occam_playbook_resolve` is for planning and inspection.

---

## Explanation

### Resolution tiers

| Tier | Source | Trust note |
|------|--------|------------|
| Local | Operator files under playbook paths | Operator-controlled |
| Org | `WT_PLAYBOOKS_PATH` | Same |
| Community | Remote manifest + sha256 integrity | **Integrity only, not authenticated authorship**; marketplace auto-merge is not a trusted supply chain (OD-1) |
| Seeds | Shipped seed recipes | Baseline hints |
| Live genome | Optional `/.well-known` fetch | Off by default; fetch has known edge cases |

Per-field merge: later tiers fill missing fields; explicit local wins for defined precedence rules.

### What a playbook can change

- DOM selectors, interaction steps, noise rules
- `preferredBackend` — overrides request policy **only when** policy is `http_then_browser`
- `knowledge_schema` for typed extraction ([Chapter 13](13-typed-field-extraction.md))
- Page class hints

Playbook interaction plans can reach `page.evaluate` / `waitForFunction`—**recipes are code-like**, not pure CSS lists.

### `playbook_policy`

| Value | Behavior |
|-------|----------|
| `off` | No overlay |
| `auto` | Resolve and apply when match |

`claim_check`, `attest`, and `dataset_export` **force `auto` internally**—no disable parameter; responses may omit `playbookId`.

### Inspecting what applied

Compare transcodes with `playbook_policy=off` vs `auto` on the same URL. Receipt may include `playbook{id,version}` when overlay applied.

`occam_playbook_resolve(url)` shows winning tier, matched fields, and whether `knowledge_schema` exists—use before `extract_knowledge`.

Task R step 8: resolve API host, then transcode with `auto` vs `off`.

---

## CHECK

**NETWORK**

1. Transcode a playbook-covered host twice: `playbook_policy=off` and `auto`.
2. Diff markdown and receipt `playbook` fields.
3. Optionally call `occam_playbook_resolve` and confirm tier matches expectations.

---

## Common misconception

**"I resolve a playbook and pass it to transcode."**

No such parameter. Resolve informs the agent; transcode resolves again at call time.

---

## Limitations

- Community/marketplace: document as machinery, not validated trusted auto-merge.
- `page_class` / `knowledge_schema` match failures may be swallowed on resolve path—do not promise those failure codes from resolve alone.
- Live genome fetch: empty Content-Type bypass and read-before-truncate behaviors—treat as untrusted remote input.
- Playbook overlay changes compiled bytes → changes `contentHash`.

---

## Links

**Public docs:** [Concepts](../concepts.md) (playbooks) · [Structured extraction](../guides/structured-extraction.md) · [Tools: occam_playbook_resolve](../tools/occam_playbook_resolve.md)

**Next chapter:** [Chapter 12 — Authoring a playbook](12-authoring-playbook.md)
