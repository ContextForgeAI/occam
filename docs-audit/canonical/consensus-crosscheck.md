# Consensus crosscheck

**Slug:** `consensus-crosscheck` · **Product system:** PS-7 Monitoring and multi-source · **CAPs:** 19 · **Public relevance:** HIGH

**Member CAPs:** CAP-850…CAP-868 (CAP-868 reserved marker in inventory)  
**Product capability:** CAP-850  
**Engineering findings:** EF-031, EF-032

## What it is

Opt-in MCP tool `occam_crosscheck` (`OCCAM_CONSENSUS_MCP=1`) that extracts one URL through 2+ **vantage points** (backend axis × optional session axis), then classifies agreement: `consensus` | `divergent` | `access_divergent` | `inconclusive`. Implementation is small and self-contained: `OccamCrosscheckTool` → `ConsensusService` → `ConsensusEvaluator` (~4 Core files).

It is a **single-node local jury**, not a distributed multi-node consensus system (`CAP-859`).

## Why it exists

Detect bot-vs-browser cloaking and anon-vs-authenticated personalization/paywalling by comparing fingerprints (`blockMerkleRoot` preferred, else `contentHash`) across vantages from the **same host egress** (`ConsensusEvaluator.cs:5-9`, `ConsensusService.cs:17-22`).

## User-visible entrypoints

| Entrypoint | Notes | Evidence |
|------------|-------|----------|
| `occam_crosscheck` | Only MCP surface | `OccamCrosscheckTool.cs:16-44`; registration `:141-146` |
| None CLI / BatchServer / doctor | 100% MCP-opt-in | subsystem inventory |

Registration is **orthogonal to `OCCAM_PROFILE`** (`CAP-860`; EF-031). Zero mentions in `OccamServerInstructions` (`CAP-861`).

## Core behavior

### Params and envelope (CAP-850)

Required: `url`. Optional: `vantages` (default `"http,browser"`), `session_profile`, `focus_query`.  
Local failure: only `invalid_arguments`. Non-argument failures are absorbed as per-vantage `ok:false` inside an outer `ok:true` success — agents must read `vantages[].ok` / `failureCode`.

### Vantage generation (CAP-852, CAP-866)

For each parsed backend (`http`/`browser`, deduped, order preserved): always one anonymous vantage; if `session_profile` set, also `"<backend>+session"`. So `http,browser` + session = **4** full pipeline runs. Empty/invalid tokens → tool `invalid_arguments`; empty list after parse falls back to default pair (`CAP-866`).

### Forced options (CAP-853, CAP-854)

| Forced choice | Why |
|---------------|-----|
| `PlaybookPolicy.Off` | Genome must not mask divergence (`ConsensusService.cs:78`) |
| Direct `Http` / `Browser` — never cascade | Failed backend stays a failed vantage (`CAP-854`) |
| `JsonBlocks=true` | Block Merkle for fingerprints |

### Verdict algorithm (CAP-851, CAP-858)

`ConsensusEvaluator.Evaluate` (`ConsensusEvaluator.cs:17-49`):

1. Any usable + any access wall → **`access_divergent`** (priority over content divergence — CAP-858).
2. Else ≥2 usable → one fingerprint → **`consensus`**, else **`divergent`**.
3. Else → **`inconclusive`**.

Pairwise `divergence[]` only over usable vantages; overlap math union-based (`CAP-857`).

## Advanced behavior

| Behavior | Notes | CAP |
|----------|-------|-----|
| Per-vantage Receipt v1 | Same `BuildReceipt` / `BuildNegativeReceipt` as transcode family | CAP-855 |
| Verdict unsigned | Re-derivable-by-design from receipts; **no tool re-derives it** | CAP-856; EF-032 |
| Dead `"paywall"` branch | Duplicated failure-code path never hit by taxonomy | CAP-864 |
| Local `OCCAM_RECEIPTS` re-parse | Does not use `ReceiptsPolicy.Enabled()` | CAP-865; aligns C6 incompleteness |
| Sibling to claim_check/attest | Shared primitives only — no call graph | CAP-862 |

## Automatic / silent behavior

| Silent | Effect |
|--------|--------|
| Cost multiplier with session | N backends × 2 session states without extra caller flag beyond profile |
| Outer ok:true on partial vantage fails | Easy to mis-read as “crosscheck succeeded” |
| No server-instructions hint | Discoverability = tools/list Description only |

