# Occam

Stop filling your agent's context with webpage noise.

Occam reads live pages and returns context focused on your task, with source
links, a token budget, and explicit omissions. Read documentation and compare
sources from a local MCP server.

[![CI](https://github.com/ContextForgeAI/occam/actions/workflows/ci.yml/badge.svg)](https://github.com/ContextForgeAI/occam/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-AGPL--3.0-blue)](LICENSE)

- **Task-focused Markdown** from a live URL — chrome and navigation stripped.
- **A declared budget** and a machine-readable record of what was omitted.
- **Typed `ok:false`** when the page is unknown. Never a silent empty shell.

![A webpage reduced to source-linked Markdown](docs/assets/occam-proof-before-after-rc4.png)

One inspectable fixture (2026-08-28): 5,297 HTML bytes become 1,736 Markdown
bytes. Not an average and not a token claim.
[Input](https://contextforgeai.github.io/occam/examples/current-proof/representative-input.html)
· [Output](docs/examples/current-proof/representative-output.md)
· [Method](docs/examples/current-proof/representative-measurement.json)

## A real documentation question

**Prompt**

```text
Use Occam to read https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide/Functions
Show how function scope and closures work. Include the syntax I need, any
conditions, and source links. Tell me if relevant content was omitted.
```

**Captured result** (2026-09-07, GitHub Release **v1.1.1**,
`occam_transcode` with `backend_policy:http`, `fit_markdown:true`,
`focus_query:"function scope closures"`, `max_tokens:800`,
toolchain `ff-occam/1.1.1`):

```text
We also refer to the function body as a _closure_. A closure is any piece of
source code (most commonly, a function) that refers to some variables, and
the closure "remembers" these variables even when the scope in which these
variables were declared has exited.

## Function scopes and closures
Functions form a scope for variables — variables defined inside a function
cannot be accessed from anywhere outside the function. …

function multiply() {
  return num1 * num2;
}

console.log(multiply()); // 60

function getScore() {
  const num1 = 2;
  const num2 = 3;
  function add() {
    return `${name} scored ${num1 + num2}`;
  }
  return add();
}

<!-- SNIP: 16 unchosen (reason: budget_exceeded) -->
```

Source: https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide/Functions

`ok:true`, `focus:hit`, `completeness:partial` (`context_truncated`),
`compile.omitted.tokensDropped:6172` (18 sections). The host kept the
on-topic definition and example, then said what it dropped instead of
inventing the rest. The same prompt at `max_tokens:128` is recorded
separately — the `multiply()` example does not fit.
Settings, hashes, and both budget outcomes:
[Understand a documentation instruction](docs/examples/golden-workflows/understand-instruction/).

This capture is the published **v1.1.1** win-x64 host, not a claim that
every later Release is byte-identical.

## Install

One recommended route: the signed GitHub Release bootstrap (**host 1.1.1**).
It installs the host, puts `occam` on your PATH, and runs `occam connect`.

<details>
<summary>Windows</summary>

```powershell
irm https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.ps1 | iex
```

</details>

<details>
<summary>Linux x64 / macOS Apple Silicon</summary>

```bash
curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh | bash
```

</details>

Then open a new conversation and use the prompt above, or from a terminal:

```bash
occam read https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide/Functions --focus "function scope closures" --fit --max-tokens 800
```

Success is cited Markdown **or** a typed `ok:false` — never a guessed page.

| Channel | What you get | Status |
|---------|--------------|--------|
| GitHub Release bootstrap | Host + `occam` CLI + Cosign verify | **Recommended (GA host 1.1.1)** |
| `npx ff-occam@1.1.1` | MCP host only — no `connect` / `doctor` | Experimental |

Published RIDs: `win-x64`, `linux-x64`, `osx-arm64`.
[INSTALL.md](INSTALL.md) ·
[installation safety](docs/trust/installation-safety.md).

## Three recipes

1. **Understand a documentation instruction** — one
   [`occam_transcode`](docs/tools/occam_transcode.md) call with `focus_query`
   and a budget. Ask for the command, steps, conditions, citations, and
   omissions. [Recorded run](docs/examples/golden-workflows/understand-instruction/).
2. **Compare known sources** — one
   [`occam_digest`](docs/tools/occam_digest.md) over several URLs, not N
   separate reads. [Recorded run](docs/examples/golden-workflows/compare-sources/).
3. **Inspect changes since a previous read** — store `contentHash`, pass it as
   `if_none_match` (optional: `diff_against` for a block delta).
   [Recorded run](docs/examples/golden-workflows/inspect-changes/).

Gallery (prompts + named builds):
[workflow gallery](docs/examples/gallery.md) ·
[release evidence](docs/examples/release-evidence.md).
The same three jobs as folders (excerpts, sources, omissions, wrapper
budget): [`occam pack`](docs/examples/context-packs/) — CLI over existing
tools, not a new MCP tool.
Missed a command or citation? [Feedback template](docs/examples/feedback.md).

Open-web discovery works without extra config:
[`occam_search`](docs/tools/occam_search.md) defaults to keyless DuckDuckGo
HTML and discloses `provider`. Set `OCCAM_SEARCH_PROVIDER=off` for air-gap.

## Why Occam

- **Budget control** — `max_tokens` or an ambient client window; omitted
  regions are listed, not silently deleted.
- **Structured materialization** — same extract, different shapes (focus,
  tables, blocks, deltas).
- **Source-linked evidence** — URLs, optional signed receipts, claim checks.
  Integrity relative to a key is **not** the same as truth.
- **Local execution** — HTTP, then browser if needed; typed refusal when both
  fail. No third-party scrape escalation.
- **Explicit failure** — `ok:false` means the page is unknown.

## Measured results

One pinned 48-URL WRB **fetch** run on 2026-08-30, one machine and network.
WRB assigns its own difficulty tiers (n = 19 / 16 / 13). Not a universal
success rate.

| WRB fetch observation | Occam |
|-----------------------|------:|
| Tier 1 retrieval | 100.0% (19/19) |
| Tier 2 retrieval | 75.0% (12/16) |
| Tier 3 retrieval | 38.5% (5/13) |
| Overall retrieval | 36/48 (75.0%) |
| False-positive rate | 0.0% |
| Successful fetch p50 / p90 | 630 ms / 1,973 ms |

A 2026-09-05 fetch-only re-check on the same pin kept **36/48** and 0%
false positives; success-conditioned p50/p90 were slower (2141 / 4359 ms).
A 2026-09-07 DonSeTch 3.6.7 fetch-only arm on the same machine scored
**42/48**. The six-URL gap is gated T2/T3 acquisition, not Tier-1 docs.
No parity claim. Decision:
[Q2 WRB](docs/examples/capability-eval/q2-wrb/).
Pin, adapter limits (`chars/4`, crawl = map discovery proxy), misses, and
reproduction: [`scripts/bench/README.md`](scripts/bench/README.md).

## Trust limits

| Claim | Reality |
|-------|---------|
| `ok:false` | Content **unknown** — never substitute training memory |
| Receipts | Integrity **relative to a key** — not truth or trusted time |
| Smaller output | Not automatically a better answer |
| npm | Experimental package **1.1.1** — not the GA host channel |
| Cosign | Release authenticity — not page-content truth |
| CAPTCHA | Detected — **not** solved |

[Trust & Safety](docs/trust-and-safety.md)

## Go deeper

[Why Occam](docs/why-occam.md) ·
[Documentation hub](docs/index.md) ·
[Quick Start](docs/quick-start.md) ·
[Choosing a tool](docs/choosing-a-tool.md) ·
[MCP API](MCP_API_SPEC.md) ·
[llms.txt](llms.txt) ·
[AGENTS.md](AGENTS.md)

The product name is **Occam**. Package and MCP server identity stay
`ff-occam`.

License: [AGPL-3.0-or-later](LICENSE).
