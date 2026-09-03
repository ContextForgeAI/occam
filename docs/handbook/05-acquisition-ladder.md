# Chapter 5 — Acquisition: the real ladder

**Part B — Reference path** · **Spine chapter** · Prerequisites: [Ch 2](02-honesty-contract.md), [Ch 4](04-request-path.md) · Next: [Chapter 6](06-when-acquisition-is-hard.md)

---

## Mental model

**A gated ladder with exits, not a cascade.**

Under `http_then_browser`, Occam tries HTTP first, conditionally escalates to Chromium, and may call an operator-configured **managed** provider only after **both local backends fail**. Termination is first-class: 404/410 and failed HTTP on **public-reference** URLs skip browser entirely. When HTTP and browser both fail, the surfaced failure is chosen by **informativeness rank**, not markdown length—and **managed failure never wins the surface**.

`managed` is **not** a `backend_policy` value.

---

## Explanation

### Backend policies

| Policy | Behavior |
|--------|----------|
| `http` | HTTP worker only (~35s timeout) |
| `browser` | Playwright Chromium only (~60s default browser timeout) |
| `http_then_browser` | Ladder below |

Aliases like `http-then-browser` normalize to the same enum.

### Locked ladder contract (`http_then_browser`)

Per `PHASE6-ACQUISITION-CONTRACT.md` and `OccamRouter`:

1. **HTTP attempt** — Run HTTP extract backend.
2. **HTTP usable success → STOP** — Non-empty markdown, not equivalently thin, not a short challenge page → return HTTP result.
3. **Thin / short-challenge HTTP → escalate** — Unusable HTTP triggers browser rung.
4. **Terminal HTTP (404/410) → STOP** — No browser.
5. **Public-reference failed HTTP → STOP** — Wikipedia/RFC-style tiers skip browser **silently** (looks like ordinary HTTP failure; no "we chose not to escalate" flag).
6. **Browser attempt** — When escalation conditions met.
7. **Browser usable success → STOP**
8. **Dual local failure → rank** — `FailureRanking.Informativeness` picks HTTP vs browser failure for the surface (e.g. `http_403` rank 100 beats browser `timeout` rank 50). There is **no** third-party managed scrape rung.

### Post-processors (after a backend returns)

Ordered pipeline may downgrade success:

- Challenge/captcha → `captcha_or_challenge`
- Login wall → `requires_login`
- Thin DOM → `thin_extract`

These run before materialization; they interact with [Chapter 2](02-honesty-contract.md) quality semantics.

### Reading `recovery[]`

Each entry typically records backend, outcome, and escalation reasons. Use it to answer: "Was browser ever tried?" and "Why did the ladder stop?"

Task R ladder flavors:

- Static docs page — HTTP success, stop at rung 1.
- `/v2/` 404 — terminate, no browser.
- JS playground — HTTP thin → browser with escalation reason.
- Partner 403 — dual fail → surface `http_403` by rank.

---

## CHECK

**NETWORK** — Three URL classes.

**(a) 404 termination**

- Transcode a URL known to 404.
- Assert: `ok:false`, `http_404` (or equivalent), **no browser** entry in `recovery[]`.

**(b) Public-reference short-circuit**

- Force HTTP failure on a public-reference page (e.g. Wikipedia) if your corpus includes one— or use gate-documented fixture.
- Assert: no browser attempt despite `http_then_browser`.

**(c) SPA escalation**

- Transcode a client-rendered SPA with defaults.
- Assert: browser attempt present with escalation metadata.

---

## Common misconception

**"It always tries HTTP, then browser, then managed, and picks whichever produced more text."**

Wrong on four counts: 404/410 terminate; public-reference failed HTTP terminates; dual-fail winner is **informativeness rank**, not density; managed failure never wins the surface.

---

## Limitations

- Public-reference skip has **no explicit signal**—diagnosis by elimination ([Chapter 25 — Diagnosing bad results](25-diagnosing-bad-results.md)).
- Domain tier `http_only` is probe-advisory; it does **not** disable browser in the router.
- No CAPTCHA solving; no fingerprint rotation.
- Browser default timeout is **60s** in current code—not legacy 120s prose.
- Private IP targets refused unless `OCCAM_ALLOW_PRIVATE_URLS=1` (policy differs for some tools—[Chapter 13](13-typed-field-extraction.md)).

---

## Links

**Public docs:** [Concepts](../concepts.md) (backends) · [Failure codes](../failure-codes.md) · [Tools reference](../tools-reference.md) (`occam_transcode`, `backend_policy`)

**Next chapter:** [Chapter 6 — When acquisition is hard](06-when-acquisition-is-hard.md)
