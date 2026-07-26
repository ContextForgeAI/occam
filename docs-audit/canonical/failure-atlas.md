# Failure atlas

**Slug:** `failure-atlas` · **Product system:** PS-7 Monitoring and multi-source · **CAPs:** 10 · **Public relevance:** HIGH

**Member CAPs:** CAP-870…CAP-879  
**Product capability:** CAP-870  
**Engineering findings:** none open for this family (EF-024 **WITHDRAWN** — do not repeat process-wide leak claim)

## What it is

Opt-in in-memory aggregator of `TranscodePipeline` success/failure outcomes, exposed as MCP tool `occam_failure_atlas` when `OCCAM_ATLAS_MCP=1`. Backed by `FailureAtlasStore` + `FailureAtlasSink` wrapping telemetry (`Telemetry/FailureAtlasStore.cs`, `FailureAtlasSink.cs`). Parent gate reuses Wave-1 **CAP-015**.

## Why it exists

Give agents a worst-first host summary (“which sites are honestly walled vs transient”) so they stop retrying captcha/login/4xx closures during a run (`FailureAtlasClassifier`; tool Description).

## User-visible entrypoints

| Entrypoint | Notes | Evidence |
|------------|-------|----------|
| `occam_failure_atlas(only_walled?)` | Sole read surface | `OccamFailureAtlasTool.cs:16-36`; registration `:149-157` |
| No CLI / BatchServer / other tools | Confirmed by grep | CAP-876 |

Profile-orthogonal (`CAP-011`).

## Core behavior

### Recording path (CAP-873)

```
OCCAM_ATLAS_MCP=1 → AddSingleton FailureAtlasStore
                 → IOccamTelemetrySink = FailureAtlasSink(new OccamLoggerTelemetrySink())
Every TranscodePipeline complete/fail → RecordSuccess/RecordFailure(ctx.Url, code)
occam_failure_atlas → Snapshot → Summarize per host → sort closureRate desc, failures desc
```

**Tools that feed atlas** (via pipeline): transcode, digest, dataset_export, verify live, crosscheck, claim_check, batch processor, watch, PlaybookSaveVerifier.  
**Do not feed:** probe, map, extract_knowledge, playbook_resolve, playbook_heal (`CAP-873`).

### Host keying and bounds (CAP-871)

- Key: `Uri.Host` lowercased, `www.` stripped; max **500** hosts; new hosts silently dropped when full.
- Thread-safe via single lock.
- Malformed URLs silently skipped.

### Classifier (CAP-872)

Closure codes: `captcha_or_challenge`, `requires_login`, `http_401`, `http_403`, `http_404`, `http_410`.  
`walled = successes==0 && failures>0 && dominantFailure is closure`.  
`closureRate = closures/attempts` (4 decimals). Dominant = highest count, alpha tie-break.

### Response (CAP-870)

`{ ok, hostCount, walledCount, hosts[{host,attempts,successes,failures,closureRate,walled,dominantFailure,byCode,lastFailureAt}], timestamp }`. Param `only_walled` default false filters after snapshot.

## Advanced behavior

| Behavior | Notes | CAP |
|----------|-------|-----|
| Telemetry sink **replacement** | Second `AddSingleton<IOccamTelemetrySink>` wins (DI last registration) — not additive | CAP-875 |
| Batch co-enable | Same store sees fire-and-forget batch outcomes | CAP-877 |
| Stdio vs WS lifetime | Per DI session container on WS/Remote (`CAP-1000`) | C2 / EF-024 WITHDRAWN |
| Gate unit-only | Classifier + raw store; no DI/MCP wire proof | CAP-878 |

## Automatic / silent behavior

| Silent | Effect |
|--------|--------|
| Aggregates all pipeline tools | Agent using only probe never populates atlas |
| Drop hosts beyond 500 | No error |
| Sink swap at enable | Browser-pool telemetry still delegates to fresh logger (harmless today) |

## Parameters

