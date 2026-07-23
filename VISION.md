# VISION.md — where FF-Occam MCP is going

A strategic brief: the north-star, the moat, and a four-layer growth plan. This is the product
counterpart to [REVIEW_GUIDE.md](REVIEW_GUIDE.md) (which covers *how the code works* for a code
review). It exists so a strategic reviewer (human or model) can evaluate the project and propose
direction from a shared, honest picture — not from marketing. Where this doc and the shipped code
disagree, the code wins.

---

## 1. North star

> **The verifiable web layer for AI agents — the only one whose results you don't have to take on faith.**
> Every extraction carries a *cryptographically verifiable receipt* (**shipped: Receipt v1**), stays
> accurate through a *community playbook ecosystem*, and powers *trustworthy deep-research and RAG
> with real citations* — all running locally, no cloud middleman.

**Positioning:** *honesty* is the means, *verifiability* is the product. "Honest" is intent
(unprovable from the outside); "verifiable" is mechanism (provable) — as of Receipt v1 a result
carries a signature a consumer can check with `occam_verify`. Local-first stays the trust
foundation: a receipt signed by a local key needs no SaaS middleman, so trust is decentralized by
construction. **Primary audience** (per the 2026-07 strategy review): agent builders whose agents
*make decisions* on extracted content — compliance, finance, monitoring, legal audit — who need
receipts like air; RAG engineers with unauditable corpora second; indie devs are a distribution
channel, not the paying pain.

---

## 2. The wedge — why this is different

Most web-extraction tools (Firecrawl, Jina Reader, Crawl4AI) optimize for **coverage**: return
*something* for every URL. FF-Occam optimizes for **honesty**:

- `ok:false` = **unknown content** — the host never infers page content from model memory on
  failure. Typed failure codes (`thin_extract`, `captcha_or_challenge`, `requires_login`, …), a
  trust-floor that rejects login/JS/consent shells dressed up as content, and per-tier metrics
  (never a single inflated global success rate).
- **Token-aware compilation** — the output is budgeted and pruned for an LLM, not a raw dump.
- **Local & AOT** — a Native AOT .NET host + Node workers; runs on a laptop or a phone-class
  device, no SaaS in the loop.

That honesty is the foundation the moat is built on: a result you can *trust* is a result you can
*prove* and *cache*.

---

## 3. What ships today (honest inventory)

Canonical detail: [docs/roadmap.md](docs/roadmap.md) · [MCP_API_SPEC.md](MCP_API_SPEC.md) ·
[REVIEW_GUIDE.md](REVIEW_GUIDE.md).

- **Core MCP tools** — fifteen always-on `occam_*` tools (see `OccamMcpServerRegistration.OccamToolNames`
  / [MCP_API_SPEC.md](MCP_API_SPEC.md)), including `occam_transcode`, `occam_probe`, `occam_digest`,
  `occam_map`, playbook resolve/heal/save/lint, `occam_extract_knowledge`, `occam_search`,
  `occam_verify`, `occam_claim_check`, `occam_attest`, `occam_dataset_export`, and
  `occam_client_capabilities`. Opt-in: `occam_watch`, `occam_batch_*`, `occam_crosscheck`,
  `occam_failure_atlas` (env-gated).
- **Verifiable receipts (Receipt v1)** — a signed `receipt.signed` on every success (contentHash +
  block-Merkle root + provenance, ECDsa P-256 local key), signed **negative** receipts on honest
  failures, and `occam_verify` to check them offline or live. **Shipped.** Deeper attestation ideas
  (multi-node jury, optional time anchors) are future direction — see §4.
- **Structured outputs** — `json_blocks` (RAG citation blocks with source selectors),
  `json_tables`, `json_feed`; `semantic_chunking`.
- **Playbooks** — community-shared, signed, per-site "genomes" (content selectors, strip rules,
  knowledge schemas); a heal→save→verify loop that repairs extraction when a site drifts.
- **Trust & safety** — post-processor trust floor; validated outbound egress (DNS-validated
  targets, redirect/navigation guards); cross-origin header scoping.
- **Transport** — stdio (default) + WebSocket + Remote MCP (TLS + JWT).

**Thin-core thesis:** Core stays lean. No vector DB / embeddings / crawl-engine / VLM *inside*
Core — those belong in optional satellites around it (see roadmap “Not shipped”).

---

## 4. The strategy — four layers (flagship: Verifiable Extraction Receipts)

The four directions compose into one stack. Each names whether it extends **Core** or ships as a
**satellite** (honoring the thin-core thesis).

| Layer | Direction | Core / satellite | Why it's unique |
|-------|-----------|------------------|-----------------|
| **① MOAT (flagship)** | **Verifiable Extraction Receipts + Cognitive Caching** | Core-adjacent | Sign each extraction (content hash + provenance). An agent — or a *downstream consumer* — can **verify** "this markdown really was on that page at that time," and reuse a cached, receipt-carrying extraction instead of re-fetching. **SHIPPED — Receipt v1** (`occam_verify`). Next directions (not shipped): richer Merkle citation proofs; multi-node cache exchange. |
| **② Data / network** | **Community Playbook ecosystem** | Core (exists) + future registry | Signed, versioned per-site genomes; heal loop. Future: public registry, reputation, auto-resolve. |
| **③ Killer app** | **Agentic deep-research** | Satellite (orchestrator) | `search → probe → extract → synthesize` under a token budget with receipt-backed claims. Stays *outside* Core. |
| **④ Integration** | **RAG-native pipeline** | Satellite | Embeddings, citation graph, dataset export — separate from the AOT host. |

**Recommended sequence:** ① flagship first (the moat; everything else is more valuable once
extractions are verifiable) → then ② + ④ in parallel (network + immediate utility) → ③ on top.

---

## 5. Principles that must survive growth

New features are only "on-brand" if they keep these:

1. **Honesty over coverage** — never inflate success; `ok:false` stays truthful; per-tier metrics.
2. **Trust model** — no path returns unknown/partial content as if it were the page.
3. **Local-first** — a core capability must work with no cloud dependency (cloud = opt-in).
4. **Thin core** — heavy/optional capability ships as a satellite, not in the AOT host.
5. **No overfitting** — no per-site or per-fixture hardcoding in generic logic.
6. **Verifiable by default** — Receipt v1 has landed; prefer designs where a claim can be proven.

---

## 6. What a strategic reviewer should evaluate

1. **Functionality** — per tool / capability: strong, weak, redundant; usefulness to an agent.
2. **Positioning** — sharpest honest one-liner + primary audience.
3. **Feature ideas per layer** — concrete ideas within ①–④, Core vs satellite, impact × effort.
4. **Growth sequence** — next milestones toward the north star without claiming unshipped work.
5. **Moat & risk** — is verifiable extraction a real moat? What to double down on or cut.

---

## 7. Non-goals (keep us honest)

- Not a bulk-scraping / data-harvesting engine — extraction is per-URL, honest, agent-facing.
- Not a CAPTCHA-solving / identity-rotating service by default — a stock-browser fingerprint that
  reads *real* content is on-brand; solving hard anti-bot is opt-in, managed-tier territory.
- Not a summarizer — summarization belongs to the calling LLM, not the transcoder.
- Not a vector DB — RAG storage is a satellite, never Core.
- Not a shipped VS Code/Cursor marketplace extension or WASM edge extractor in `1.0.0-rc.2`
  (see [docs/roadmap.md](docs/roadmap.md) “Not shipped”).
