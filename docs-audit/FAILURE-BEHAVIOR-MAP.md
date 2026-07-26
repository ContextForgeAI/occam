# FAILURE-BEHAVIOR-MAP (Wave 4 Phase 4G)

Sources: A–H blind + central sweeps.

## Happy-path failure → observed behavior

| Trigger | Observed behavior | Visible? | Dangerous? | Evidence | Gap |
|---------|-------------------|----------|------------|----------|-----|
| HTTP 404/410 under `http_then_browser` | **Stop** — no browser, no managed | yes (`http_404`/`http_410`) | no | `OccamRouter.cs:145-148` | corrects CAP-052 |
| HTTP fail on public-reference domain | **Stop** — no browser | yes | no (intentional) | `OccamRouter.cs:149-152` | GAP-001 |
| HTTP thin/challenge | escalate browser → optional managed | yes via recovery[] | no | router + PPs | modeled |
| Both local fail + managed fail | surface = `ChooseRawFallback(http,browser)` only; managed never wins on fail | recovery may show managed | agent may miss managed attempt | `:182` | GAP-014 |
| SSRF in probe (`OutboundUrlBlockedException`) | masked as `network_error` | **misleading** | **yes** — hides policy block | `HttpProbeFetcher.cs:172-175` | GAP-003 |
| SSRF in css-extract | **no guard** — may fetch private IP via egressFetch | no typed refusal | **yes** | `css-extract.mjs:39` | GAP-004 |
| Oversize HTTP body | `fail` or `partial` per `on_oversize` | yes | partial trust | response-body-cap | covered |
| Oversize css body | **unbounded** `response.text()` | no | DoS/memory | `css-extract.mjs:78` | GAP-004 |
| Genome empty CT | skips `not_json` | may accept non-JSON | trust | WellKnownGenomeFetcher:67-69 | GAP-009 |
| Genome huge body | ReadToEnd then truncate 32KiB | latency/memory | DoS | `:75-81` | GAP-009 |
| TSA failure | omit time-anchor, continue unsigned-time | silent | trust downgrade | TimeAnchorService | covered partial |
| Batch Persist IO fail | swallow | silent | data loss | Batch JsonFile store | EFC-E-7 |
| Watch corrupt store | empty history | silent reset | data loss | WatchStore | Wave3 |
| Robots fetch fail | **fail-open allow-all** | no | polite-policy bypass | RobotsThrottleService:107-146 | GAP-018 |
| Playwright proxy config fail | fail-open null proxy | silent | egress policy bypass | egress-proxy.mjs:139 | GAP-030 |
| Translate fail | non-fatal warning codes | yes (warning) | no | TranslationService | GAP-020 |
| Malformed knowledge_schema field kinds | may throw past typed `invalid_arguments` | crash/untyped | yes | FieldSpecParser + KnowledgeExtractService | GAP-024 |
| Browser pool InstallShared on new WS session | StopAll kills warm pool | latency spike | availability | BrowserPoolManager:45-48 | GAP-002 |
| Worker exit 13 | mapped to timeout | yes | may hide OOM | worker-exit-guard | F covered |
| Thin after browser exhausted | stop / no-heal (`ThinExtractBrowserExhausted`) | decisions | under-documented in CAP-106 | OccamTranscodeTool:569-578 | GAP-016 |
| Cache TTL expiry | delete on read, live fallback | transparent | stale until read | TranscodeResponseCache | GAP-022 edge |
| Receipts off | most signing gated; **playbook_save still signs**; key still minted | partial | trust policy hole | PlaybookSaveService; DI LoadOrCreate | GAP-005 |

## Fallback systems (major?)

No *wholly unmodeled* fallback subsystem discovered in A–F. Gaps are **honesty / parity / ranking** inside known ladders (router, managed, robots, proxy, receipts), plus **css-extract guard asymmetry** (security parity gap, not a new subsystem).

| Unknown CLI arg (e.g. `--version`) | silently ignored → stdio starts (blocks) | Docker health red | **yes** (hang) | OccamMcpCli.Parse | GAP-035 |
| `occam refresh` | name-wide kill of OccamMcp.Core processes | process death | **yes** collateral | stop-occam-processes | GAP-033 |
| Marketplace L4 skipped | treated as validate success → auto-merge | PR merged | **yes** supply chain | playbook-marketplace.yml | GAP-036 |
| Cosign marketplace step | misconfigured → sign fails / keyless without OIDC | `.sig` chain broken | trust | playbook-marketplace.yml | GAP-037 |
| Install `rm -rf` before extract | no rollback (EF-028) | wipe | yes | get-ff-occam / release-install | Wave3 |
| Connect post-verify rollback | dead when `requiresRestart` (EF-021) | stale config | yes | connect ownership | Wave3 |
| Docker missing profiles/ | silent built-in fallbacks | behavior drift | subtle | Dockerfile | GAP-043 |

## Fallback systems (major?)
No wholly unmodeled fallback *subsystem*. New major failure semantics are honesty/parity/CI-supply-chain holes inside known surfaces (router, SSRF, install, marketplace, Docker health).