| Name | Default | Effect |
|------|---------|--------|
| `only_walled` | `false` | Filter to walled hosts |

No write/clear/reset param — process/session end clears (`CAP-874`).

## Configuration

| Env | Default | Effect |
|-----|---------|--------|
| `OCCAM_ATLAS_MCP` | off | Tool + store + sink swap; startup-only |

No atlas DB path env (contrast watch/batch).

## Backends

None of its own — observes pipeline outcomes after routing.

## Sessions / state

| State | Class | Notes |
|-------|-------|-------|
| `FailureAtlasStore` | SESSION (ST-18) | In-memory; **not persisted** (`CAP-874`) |
| Lifetime | stdio ≈ process; WS/Remote ≈ per accepted connection DI | `CAP-1000`; **EF-024 WITHDRAWN** |

Wave-4 / C2: S3-04’s “process-wide multi-tenant leak under `--remote`” was **rejected**. Each WS/Remote session builds a new `Host.CreateApplicationBuilder()` + `AddOccamMcpServer()` — atlas is per-session and resets on reconnect.

## Network behavior

Read tool is local. Recording piggybacks on whatever network the pipeline already did.

## Artifacts produced

None on disk. JSON response only. Not in ART durable list (`STATE-MODEL.md` ST-18).

## Trust / provenance properties

- Operational telemetry, **not** a proof artifact.
- Description claim “in-memory; not persisted” matches code (`CAP-879`).
- Does not sign or verify.
- Scope omission: description does not state “transcode-pipeline-only” (`CAP-873` gap).

## Failure / fallback behavior

Tool itself does not fail on empty atlas (`ok:true`, zero hosts). Upstream failures are what get counted. Enabling atlas does not change extract failure codes.

## Platform differences

None. Pure CLR in-memory.

## Composition with other capabilities

- **Observes** PS-1/PS-2/PS-6/PS-7 pipeline consumers.
- **Combo:** `OCCAM_BATCH_MCP` + `OCCAM_ATLAS_MCP` → mid-batch wall discovery (`CAP-877`).
- Does not observe discovery-only tools (probe/map).
- Peer models: `AUTOMATION-MODEL.md` silent aggregation; `FAILURE-BEHAVIOR-MAP.md` for closure taxonomy alignment.

## Known limitations

- Empty after restart/reconnect (WS).
- Misses probe/map/heal/resolve/extract_knowledge signals.
- Cap 500 hosts with silent drop.
- Gate does not prove registration path (`CAP-878`).
- Sink replacement is a latent DI trap if a stateful base sink appears later (`CAP-875`).

## Engineering findings

| ID | Status | Notes |
|----|--------|-------|
| **EF-024** | **WITHDRAWN** | Do not document process-wide atlas leak; per-session DI proven |
| Related | EF-041 | Separate: `BrowserPoolManager.InstallShared`/`StopAll` on each WS session DI — pool kill, **not** atlas leak |

## Code evidence

- `src/FFOccamMcp.Core/Tools/OccamFailureAtlasTool.cs`
- `src/FFOccamMcp.Core/Telemetry/FailureAtlasStore.cs:15,85,162-165`
- `src/FFOccamMcp.Core/Telemetry/FailureAtlasSink.cs`
- `src/FFOccamMcp.Core/Transport/OccamMcpServerRegistration.cs:148-157`
- `src/FFOccamMcp.Core/Routing/TranscodePipeline.cs` telemetry call sites
- `benchmarks/l0-gate/FailureAtlasUnitTests.cs`
- Deep: `docs-audit/subsystems/failure-atlas.md`; C2 in `CANONICAL-AUDIT-INDEX.md`

## Public-doc relevance

**MEDIUM–HIGH** for opt-in ops. Accurate persistence claim already in tool docs. Add scope: pipeline-fed tools only; per-session on WS.

## Handbook relevance

**Operator/agent troubleshooting** — “stop retrying walled hosts.” Explicitly correct any older leak narrative with EF-024 WITHDRAWN.
