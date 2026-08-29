# Occam

**Live web pages → compact Markdown for AI agents. Honest failures. Local-first.**

[Docs](https://contextforgeai.github.io/occam/) · [Why Occam](https://contextforgeai.github.io/occam/why-occam/) · [Quick Start](https://contextforgeai.github.io/occam/quick-start/) · [`llms.txt`](llms.txt) · [Releases](https://github.com/ContextForgeAI/occam/releases)

> Occam is an MCP host (and CLI) that **acquires the real page now**, strips chrome, fits your token budget, and returns either usable Markdown **or** a typed `ok:false`. It does **not** invent page text from model memory.

**Status:** published install channel **`1.0.0-rc.4`** (release candidate). npm is an RC path — not GA. Guarded install = signed GitHub Release bootstrap ([INSTALL.md](INSTALL.md)).

---

## What you get (30 seconds)

| | Capability |
|---|------------|
| **Honest read** | Live extract → Markdown, or typed refusal. `ok:false` = content **unknown** — never guess. |
| **Token contract** | Ambient budget via `occam_client_capabilities`, or `max_tokens` / `fit_markdown` + `focus_query`. Not an LLM summarizer. |
| **Acquisition ladder** | HTTP first → browser when needed → optional managed / archive (operator env). |
| **One page / many** | `occam_transcode` (one URL) · `occam_digest` (many) · `occam_map` / `occam_search` (discover). |
| **Structure on demand** | Opt-in `json_blocks` / tables / feeds · `if_none_match` / `diff_against` for cheap re-checks. |
| **Integrity** | Optional Receipt v1 → `occam_verify` (integrity vs a key — **not** truth). |
| **Playbooks** | Per-site recipes: resolve / heal / lint / save when you author them. |
| **Local-first** | Runs on your machine; SSRF / private URL blocks by default. |

Full knobs + tool map: **[Why Occam](docs/why-occam.md)** (read this before treating Occam as “just fetch”).

---

## Install

**Fast try (npm RC):**

```bash
npm install -g ff-occam@1.0.0-rc.4
occam connect
```

**Guarded release (recommended):**

```bash
# Linux x64 / macOS Apple Silicon
curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh | bash
```

```powershell
# Windows x64
irm https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.ps1 | iex
```

Published RIDs today: `win-x64`, `linux-x64`, `osx-arm64`. Details, Cosign policy, safety notes: [INSTALL.md](INSTALL.md) · [installation safety](docs/trust/installation-safety.md).

Then open a **new** chat in the connected host and ask it to read a URL (example below).

---

## Two ways to use it

### MCP (AI agents — Cursor, Claude, Hermes, …)

After `occam connect` (or a manual MCP snippet), the host sees Occam tools. Default profile **`reader`** ≈ 8 tools; **`full`** = 15 core tools. Runtime `tools/list` wins.

**Default page read — only `url` required:**

```text
Use Occam to read https://example.com/ and summarize it. Include the source URL.
On ok:false do not invent the page — report the failure code.
```

Agent map: [`llms.txt`](llms.txt) → start with [Why Occam](docs/why-occam.md).

### CLI (humans / scripts)

```bash
occam doctor          # runtime health
occam connect         # wire a supported MCP host
occam --help
```

Ad-hoc extract from a checkout (dev):

```bash
dotnet run --project benchmarks/l0-gate -- --url=https://example.com
```

---

## Core tools (what to call)

| Goal | Tool |
|------|------|
| Size later reads to your model window | `occam_client_capabilities` |
| Is this URL worth fetching? | `occam_probe` |
| Read **one** page | `occam_transcode` |
| Read **several** URLs | `occam_digest` (not N× transcode) |
| List site links | `occam_map` |
| Search the open web | `occam_search` (needs `OCCAM_SEARCH_PROVIDER`) |
| Typed fields from a playbook | `occam_extract_knowledge` |
| Prove a receipt / check a claim | `occam_verify` · `occam_claim_check` · `occam_attest` |

Opt-in (env-gated): batch, watch, crosscheck, failure atlas, browser interact — [experimental](docs/experimental.md).

---

## Token knobs (not a “compression codec” picker)

There is **no** public MCP `codec=` / gzip-style algorithm switch. Live output is Markdown. Shrink context with:

| Knob | Effect |
|------|--------|
| `occam_client_capabilities(context_tokens=…)` | Ambient ~20% output budget |
| `max_tokens` / `fit_markdown` + `focus_query` | Cap / BM25 prune |
| `compact_links` / `include_media_refs` | Less link/media noise |
| `json_blocks` + `rank_blocks` | Citation spans + salience |
| `if_none_match` / `diff_against` | Skip unchanged / send deltas |

---

## Measured proof (one fixture)

Controlled article + chrome → compact Markdown. **5,297 → 1,736 UTF-8 bytes (67.2% fewer in this fixture).** Bytes, not tokens — not a universal benchmark.

![Before/after](docs/assets/occam-proof-before-after-rc4.png)

[Input](https://contextforgeai.github.io/occam/examples/current-proof/representative-input.html) · [Output](docs/examples/current-proof/representative-output.md) · [Method](docs/examples/current-proof/representative-measurement.json)

---

## Trust limits (do not overclaim)

| Claim | Reality |
|-------|---------|
| `ok:false` | Content **unknown** — never substitute training memory |
| Receipts | Integrity **relative to a key** — not truth / identity / trusted time |
| Crosscheck | Comparison — **not** consensus proof |
| npm | Experimental RC — **not** GA |
| Cosign | Release authenticity under policy — **not** page-content truth |
| CAPTCHA | Detected — **not** solved |

[Trust & Safety](docs/trust-and-safety.md)

---

## Go deeper

| Link | For |
|------|-----|
| [Why Occam](docs/why-occam.md) | Advantages + every common knob |
| [Documentation hub](docs/index.md) | Site entry / landing |
| [Quick Start](docs/quick-start.md) | Install → connect → first read |
| [Choosing a tool](docs/choosing-a-tool.md) | Task → tool table |
| [Tools reference](docs/tools-reference.md) | Compact param tables |
| [MCP API](MCP_API_SPEC.md) | Normative response contract |
| [Configuration](docs/configuration.md) | Env vars |
| [Troubleshooting](docs/troubleshooting.md) | Symptom → fix |
| [AGENTS.md](AGENTS.md) | Contributor / agent repo rules |

License: [AGPL-3.0-or-later](LICENSE).
