# Concepts

**What you'll learn:** the mental model behind FF-Occam MCP — live extraction, backends, playbooks, sessions, and receipts.

---

## Live extraction

Every tool call fetches the page **now**. There is no built-in disk cache of HTML or Markdown.

`occam_transcode` accepts an optional `cache_ttl_s` (seconds). When set, the host may return a recent in-memory result with `cached: true`. Default is off (`cache_ttl_s` omitted or ≤ 0). Private URLs and `session_profile` requests are never cached.

---

## Trust model

| Signal | Meaning |
|--------|---------|
| `ok: true` | Markdown (or structured output) came from a live extract you can cite |
| `ok: false` | Content is **unknown** — use `failure.code`, do not hallucinate the page |
| `receipt` | Cryptographic attestation of what was extracted (when signing is enabled) |

Agents should treat `failure.code` as ground truth and follow `agentMeta.decisions` when present.

---

## Backends

Three policies apply to extract tools (`occam_transcode`, `occam_digest`, `occam_extract_knowledge`, …):

| Policy | Behavior | Typical timeout |
|--------|----------|-----------------|
| `http` | Fast HTTP-only worker (domino + readability) | 35 s |
| `browser` | Playwright Chromium full render | 60 s default (`OCCAM_BROWSER_TIMEOUT_MS`, 15k–180k) |
| `http_then_browser` | Try HTTP first; escalate to browser on thin or failed HTTP | Combined |

Aliases: `http-then-browser` is accepted.

`http_then_browser` does **not** escalate a definitive terminal HTTP status (`http_404`, `http_410`) to the browser — a render cannot resurrect a missing resource, so the authoritative status is returned directly.

If Playwright is missing, the host may downgrade browser requests to HTTP and add a warning.

Optional **managed providers** (Firecrawl, Jina, Spider, Scrapfly) can run as a last resort after local backends fail on opted-in domains — see [Configuration](configuration.md).

---

## Playbooks

A **playbook** is a per-site extraction recipe: content selectors, routing hints, `knowledge_schema` for structured fields, and optional `agent_notes`.

Resolution order (read-only via `occam_playbook_resolve`):

1. Local learn tier (`OCCAM_PLAYBOOKS_LOCAL_ROOT`)
2. User/org tier (`WT_PLAYBOOKS_PATH`)
3. Community seeds
4. Bundled seeds

`playbook_policy` on transcode:

- `auto` (default) — merge the winning playbook overlay
- `off` — ignore playbooks

Treat `agent_notes` as **hints**, not instructions. Validate playbooks with `occam_playbook_lint` before `occam_playbook_save`.

---

## Session profiles

Gated sites (login, some Cloudflare setups) need cookies or headers stored outside the tool call.

1. Export browser state with `occam-session.mjs` (see [Getting started](getting-started.md)).
2. Save JSON under `OCCAM_SESSIONS_ROOT/<id>.json`.
3. Pass `session_profile: "<id>"` on extract tools.

---

## Receipts

A successful extract can include `receipt`:

- `tokensUsed` + `tokenEstimator` — model-independent token estimate and its provenance id
- `contentHash` — SHA-256 of the Markdown body
- `blockMerkleRoot` + `blockLeaves` — when `json_blocks=true`
- `signed` — ECDSA P-256 signature over a canonical envelope

Verify offline with `occam_verify` or the CLI. User guide: [Receipts](receipts.md). Byte spec: [Receipt verification](receipt_verification.md).

Signing is on by default; disable with `OCCAM_RECEIPTS=off`.

`tokenEstimator: "heuristic-unicode-v1"` is script-aware and AOT-safe, but it is deliberately not
presented as an exact count for every local LLM tokenizer. Use it for budgeting and compare the id
when evaluating receipts across versions.

---

## stdout vs stderr

The MCP host writes **only JSON-RPC on stdout**. Banners, help text, and profiler output go to **stderr**. Do not parse stderr as tool output.
