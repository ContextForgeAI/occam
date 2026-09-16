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

One recommended route: the signed GitHub Release bootstrap (**host 1.2.0**).
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
| GitHub Release bootstrap | Host + `occam` CLI + Cosign verify | **Recommended (GA host 1.2.0)** |
| `npx ff-occam@1.2.0` | MCP host only — no `connect` / `doctor` | Experimental |

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

## Proof of read (experimental)

Everything above is about giving an agent good context. This part is about the
next question: **did the agent actually read it?**

A fluent summary is no evidence of a read. Content hashes prove what the
*fetcher* received; citations prove a span exists in a document the *pipeline*
holds. Neither says the bytes reached the model's context before it answered.

Occam ships a protocol for that. A probe endpoint serves a page carrying a
**sentinel** — `HMAC-SHA256(secret, bucket ‖ sessionId)`, where
`bucket = floor(unixSeconds / 300)`. The agent is asked to report it. The
sentinel cannot be guessed, was not in any training set, and expires. So the
report is checkable, and it resolves to one of four states:

| Verdict | Meaning |
|---------|---------|
| `READ_VERIFIED` | Authentic, issued by this host, inside the fresh window |
| `READ_STALE` | Authentic and issued — but from an older bucket. The read was real, just not now |
| `REPLAY_SUSPECT` | Cryptographically valid, yet this host never recorded issuing it |
| `HALLUCINATED` | Matches nothing in the recognised window |

Run it yourself — no network, no browser, no worker tree:

```bash
occam canary selftest   # 17 assertions over the state machine    -> CANARY_SELFTEST_OK
occam canary smoke      # HTTP: issue, read, verify, reject        -> CANARY_SMOKE_OK
occam canary vectors --verify docs/testing/canary-vectors.json  #  -> CANARY_VECTORS_OK
```

**What `READ_VERIFIED` proves:** the sentinel transited from the endpoint into
whatever reported it, inside the time window, and the host has a record of
serving it.

**What it does not prove:** comprehension; that the *whole* page was read (a
sentinel is a spot check, not coverage); that a human did not relay the value;
anything at all about a different URL.

Specification, threat model and test vectors:
[PROBE_PROTOCOL.md](PROBE_PROTOCOL.md). Design rationale and the alternatives
that were rejected: [ADR-0010](docs/adr/0010-proof-of-read-canary.md).

Verified on macOS arm64, Linux x64 and Windows x64 — 149 tests, byte-identical
sentinel derivation (same SHA-256 for the emitted vector file on all three),
Native AOT binaries re-checked against the same vectors. Logs, not assurances:
[docs/testing/RESULTS.md](docs/testing/RESULTS.md). Not verified: Linux arm64,
macOS x64, musl, FreeBSD — [status.md](docs/testing/status.md).

## Capability-sized tool surfaces (experimental)

Sixteen tools is a lot to choose from. The failure that causes is not bad tool
*use* — it is bad tool *selection*: a weak model reaches for
`occam_playbook_heal` when it wanted to read a page, then picks its next step
from the same confused state.

So the surface can be sized to measured capability. Four checkable tasks, one
point each:

| Task | Measures | Passes when |
|------|----------|-------------|
| `canary` | whether the agent reads at all | the reported sentinel verifies as a current read |
| `basic_call` | schema binding | arguments parse to an object with an absolute http(s) `url` |
| `focus_budget` | context-budget awareness | a non-empty `task`/`focus_query` plus a plausible `budget`/`max_tokens` |
| `chain` | multi-step state handling | a later call carries the exact value an earlier one produced |

Score 0–1 → **1 tool** (`occam`). Score 2–3 → **3 tools**. Score 4 → **all 16**. A client
that never sat the exam is treated as the middle tier: starving a capable agent
is a silent failure, while over-trusting a weak one is visible and recoverable.

The `canary` task is the anchor. The other three check *form*, which a model can
satisfy by pattern-matching a schema; the canary cannot be satisfied that way,
because its answer did not exist at training time.

```bash
occam exam selftest          # 36 assertions over grading, tiering and hysteresis
occam exam tasks --out t.json  # the catalogue, for a harness that administers it
```

**What is wired up:** grading, tiering, the tier → surface mapping, six
`OCCAM_PROFILE` values exposing 1/3/8/9/12/15 tools, TTL-bounded result caching,
and rolling competence scoring with hysteresis and a reported oscillation count.

**What is not:** the host does not administer the exam. MCP has no general
mechanism for a server to hand a client an arbitrary task and await a result, and
the tool surface is fixed when the MCP server is built — so a tier cannot narrow
or widen a live session. Today an operator sets `OCCAM_PROFILE`. Full accounting:
[ADR-0016](docs/adr/0016-capability-exam.md).

## This is research, not only a parser

The extraction path is a product and is measured as one. The proof-of-read
layer is a **hypothesis with an instrument attached**, and the honest status is:

> The instrument exists and is verified on three platforms.
> **No experiment has been run with it.**

Three falsifiable claims, each with the observation that would kill it:

