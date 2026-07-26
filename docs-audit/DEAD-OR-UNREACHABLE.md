# DEAD-OR-UNREACHABLE (Wave 1 seed)

Code-proven from Wave 1 reports. Not product capabilities for public docs.

| ID | Finding | Report |
|----|---------|--------|
| CAP-286 / CAP-331 | `MaterializedProvenanceResolver` / `ProvenanceTrace` — fully implemented, zero callers | trust-receipts, materialization |
| CAP-248a | `IWorkerProcessSpawner` / `NodeWorkerProcessSpawner` registered in DI, never injected | browser-workers |
| CAP-248b | `BrowserConcurrencyGate.Run<T>` never called; only `.MaxParallel` read | browser-workers |
| CAP-324 | `ResponseBudgetMode.Unchanged` / `DeltaOnly` — unit-tested only, never selected live | materialization |
| CAP-328 | `CompactMarkdownCodec` / `JsonKnowledgeCodec` registered, never selectable (no MCP codec param) | materialization |
| CAP-330 / CAP-333 | Canonical Knowledge extraction runs every transcode then discarded (only `MarkdownPassthroughCodec` reads response path) | materialization |
| CAP-332 | `Fact` / `Entity` / `Relationship` Canonical types never instantiated | materialization |
| CAP-334 | `TableSemanticMaterializer` — bench/test-only, not live table path | materialization |
| CAP-264 / CAP-279 | `"paywall"` negative-receipt branch unreachable (no post-processor emits that code) | trust-receipts |
| CAP-165 | Proxy rotation does not reach persistent daemon / pool / CSS / dom-skeleton spawns | network-fetch-proxy |
| CAP-166 | Core C# HttpClients never honor `OCCAM_HTTP_PROXY` | network-fetch-proxy |
| CAP-188 | No automatic network retry/backoff | network-fetch-proxy |

## Computed-but-not-exposed (live cost, weak observability)

| ID | Finding |
|----|---------|
| CAP-303 | `ResponseBudgetDiagnostics` computed, never in MCP response |
| CAP-335 | `SurfaceSpanAttacher` spans computed, not exposed |
| CAP-287 | `occam_extract_knowledge` "Receipt" field is telemetry, not signed Receipt v1 |

## Live bugs flagged (not dead)

| ID | Finding |
|----|---------|
| CAP-315 | `TranscodeCacheKey` omits `rank_blocks` / `tag_trust` / `emit_capsule` — stale annotation replay risk on cache hit |

## Wave 2 additions

| ID | Finding | Report |
|----|---------|--------|
| CAP-436 | Probe dead/unreachable branch (see probe report) | occam_probe |
| CAP-552 | Heal: unused CreateHeadersScope helper | occam_playbook_heal |
| CAP-553 | Heal: `--consent-aggressive` unreachable from MCP | occam_playbook_heal |
| CAP-496 | Resolve: knowledge_schema failure codes swallowed | occam_playbook_resolve |
| CAP-600 | extract_knowledge: base_selector row-mode unused by host mapper | occam_extract_knowledge |
