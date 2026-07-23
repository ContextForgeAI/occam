# Corpora — gate data, audits, and eval inputs

Committed corpora feed **gates**, **smoke tests**, **eval harness**, and **quality audits**.
Most sprint prompts and raw audit report dumps stay local-only (gitignored).

---

## Always committed (do not delete)

| Path | Purpose |
|------|---------|
| `l0-smoke.jsonl` | L0 smoke URLs |
| `l1b-probe.jsonl` … `l9-golden.jsonl` | Gate tier corpora (see filenames) |
| `l2-egress.jsonl` | L2 egress gate fixtures |
| `l3-heal-learn.jsonl` | Heal/save gate |
| `l4-genome.jsonl` | Genome + extract gate |
| `rc1-regression.jsonl` | RC.1 compatibility regression |
| `quality-audit-rotation.jsonl` | Tier-3 agent audit rotation (human-oriented) |
| `quality-audit-wildcard*.jsonl` | Wildcard digest audit inputs |
| `agent-popular-hosts.jsonl` | `run-agent-popular-hosts.mjs` corpus |
| `popular-hosts-priority.json` | Notes/status for popular-host audit triage (referenced by `quality-audit-agent.PROMPT.md`) |
| `agent-tool-cheatsheet.json` | Host wizard / agent hints |
| `occam-host-wizard-manifest.json` | Onboarding manifest |
| `eval-harness/` | npm eval runner + machine-oriented rotation jsonl |
| `visual-matrix.HOW-TO-READ.md` | Visual matrix HTML report legend |
| `traps.jsonl`, `discovery-focus.jsonl`, … | Optional / extended gate corpora |

---

## Active prompts (committed)

| File | Use |
|------|-----|
| `quality-audit-agent.PROMPT.md` | Tier-3 full quality audit |
| `quality-sprint-wide-cursor-desk.PROMPT.md` | Windows Cursor desk replay (`desk-recipe-*.mjs`) |

---

## Quality baseline (public)

Public, reproducible quality claims live in
[docs/quality-baseline.md](../docs/quality-baseline.md).

Raw dated audit report markdown under `quality-audit-reports/` is **optional private
evidence** and is not required to build, test, or contribute from a clean public clone.
New runs should write drafts under `artifacts/quality-audit/<timestamp>/` (gitignored).

---

## Rotation corpora (two files on purpose)

| Path | Audience |
|------|----------|
| `quality-audit-rotation.jsonl` | Agent / human audit prompt |
| `eval-harness/quality-audit-rotation.jsonl` | Eval harness (`expectedOutcome` rows) |

Do not merge them without updating both consumers.

---

## Bench / optional corpora

| Path | Notes |
|------|-------|
| `bench-1k.jsonl` / `bench-2k.jsonl` | Maintainer bench (`scripts/bench/`) |
| `no-vpn-bench.jsonl` | Crawl4ai comparison bench |
| `agent-mvp-latency.jsonl` | MVP latency check |
| `heavy-browser-escalation.jsonl` | Browser escalation bench |

---

## Maintainer scripts (related)

| Script | Role |
|--------|------|
| `scripts/run-l0-fast.ps1` | Fast gate |
| `scripts/hermes-smoke.mjs` | Hermes MCP smoke |
| `scripts/run-agent-popular-hosts.mjs` | Popular hosts honest outcomes |
| `scripts/run-agent-mvp-gate.mjs` | Chains smoke + popular hosts |
| `scripts/desk-recipe-r.mjs`, `scripts/desk-recipe-e-heal.mjs` | Cursor desk replay |
| `scripts/run-wide-cursor-desk.ps1` | Full desk orchestration |