| | Hypothesis | Status |
|---|---|---|
| **H1** | A proof-of-read canary detects — and then reduces — fabricated page content | Instrument built and verified; no dataset |
| **H2** | Narrowing 15 tools to 1 raises task success for weak agents, and mildly *lowers* it for strong ones | All four surfaces ship (1/3/8/15 tools); experiment not run |
| **H3** | Tiering an agent from observed behaviour predicts success better than self-report or a model-name lookup | Engine built and verified; not administered, not measured |

H3 is the most likely to fail, and the most useful to know: if a model-name
table performs as well, the scoring machinery is complexity for nothing — and
the right response is to delete it, not to tune it.

The cheapest first data point is H2: four `OCCAM_PROFILE` values, 60
programmatically checkable tasks, no human labelling, **no new code**.

[Hypotheses](docs/research/hypothesis.md) ·
[Methodology](docs/research/methodology.md) ·
[Related work](docs/research/related_work.md) ·
[Paper skeleton](docs/research/paper_draft.md)

The related-work file marks every entry `[verified]`, `[concept]` or
`[unverified]`. Four literature searches are open, and the novelty claim is
unsupported until they close. That is stated there rather than implied away.

### Where the ideas came from

The author is a Unity/C# game developer, not an ML researcher. These designs
are transplanted game-engineering patterns, and the transplant is the part
under test:

| Game pattern | Occam mechanism | Where the analogy breaks |
|---|---|---|
| Anti-cheat: never trust the client's claim, validate against server state | Canary: validate a read against a server-derived token | A cheater adapts; a hallucinating model is not trying to beat the check. Detection is easier — deterrence may be impossible for the same reason. |
| Replay detection | `REPLAY_SUSPECT` | In a multi-agent system, one agent reading and another reporting is legitimate, and this verdict mislabels it. |
| Tutorial gating | Capability exam → tool tier | A player improves; a model's capability is fixed within a session. |
| Level of detail | Cascade: cheap HTTP first, escalate on demand | LOD has a ground-truth error metric in pixels. "Good enough extraction" has none. |

## How this differs from extraction services

Design differences, **not** a benchmark. No head-to-head numbers are claimed
here; the only measured comparison in this repository is the pinned WRB run
above.

| | Occam | Hosted extraction APIs (e.g. Firecrawl, Jina Reader) | Library extractors (e.g. Trafilatura, Readability) |
|---|---|---|---|
| Where it runs | Your machine, single static binary | Vendor's cloud | Your process, as a library |
| Page content leaves your network | No | Yes | No |
| On failure | Typed `ok:false` + failure code | Varies by provider | Usually empty or partial output |
| Token budget as a first-class input | Yes (`max_tokens`, omission manifest) | No | No |
| Signed receipt over what was fetched | Yes (Receipt v1) | No | No |
| Proof that content reached the *model* | Yes (this protocol) | No | No |
| MCP tool surface | 15 tools, role-scoped profiles | Usually one endpoint | Not an agent surface |
| Cost model | Your CPU | Per request | Your CPU |

The row that motivated this project is the second-to-last one. The others are
engineering trade-offs; that one is a category nobody else populates.

## Trust limits

| Claim | Reality |
|-------|---------|
| `ok:false` | Content **unknown** — never substitute training memory |
| Receipts | Integrity **relative to a key** — not truth or trusted time |
| Smaller output | Not automatically a better answer |
| npm | Experimental package **1.2.0** — not the GA host channel |
| Cosign | Release authenticity — not page-content truth |
| CAPTCHA | Detected — **not** solved |
| `READ_VERIFIED` | Content **arrived** — not that it was understood, nor that the whole page was read |
| Proof-of-read effect on hallucination | **Unmeasured.** The instrument is built; no experiment has been run |

[Trust & Safety](docs/trust-and-safety.md)

## Looking for co-authors, not users

The proof-of-read layer needs people who will argue with it, not install it.
Concretely useful right now:

- **An MCP developer** — the single-entry cascade and capability-gated tool
  disclosure (H2) are specified and unimplemented. See
  [the roadmap](docs/roadmap.md).
- **An ML engineer or researcher** — [the experiment](docs/research/methodology.md)
  is designed and pre-registered in shape, and not run. The labelling scheme
  is the weak point and needs someone who has done inter-rater work before.
- **A security reviewer** — [PROBE_PROTOCOL.md §7](PROBE_PROTOCOL.md) is a
  threat model written by its own author, which is the least reliable kind.
- **A technical writer** — the specification is dense and the analogies are
  load-bearing; both could be much clearer.

The most valuable contribution is a reason the hypotheses are wrong. If the
canary measures tokenisation rather than reading, that finding is worth more
than the feature, and it will be published as such.

Start with [CONTRIBUTING.md](CONTRIBUTING.md), or open an issue with the
`research-proposal` template.

## Citation

```bibtex
@software{occam_mcp,
  title   = {Occam: a proof-of-read layer for MCP agents},
  author  = {{Ebony Swan}},
  year    = {2026},
  version = {1.2.0},
  url     = {https://github.com/ContextForgeAI/occam},
  note    = {Proof-of-read canary protocol: PROBE_PROTOCOL.md}
}
```

There is no paper yet, so there is no paper to cite. Protocol-level entry and
the normative RFC references: [BibTeX](docs/research/BibTeX.md).

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
