# SOURCE-COVERAGE-MATRIX (Wave 4 Phase 4B)

Target: **100% ownership** of shipped executable source. Partitions derived from the real tree (implementation ownership, not product features). Each partition = one blind Wave-4 agent (`W4-*`).

## Partition roster (blind agents)

| Owner | Scope | Files (approx) | Report |
|-------|-------|----------------|--------|
| **W4-A** host/runtime/transport/config/telemetry | `Program.cs`, `Transport/`, `Composition/`, `Configuration/`, `Lifecycle/`, `Cli/`, `Portable/`, `Telemetry/`, `Json/`, `Abstractions/` | ~40 | `negative-space/A-host-runtime-blind.md` |
| **W4-B** routing/backends/managed/postproc/probe/access/text/semantics | `Routing/`, `Backends/`, `Backends/Managed/`, `PostProcessors/`, `Probe/`, `Access/`, `Text/`, `Semantics/` | ~65 | `negative-space/B-routing-backends-blind.md` |
| **W4-C** compile/knowledge/codecs/caching/materialization/extract | `Compile/`, `Knowledge/**`, `Codecs/`, `Caching/`, `Extract/` | ~75 | `negative-space/C-compile-knowledge-blind.md` |
| **W4-D** tools/services/digest/search/session/client/agent | `Tools/`, `Services/`, `Digest/`, `Search/`, `Session/`, `Client/`, `Agent/` | ~65 | `negative-space/D-tools-services-blind.md` |
| **W4-E** trust/state (receipts/playbooks/claims/attest/consensus/dataset/watch/batch) | `Receipts/`, `Playbooks/`, `Claims/`, `Attest/`, `Consensus/`, `Dataset/`, `Watch/`, `Batch/` | ~65 | `negative-space/E-trust-state-blind.md` |
| **W4-F** workers (Node) | `workers/**` (incl. selftests, recipes, shared, plugins) | ~80 | `negative-space/F-workers-blind.md` |
| **W4-G** scripts/operator/connect/installer | `scripts/**` (excl. `scripts/bench`) | ~130 | `negative-space/G-scripts-operator-blind.md` |
| **W4-H** packages/docker/CI/shipped-boundary | `packages/**`, `Dockerfile`, `docker-compose.yml`, `.github/workflows/**`, `scripts/bench/**` | ~70 | `negative-space/H-packaging-ci-blind.md` |

## src/ subdir → owner (100%)

| src subdir | files | owner |
|------------|-------|-------|
| Program.cs, Abstractions, Transport, Composition, Configuration, Lifecycle, Cli, Portable, Telemetry, Json | 40 | W4-A |
| Routing, Backends, Backends/Managed, PostProcessors, Probe, Access, Text, Semantics | 65 | W4-B |
| Compile, Knowledge (+Canonical/Extraction/Legacy), Codecs, Caching, Extract | 75 | W4-C |
| Tools, Services, Digest, Search, Session, Client, Agent | 65 | W4-D |
| Receipts, Playbooks, Claims, Attest, Consensus, Dataset, Watch, Batch | 65 | W4-E |

Sum C# = 309 (whole shipped glob) — no orphan subdir.

## Non-C# → owner (100%)

| tree | owner |
|------|-------|
| workers/http-extract, browser-extract, css-extract, shared, plugins, recipes, selftests | W4-F |
| scripts/*.mjs/.ps1/.sh, scripts/lib/** (incl. operator/connect) | W4-G |
| scripts/bench/** | W4-H (BENCHMARK_ONLY reachability check) |
| packages/occam-mcp, occam-agent-sdk, occam-skill | W4-H |
| Dockerfile, docker-compose.yml, .github/workflows/** | W4-H |

## Data/asset trees (no blind agent; classified only)

`profiles/`, `corpora/`, `skills/`, `docs/` — data/doc, not executable. Referenced by owners where they gate behavior (e.g. W4-E reads `profiles/playbooks/seeds`, W4-G reads `scripts/templates`).

## Review status (updated as agents land)

| Owner | Status |
|-------|--------|
| W4-A | landed — `A-host-runtime-blind.md` |
| W4-B | landed — `B-routing-backends-blind.md` |
| W4-C | landed — `C-compile-knowledge-blind.md` |
| W4-D | landed — `D-tools-services-blind.md` |
| W4-E | landed — `E-trust-state-blind.md` |
| W4-F | landed — `F-workers-blind.md` |
| W4-G | landed — `G-scripts-operator-blind.md` |
| W4-H | landed — `H-packaging-ci-blind.md` |

**Coverage:** 100% of shipped executable source partitions have a Wave-4 owner and a landed blind report.
