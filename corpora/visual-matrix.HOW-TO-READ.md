# Visual QA — golden path (6 cases)

Copied into each run at `artifacts/l0-runs/<timestamp>/`.

## Product in 5 minutes — K1 / K2 / K3

| Killer | What the matrix checks |
|--------|-------------------------|
| **K1** Honest compiler | `golden-transcode-live`, `golden-probe-404`, typed failures |
| **K2** Token contract | `golden-transcode-budget`, `golden-transcode-selectors` |
| **K3** Recipe A | `golden-recipe-a` — probe → transcode |

Matrix scope: K1–K3 only (6 cases).

## Quick start

1. Open **`index.html`** in a browser.
2. Left pane — **6 cases** in three categories.
3. Tabs: Preview · Source · JSON · Meta (recipe_a: Probe + Transcode).

## Six cases

| id | Category | Meaning |
|----|----------|---------|
| `golden-probe-ok` | occam_probe | Page OK → proceed to transcode |
| `golden-probe-404` | occam_probe | Missing page → stop |
| `golden-transcode-live` | occam_transcode | Baseline live extract (K1) |
| `golden-transcode-budget` | occam_transcode | max_tokens + fit_markdown + focus_query (K2) |
| `golden-transcode-selectors` | occam_transcode | content_selectors (K2) |
| `golden-recipe-a` | recipe_a | probe → transcode (K3) |

## What to look for

### Probe

- Success: `ok: true`, `agentHints.suggestedNextTool` ≈ `occam_transcode`
- 404: `ok: false`, `failureCode: "http_404"` — no invented markdown

### Transcode

- Success: non-empty markdown, `backend` = `http` or `browser`
- K2: truncation / prune visible in Preview
- Failure: typed `failure.code`, `agentMeta.decisions` (`stop`, `retry_transcode`, …)

### Recipe A

1. `01-probe.json` — backend recommendation
2. `02-transcode` — content matches probe

## Re-run

```powershell
$env:OCCAM_HOME = (Get-Location).Path
.\scripts\run-visual-matrix.ps1 -Open
```

---

*Run id: {{RUN_ID}} · Cases: {{CASE_COUNT}} · Generated: {{GENERATED_AT}}*
