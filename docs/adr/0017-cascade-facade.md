# ADR-0017 — Cascade facade `occam(url, task, budget)`

**Status:** Accepted (engine + MCP + CLI)
**Date:** 2026-09-16
**Hypothesis:** [H2](../research/hypothesis.md) (progressive disclosure)

## Context

Fifteen specialised tools are correct for a strong agent and hostile to a weak one. ADR-0016 sizes
the *surface*; this ADR defines the *one tool* a weak surface keeps — a cascade that still does
playbook overlay, HTTP→browser escalation, focus/budget, and a content-hash stamp, without asking
the client to pick among opt-ins.

## Decision

**Public facade:** MCP tool `occam` with parameters `url` (required), `task?`, `budget?`,
`mode=auto|advanced`.

**Internal ladder** (each step has its own timeout; total budget is also capped):

1. `playbook_resolve` — soft-fail; continue without overlay on miss/timeout
2. `http_extract` — primary body
3. `browser_fallback` — only when HTTP did not produce a usable body
4. `focus_budget` — maps `task`→`focus_query`+fit and `budget`→`max_tokens` (recorded even when
   applied as options to the extract)
5. `receipt` — content-hash when receipts are enabled; otherwise skipped honestly

**Graceful degradation:** a timed-out step is `omitted` (listed in `omitted[]`); if a later step
still yields a body, the response is `ok:true` with `partial:true`. Both extract stages failing
yields `ok:false` with a typed `failure.code` and the full step log.

**Profiles:**

| Profile | Cascade |
|---|---|
| `minimal` | only `occam` |
| `basic` | `occam` + digest + search |
| `reader`+ | `occam` alongside `occam_transcode` (full opt-ins) |
| `full` | all sixteen core tools |

**`mode=advanced`:** still runs the cascade; response may include a `hint` that specialised tools
live on wider profiles. It does **not** dynamically enlarge `tools/list` (see ADR-0016 —
`list_changed` is not wired).

## Consequences

- Core catalog grows 15 → **16**; default reader 8 → **9**.
- Exam prompts teach `occam` / `task`+`budget`; the grader also accepts the specialised
  `focus_query`+`max_tokens` aliases.
- CLI `cascade selftest|run` proves the state machine without MCP.
- `occam_transcode` remains the power reader; cascade does not re-implement every opt-in.

## Not decided here

- Mid-session surface changes via MCP `list_changed` (blocked by DI registration; ADR-0016).
- Signing a full Receipt v1 envelope inside the cascade (content-hash stamp only for now).
