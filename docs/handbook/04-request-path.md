# Chapter 4 — The request path — and why there is no single spine

**Part A — Orientation** · **Spine chapter** · Prerequisites: [Ch 2](02-honesty-contract.md), [Ch 3](03-standing-up-an-install.md) · Next: [Chapter 5](05-acquisition-ladder.md)

---

## Mental model

**One reference narrative, several parallel spines.**

`TranscodePipeline` orchestrates the transcode family: preflight → playbook resolve → router/backends → post-processors → compile/materialize → optional receipt. **`OccamRouter` owns escalation** inside that path. Nine other tool names never enter this pipeline—or enter only fragments of it.

The linear diagram "client → discovery → acquisition → routing → materialization → trust" is **true for transcode/digest/claim paths** and **false product-wide**.

---

## Explanation

### Reference path (transcode family)

When you call `occam_transcode(url)`, the host roughly:

1. Validates arguments and applies fetch preflight (SSRF/private URL policy).
2. Resolves session profile headers (and browser `storageState` on tier-1 callers).
3. Resolves playbook overlay when policy is not `off` ([Chapter 11](11-playbooks-resolution.md)).
4. Routes through `OccamRouter` with `backend_policy` ([Chapter 5](05-acquisition-ladder.md)).
5. Runs HTTP and/or browser workers; optional managed provider last on dual local failure.
6. Applies ordered post-processors (challenge, login, thin extract, …).
7. Builds blocks internally; reconciles markdown.
8. Applies token budget, optional `fit_markdown` / `focus_query`, sidecars if requested ([Chapter 7](07-materialization-token-contract.md)).
9. Computes `contentHash` over compiled markdown (always on success path).
10. Optionally signs Receipt v1 ([Chapter 14](14-what-a-receipt-proves.md)).
11. Optionally writes opt-in disk cache entry.
12. Returns JSON with `ok`, `markdown`, diagnostics, `recovery[]`, `agentMeta.decisions`.

`occam_digest` repeats acquisition+compile per URL under one combined budget ([Chapter 10](10-many-sources-digest.md)). `occam_claim_check` / `occam_attest` / `occam_dataset_export` re-enter acquisition+compile for live fetches but skip much of the transcode response shape.

### Tools that bypass the full spine

| Tool | Enters pipeline? | Notes |
|------|------------------|-------|
| `occam_probe` | No worker spawn | HTTP-only classification |
| `occam_map` | No full extract | Link listing, cap 64 |
| `occam_search` | Provider proxy | Fails closed without provider |
| `occam_playbook_resolve` | Read-only resolve | No fetch |
| `occam_playbook_heal` | Browser skeleton path | Authoring, not reading |
| `occam_playbook_lint` / `save` | File/validation | No page materialization |
| `occam_extract_knowledge` | **Separate spine** | CSS worker; no Receipt v1, no post-processors, no token budget ([Chapter 13](13-typed-field-extraction.md)) |
| `occam_client_capabilities` | Config store | No URL |
| `occam_verify` | Verifier only | No acquisition |
| Opt-in: watch, batch, crosscheck, atlas | Partial / alternate | Env-gated ([Chapter 17 — Opt-in surfaces](17-opt-in-surfaces.md)) |

**Misconception driver:** "Everything gets budgets, post-processors, and receipts." False for `extract_knowledge` and discovery tools.

### Internal behaviors worth knowing

- The pipeline **unconditionally enables internal block collection** (`json_blocks`, `json_tables` features pushed internally) even when the caller did not ask—sidecars still require opt-in params on the wire.
- A canonical IR is built on some paths and **discarded**; there is no live codec selection beyond markdown passthrough.
- Managed provider content can be what gets signed when configured—third party sees the URL ([Chapter 5](05-acquisition-ladder.md)).

### Task R annotation

Take your first successful transcode from [Chapter 3](03-standing-up-an-install.md) and label which of the twelve steps above occurred. Then note: a later `occam_probe` on the same URL skips worker spawn entirely ([Chapter 9](09-discovery-before-acquisition.md)).

---

## CHECK

**LOCAL** — Compare stderr work.

1. Set `OCCAM_LOG=debug` (or `info`).
2. Run `occam_transcode` on a stable URL.
3. Run `occam_probe` on the same URL.
4. Compare stderr: transcode should show worker/router activity; probe should not spawn extract workers.

---

## Common misconception

**"Everything goes through the pipeline, so everything gets budgets, post-processors, and receipts."**

Nine of twenty-one registered tool names bypass all or most of that path. `occam_extract_knowledge` alone skips router post-processors, token budget, and Receipt v1—yet returns a field named `Receipt` that is **extraction telemetry only** (OD-5).

---

## Limitations

- This chapter is structural; parameter details are generated in [tools-reference.md](../tools-reference.md).
- Digest items carry **reduced** receipts (content hash only)—weaker trust object than full transcode ([Chapter 10](10-many-sources-digest.md)).
- Profile gates change **exposure**, not handler semantics—a `reader` profile still signs transcodes.
- Alternate transports (WS, batch HTTP) change process topology and can affect browser pool lifecycle.

---

## Links

**Public docs:** [How Occam works](../how-occam-works.md) · [Concepts](../concepts.md) · [Choosing a tool](../choosing-a-tool.md) · [Semantic contract](../architecture/semantic-contract.md)

**Next chapter:** [Chapter 5 — Acquisition: the real ladder](05-acquisition-ladder.md)