## Parameters

| Name | Default | Effect |
|------|---------|--------|
| `url` | required | Target |
| `vantages` | `http,browser` | Backend list |
| `session_profile` | unset | Doubles vantages per backend |
| `focus_query` | unset | Passed into options |

## Configuration

| Env | Default | Effect |
|-----|---------|--------|
| `OCCAM_CONSENSUS_MCP` | off | Registers tool + `IConsensusService` |
| `OCCAM_RECEIPTS` | (dual-parse) | Local gate in ConsensusService (`CAP-865`) |
| Proxy / egress | process-wide | **No per-vantage proxy/geo** (`CAP-859`) |

## Backends

Explicit `http` and/or `browser` only. Does **not** use `http_then_browser` cascade or managed (`CAP-854`).

## Sessions / state

Stateless beyond normal extract side effects. Session profile creates parallel authed vantages. No durable consensus store. Atlas (if enabled) records each vantage’s pipeline outcome (`CAP-873`).

## Network behavior

N independent live extracts, same egress IP/proxy. Timeouts = backend contracts (HTTP ~35s, browser ~120s) × vantage count. Cannot detect CDN/geo cloaking (`CAP-859`).

## Artifacts produced

| Artifact | Signed? |
|----------|---------|
| Per-vantage `contentHash` / `blockMerkleRoot` / `receipt` | Receipt when policy + wall rules allow |
| `verdict` string + `divergence[]` | **No** (`CAP-856`) |

## Trust / provenance properties

What **`consensus` proves** (`CAP-863` ledger): N vantages on **this host** saw the same fingerprint.

What it **does not** prove: which vantage is canonical; absence of geo/CDN cloaking; that the reported `verdict` field wasn’t altered (recompute from receipts yourself — no shipped helper); distributed N-of-M agreement (deferred).

Align with `TRUST-MODEL.md`: conservative claims only.

## Failure / fallback behavior

| Case | Surfaced |
|------|----------|
| Bad args / bad vantage token | Top-level `invalid_arguments` |
| Transient vantage fail | `vantages[i].ok=false`, may yield `inconclusive` |
| Wall + usable | `access_divergent` |
| Gate coverage | Pure evaluator unit tests only (`L_CONSENSUS_OK`) — no e2e MCP/receipt re-derive (**EF-032**; CAP-867) |

## Platform differences

None specific. Same as underlying HTTP/browser workers.

## Composition with other capabilities

- **Uses** PS-1/PS-2 pipeline with playbooks forced off.
- **Uses** PS-6 receipt builder per vantage; **does not** feed claim_check/attest.
- Orthogonal opt-in vs batch/watch/atlas; may populate atlas.
- Profile cannot hide the tool if env is set (**EF-031**).

## Known limitations

- Local jury only.
- Verdict field is advisory plaintext.
- No discoverability in server instructions.
- Gate does not prove registration or signing path.
- Dead paywall branch / dual receipts parse are hygiene debt (`CAP-864`, `CAP-865`).

## Engineering findings

| ID | Finding (not a feature) |
|----|-------------------------|
| **EF-031** | Exempt from `OCCAM_PROFILE` + absent from server instructions |
| **EF-032** | Gate unit-only; no e2e tool/receipt re-derive of verdict |

## Code evidence

- `src/FFOccamMcp.Core/Tools/OccamCrosscheckTool.cs`
- `src/FFOccamMcp.Core/Consensus/ConsensusService.cs`, `ConsensusEvaluator.cs`, `ConsensusModels.cs`
- `src/FFOccamMcp.Core/Transport/OccamMcpServerRegistration.cs:141-146`
- `benchmarks/l0-gate/ConsensusUnitTests.cs`
- Deep: `docs-audit/subsystems/consensus-crosscheck.md`
- Peers: `TRUST-MODEL.md`, `PRODUCT-ARCHITECTURE.md` L7

## Public-doc relevance

**HIGH** when documenting cloaking checks. Forbidden: “distributed consensus,” “proves truth of page,” “geo-independent.” State opt-in + local jury + unsigned verdict.

## Handbook relevance

**Trust-adjacent workflows** after receipts. Teach reading `vantages[]` not only `verdict`; show session_profile cost multiplier.
