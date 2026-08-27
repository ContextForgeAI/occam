# Occam Handbook

**Audience:** Anyone who did not write Occam and wants to understand what it can do, how it works, and where its promises stop.

**Authority:** This handbook follows the canonical model in `docs-audit/` (product definition, trust model, owner decisions, honesty schema). Where older pages under `docs/` disagree, **this handbook and the code win**.

**Version baseline:** 1.0.0-rc.4 foundation (published install channel `1.0.0-rc.3`) · core MCP tools from the registry (runtime `tools/list` varies by profile/opt-in; product default `OCCAM_PROFILE=reader`) · live extract by default (opt-in `cache_ttl_s` for local replay)

---

## Honesty preamble (read this first)

Occam is built around a contract that is easy to ignore and impossible for the product to enforce:

1. **`ok:false` means the page content is UNKNOWN.** Do not fill gaps from model memory. A typed refusal is an answer, not a blank page.
2. **A receipt proves integrity relative to a key, not truth.** It means *this install's key asserted these exact compiled bytes*. It does not prove the origin served them, who the signer is, or that the content is accurate.
3. **Compiled markdown is the object.** Occam returns a budgeted reading of a page, not the raw HTTP response body. Hashes and receipts cover that compiled form.
4. **Names that overclaim are corrected here.** `claim_check` retrieves and cites; it does not prove claims. `attest` is a heuristic citation assessment, not cryptographic attestation. `crosscheck` compares sources; it is not consensus proof. The `Receipt` field on `occam_extract_knowledge` is extraction telemetry, not Receipt v1.
5. **Distribution honesty.** npm is not a GA install channel. Cosign on install is **policy-gated** (`required-cosign-v1` for published `1.0.0-rc.3`; legacy `1.0.0-rc.2` stays SHA-256-only). Authenticity ≠ page-content truth. The community marketplace is operational machinery, not a trusted auto-merge supply chain.
6. **Every chapter is falsifiable.** Each includes a **CHECK** you can run. When observation and text disagree, executable code wins.

If you read nothing else before calling tools, read [Chapter 2](02-honesty-contract.md) and [Chapter 14](14-what-a-receipt-proves.md).

---

## What this book is

A teachable spine—not an API dump. Parameter tables live in [tools-reference.md](../tools-reference.md) and [reference/mcp-api.md](../reference/mcp-api.md). This handbook explains **mechanisms**, **mental models**, and **limits**.

### Canonical one-sentence definition

> Occam is a locally run host process that turns a URL into content an LLM agent can use: it acquires the page through a gated HTTP→browser→(optional third-party) ladder, compiles the result into a token-bounded representation, returns `ok:false` when content is unknown rather than guessed, and can sign what it produced so the exact bytes can be checked for tampering against a key the recipient obtains out of band.

---

## Parts and chapters (1–14)

| Part | Chapters | Theme |
|------|----------|-------|
| **A — Orientation** | [1](01-what-occam-is.md) · [2](02-honesty-contract.md) · [3](03-standing-up-an-install.md) · [4](04-request-path.md) | What Occam is, the honesty contract, a testable install, and why there is no single product-wide spine |
| **B — Reference path** | [5](05-acquisition-ladder.md) · [6](06-when-acquisition-is-hard.md) · [7](07-materialization-token-contract.md) · [8](08-structured-differential-output.md) | How pages are fetched, what blocks them, how output is budgeted, and optional structured/differential shapes |
| **C — Breadth** | [9](09-discovery-before-acquisition.md) · [10](10-many-sources-digest.md) | Cheaper signals before full extract, and many URLs under one budget |
| **D — Site-specific** | [11](11-playbooks-resolution.md) · [12](12-authoring-playbook.md) · [13](13-typed-field-extraction.md) | Recipes, authoring loop, typed field extraction |
| **E — Trust** | [14](14-what-a-receipt-proves.md) | Receipt v1: what signatures and Merkle roots license you to say |

Spine chapters (read carefully): **2, 4, 5, 7, 14**.

---

## Reading orders

### Shortest path to competence (six chapters)

**1 → 2 → 4 → 5 → 7 → 14**

Definition, honesty contract, spine plurality, acquisition ladder, token contract, receipt limits. If you deploy for others, also read [configuration](../configuration.md), [transports](../transports.md), and [Chapter 18 — Exposure](18-exposure.md).

### Agent integrator

**1 → 2 → 3 → 4 → 5 → 7 → 8 → 9 → 10 → 14**

Then [6](06-when-acquisition-is-hard.md) when you hit walls; [11–13](11-playbooks-resolution.md) for site-specific extraction.

### Operator

**1 → 2 → 3 → 5 → 6 → 14** plus [install](../install.md), [configuration](../configuration.md), [getting-started](../getting-started.md).

### Auditor / verifier

**1 → 2 → 7 → 14** then [receipts](../receipts.md), [receipt_verification](../receipt_verification.md), [guides/verify-sources](../guides/verify-sources.md).

### Adoption decision (no install required)

**1 → 2 → 5 (skim) → 14** plus [trust-and-safety](../trust-and-safety.md).

---

## Recurring example: Task R

Throughout the handbook, **Task R** asks: *What rate limits does this API document state?* You need a quotable sentence, evidence it was on the page you read, and a way to notice change—without guessing.

The concrete host should be a public, gate-observed site (see `corpora/l0-smoke.jsonl`). Network checks are tagged **NETWORK**; prefer local checks when offered.

---

## Public docs cross-links

| Topic | Public page |
|-------|-------------|
| Quick first success | [quick-start.md](../quick-start.md) |
| Install | [install.md](../install.md) · [getting-started.md](../getting-started.md) |
| Tool choice | [choosing-a-tool.md](../choosing-a-tool.md) |
| Concepts | [concepts.md](../concepts.md) |
| Failure codes | [failure-codes.md](../failure-codes.md) |
| Receipts | [receipts.md](../receipts.md) |
| Trust posture | [trust-and-safety.md](../trust-and-safety.md) |
| API contract | [reference/mcp-api.md](../reference/mcp-api.md) |

---

## Parts and chapters (15–27)

| Part | Chapters | Theme |
|------|----------|-------|
| **E — Trust (continued)** | [15](15-verifying.md) · [16](16-evidence-for-claims.md) | Verify modes, claims and corpora limits |
| **F — Deployment reality** | [17](17-opt-in-surfaces.md) · [18](18-exposure.md) · [19](19-operating-an-install.md) · [20](20-automatic-behaviors.md) · [21](21-state-and-footprint.md) · [22](22-configuration.md) · [23](23-security-posture.md) | Opt-in tools, exposure model, operations, automation, disk footprint, config negative space, security |
| **G — Synthesis** | [24](24-composing-tools.md) · [25](25-diagnosing-bad-results.md) · [26](26-architecture-internals.md) · [27](27-checking-this-book.md) | Tool chains, diagnosis, internals, falsification protocol |

Appendix: [Status labels](appendix-status-labels.md)

Spine chapters in this range: **18** (exposure). Chapters **20–21** are the canonical route for automation and state questions.

Design provenance: `docs-audit/HANDBOOK-OUTLINE.md` (engineering reference, not user-facing).
