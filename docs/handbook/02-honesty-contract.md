# Chapter 2 — The honesty contract: `ok:false` means unknown

**Part A — Orientation** · **Spine chapter** · Prerequisites: [Chapter 1](01-what-occam-is.md) · Next: [Chapter 3](03-standing-up-an-install.md)

---

## Mental model

**A refusal is an answer.**

`ok:false` is a typed statement that page content is **UNKNOWN to the system**. It is not "empty page," not "short page," and not permission to recall from model memory. The product makes honesty *possible*; only the caller can make it *mandatory*—no mechanism detects memory substitution.

---

## Explanation

### The trust rule

When `ok:false`, the correct action is to read:

| Field | Use |
|-------|-----|
| `failure.code` | Typed reason (`http_404`, `thin_extract`, `requires_login`, …) |
| `quality.verdict` | Post-extraction quality measurement when partial content exists |
| `confidence` | Host-side confidence signal (not truth) |
| `recovery[]` | Which backends were attempted, in order, with reasons |
| `agentMeta.decisions` | Host routing and compile decisions |

Never summarize the page from memory on failure. Downstream tools (`claim_check`, `attest`, `verify`) cannot fix content that never entered Occam—and worse, a response may carry receipts from *other* URLs that succeeded in the same session.

### Failure vs quality

Two traps the design explicitly guards:

**`thin_extract` ≠ short page**

- `thin_extract` — **bad extraction** (chrome, shell, near-empty DOM). Correct actions: different backend, session, playbook heal—not "accept as complete."
- `short_quality` — **good extraction of a genuinely short page** (`ok:true`). Do not heal or escalate merely because the body is small.

Task R contrast: a marketing landing page returns `ok:false` / `thin_extract`; a three-sentence changelog returns `ok:true` / `short_quality`. Same length band, opposite meaning, opposite next step.

### Signed failures

A **negative receipt** can sign that a provable wall was hit (`captcha_or_challenge`, `requires_login`, `http_403`, `http_404`, `http_410`, etc.). That signature proves the **failure was claimed** by this install's key—not that content was obtained, not that the page is inaccessible to everyone else.

### Signal classes (do not conflate)

| Signal | When | Meaning |
|--------|------|---------|
| `extractability` (probe) | Before fetch | Prediction; HTTP-only probe never escalates |
| `confidence` / `quality` | After fetch | Measurement on what was extracted |
| Playbook `verify.score` | Authoring gate | 0–100 heuristic; v2 signed as tamper-evident snapshot, not quality proof |

### Known misleading code

Probe currently maps SSRF-policy refusals to `network_error` (GAP-003). An agent may retry a private URL forever unless it reads policy context. Treat unexpected `network_error` on internal targets with suspicion.

---

## CHECK

**NETWORK** — Typed termination vs escalation.

1. `occam_transcode` a known **404** URL (e.g. append `/this-path-does-not-exist-occam-handbook` to a stable site).
   - Expect: `ok:false`, `failure.code` ≈ `http_404`, **no browser attempt** in `recovery[]`.
2. `occam_transcode` a known **JS-heavy SPA** (client-rendered app shell) with default `backend_policy`.
   - Expect: HTTP attempt first, then browser attempt with an escalation reason in `recovery[]`.

If the 404 triggers a browser launch, the ladder description in [Chapter 5](05-acquisition-ladder.md) is wrong for your build—report it.

---

## Common misconception

**"`thin_extract` means the page was short."**

It means the **extraction** was bad. A complete short page is `ok:true` with `quality.verdict=short_quality`. Healing or browser escalation on `short_quality` wastes resources and can degrade good results.

---

## Limitations

- Occam cannot enforce caller honesty. If the agent ignores `ok:false`, the trust layer becomes decoration.
- Not every cause has a visible signal ([Chapter 25 — Diagnosing bad results](25-diagnosing-bad-results.md): public-reference browser skip looks like ordinary HTTP failure; some TSA failures vanish silently).
- Probe/map/search outputs are **unsigned**—predictions and listings, not verifiable extracts.
- `occam_verify` verdicts are about bytes and keys, not truth ([Chapter 14](14-what-a-receipt-proves.md)).

---

## Links

**Public docs:** [Failure codes](../failure-codes.md) · [Trust & Safety](../trust-and-safety.md) · [Choosing a tool](../choosing-a-tool.md) (trust rule in tool guidance)

**Next chapter:** [Chapter 3 — Standing up an install](03-standing-up-an-install.md)
