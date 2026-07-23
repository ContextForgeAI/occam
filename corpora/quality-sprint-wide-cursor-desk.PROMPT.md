# FF-Occam MCP — wide Cursor desk validation (pre multi-device)

> **Status: ACTIVE** — run on **Windows + Cursor** before Hermes migration  
> **Prerequisite:** v0.8.10-media-refs · eight MCP tools · doctor green  
> **Normative quality audit:** [`quality-audit-agent.PROMPT.md`](quality-audit-agent.PROMPT.md) § Copy-paste — full eight-tool audit (Части 1–3)  
> **Next:** Linux Hermes · macOS OpenClaw embed · OpenRouter answers

**Repo:** `c:\PROJECTS\FFOccamMCP`

**Mission:** maximum **host-realistic** validation on Cursor — gates + Recipe R (K9) + heal desk — artifacts for replay on clean Hermes host.

---

## Run (one command)

```powershell
cd c:\PROJECTS\FFOccamMCP
$env:OCCAM_HOME = (Get-Location).Path
.\scripts\occam-doctor.ps1          # if stale workers
# Reload MCP Servers in Cursor (ff-occam)
.\scripts\run-wide-cursor-desk.ps1    # full (~15–40 min live corpora)
# or faster:
.\scripts\run-wide-cursor-desk.ps1 -Fast
```

**Artifacts:** `artifacts/wide-cursor-desk/<date>/`

| Subfolder | Content |
|-----------|---------|
| `recipe-r/` | `inspector-session.json`, `cursor-rag-transcript.md` (K9) |
| `recipe-e-heal/` | heal hint desk |
| `summary.json` | phase pass/fail |
| `WIDE-CURSOR-DESK.md` | human summary |

---

## What this proves

| Layer | Harness | Pass |
|-------|---------|------|
| Unit + corpora | `l0-gate` (unless `-SkipGate`) | `L0_GATE_OK`, `L2_*`, `L3_*`, `L4_*`, `L2_MEDIA_REFS_OK` |
| RAG chain | `desk-recipe-r.mjs` **or** MCP Recipe R (audit § Часть 3) | K9 rubric + `mediaRefs` on transcode/digest |
| Heal trust | `desk-recipe-e-heal.mjs` **or** MCP heal/save (audit § Часть 2 шаг 5–6) | heal hint on eligible fail; no hint on terminal |
| Full eight-tool | [`quality-audit-agent.PROMPT.md`](quality-audit-agent.PROMPT.md) | Merge PASS + Eight-tool PASS + Recipe coverage |

---

## K9 Recipe R (normative)

1. `occam_playbook_resolve` before map/digest (k8s entry)
2. `occam_map` → filter docs URLs (≤8)
3. `occam_digest` with `focus_query` — honest `failure.code` on bad items
4. `occam_transcode` nginx + `playbook_policy=auto`
5. `occam_extract_knowledge` on k8s concept URL
6. **`mediaRefs[]`** present on transcode success; digest ok items include array field

---

## Handoff — Linux Hermes (clean system)

After Cursor PASS:

```text
@corpora/quality-sprint-wide-cursor-desk.PROMPT.md @docs/HOST_INTEGRATION.md @docs/00-agent-handbook.md @artifacts/wide-cursor-desk/<date>/WIDE-CURSOR-DESK.md

Install Occam on Linux (Level A): clone + install.sh + doctor.
Register stdio MCP in Hermes → launch-mcp-host.mjs, OCCAM_HOME set.
Replay: node scripts/desk-recipe-r.mjs — expect K9 PASS.
Inject handbook §6 RAG + mediaRefs metadata into ingest policy.
```

---

## Handoff — macOS OpenClaw (embed)

Ingest from Hermes/Occam JSON:

- chunk `excerpt` / `markdown` where `ok: true`
- metadata: `url`, `failure.code`, `mediaRefs[].url`, `contextHeading`, `kind`
- skip `ok: false` rows

---

## Does not ship

- Baseline bump tier-3 (only after Merge PASS audit)
- Public `0.0.0.0` Occam bind
- In-core embeddings / vector DB
