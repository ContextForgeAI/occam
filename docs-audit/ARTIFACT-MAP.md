# ARTIFACT-MAP (Wave 2)

**SoT:** tool/subsystem audits + call sites in Core. Confidence = PROVEN unless noted.

| Artifact ID | Name | Created by | Consumed by | Persisted? | Verifiable? | Portable? | Cacheable? | Signed/hashed? | User/agent visible? |
|-------------|------|------------|-------------|------------|-------------|-----------|------------|----------------|---------------------|
| ART-001 | Markdown extract (compiled) | `occam_transcode`, digest items, claim/attest/dataset rows (via pipeline) | Agents; verify live compares drift | No (response) | Via receipt contentHash | Yes (text) | Yes if `cache_ttl_s` (transcode only) | contentHash / receipt | Yes |
| ART-002 | Structured `blocks[]` | Transcode (opt-in serialize; always collected internally CAP-078); forced on claim/attest/dataset | claim_check ranker; verify prove/citation; diff | No | Merkle leaf/proof | Partial (citations) | Key bug EF-001 if cached w/ annotations | blockMerkleRoot | Transcode yes; claim projects matches only |
| ART-003 | `tables[]` / records | Transcode `json_tables` | Agent | No | No dedicated proof | Yes | Via response cache | No | Transcode only |
| ART-004 | `feed` object | Transcode `json_feed` / HTTP feed short-circuit | Agent | No | No | Yes | Via cache | No | Transcode |
| ART-005 | `chunks[]` | Transcode `semantic_chunking` | Agent; verify live chunk staleness | No | Chunk staleness eval | Yes | Via cache | No | Transcode |
| ART-006 | Capsule `occam://capsule/…` | Transcode `emit_capsule` | `occam_verify` (any mode that parses capsule) | No (in receipt) | Offline verify | Yes (string) | N/A | Bundles signed receipt | Yes |
| ART-007 | Receipt v1 (positive) | transcode, digest items, claim_check, dataset rows, watch (opt-in) | `occam_verify` offline/live/prove/citation | Ephemeral unless caller stores | Yes (sig + optional content) | Yes JSON | No | ECDSA + hashes | Yes (if `OCCAM_RECEIPTS` on*) |
| ART-008 | Negative receipt | Failure paths (subset of codes) | verify offline | Ephemeral | Yes | Yes | No | Signed | Yes |
| ART-009 | Time-anchor sidecar | Transcode when TSA env on | verify offline | Ephemeral | Partial (self-consistent; chain cut) | Yes | No | RFC3161 | Advanced |
| ART-010 | Digest combined + items | `occam_digest` | Agent | No | Per-item reduced receipts | Yes | Digest-level `if_none_match` on combined only | Reduced Receipt v1 | Yes |
| ART-011 | Map link list | `occam_map` | Digest `source_url` discovery shares engine | No | No | Yes | No | No | Yes |
| ART-012 | Probe diagnosis | `occam_probe` | search rerank; agentHints → next tools | No | No | Yes | No | No | Yes |
| ART-013 | Search hits | `occam_search` | Agent → probe/transcode/digest | No | No | Yes | No | No | Yes |
| ART-014 | Knowledge `facts[]` | `occam_extract_knowledge` | Agent (not claim_check) | No | **Fake** “Receipt” telemetry only | Yes | No | No crypto | Yes |
| ART-015 | Playbook JSON (local) | `occam_playbook_save` | resolve; transcode `playbook_policy=auto`; extract schema | Yes (`OCCAM_PLAYBOOKS_LOCAL_ROOT`) | Signature = self-key only | File | Resolver cache bust on save | ECDSA always (ignores OCCAM_RECEIPTS) | Full profile |
| ART-016 | Heal skeleton / candidates | `occam_playbook_heal` | Human/agent → draft → save | No | No | Yes | No | No | Full profile |
| ART-017 | Resolve overlay / genome | `occam_playbook_resolve` | Agent planning; same resolver as auto transcode | Optional live genome fetch | Signature inspect self-key | JSON | In-memory resolver cache | Inspect only | Full profile |
| ART-018 | Lint grade/issues | `occam_playbook_lint` | Author before save | No | Advisory (may disagree save/resolve) | Yes | No | No | full+auditor |
| ART-019 | Claim matches + citation proofs | `occam_claim_check` | `occam_verify` citation/prove; `occam_attest` | No | Merkle (+ optional receipt sig) | Yes | **Never** cached | Leaf+proof; sig optional | researcher+ |
| ART-020 | Attest status batch | `occam_attest` | Agent gate on `status` | No | Merkle proves existence not truth | Yes | No | Consumes claim receipts | auditor+ |
| ART-021 | Verify verdict | `occam_verify` (+ CLI) | Agent / third party | No | Is the verifier | Yes | No | Validates sigs/proofs | researcher+ |
| ART-022 | Dataset rows + manifest | `occam_dataset_export` | CLI `occam verify --mode manifest`; per-row via verify | Response only (caller stores) | Manifest CLI-only; rows via MCP verify | Yes | No budget/cache on rows | Manifest sig + row receipts | auditor+ |
| ART-023 | Client ambient budget | `occam_client_capabilities` | transcode/digest omitted `max_tokens` | Process memory (+ env bootstrap) | No | N/A | Shifts cache identity | No | All profiles |
| ART-024 | MaterializationKey / contentHash | Transcode compile | `if_none_match`; receipts | No | Hash compare | Yes | Cache key | Hash | Advanced |
| ART-025 | Watch history chain | `occam_watch` (opt-in) | `occam_verify` mode=history | Yes (watch store) | Chain verify | Yes | Watch store | Signed chain | Opt-in |
| ART-026 | Session profile files | `occam-session` CLI | Tools with `session_profile` | Yes on disk | N/A | Operator-local | N/A | Secrets on disk | Operator |

\*Playbook save signs even when `OCCAM_RECEIPTS=off` (EF-005).

## Wave 3 additions

| Artifact ID | Name | Created by | Consumed by | Persisted? | Verifiable? | Notes |
|-------------|------|------------|-------------|------------|-------------|-------|
| ART-027 | Batch job snapshot | `occam_batch_*` / `--batch-server` | status/results / HTTP clients | Yes (`jobs.json`) | No Receipt v1 | Full markdown retained; no eviction (EF-037/038) |
| ART-028 | Watch store | `occam_watch` | `occam_verify` mode=history | Yes | SI-05 chain | No Remove API (EF-020) |
| ART-029 | Onboard state | `occam-onboard.mjs` | launcher / connect handoff | Yes (`~/.occam/onboard.json`) | No | Written before verify (EF-029) |
| ART-030 | Connect last-run | `occam connect` | installer snippet-skip | Yes (`connect-last.json`) | No | Best-effort |
| ART-031 | Host MCP config + bak | Connect CONFIG_FILE adapters | Host IDEs/CLIs | Yes | Host-side | Rollback often dead (EF-021) |
| ART-032 | Level B tarball + manifest | `build-release` / GH Releases | install Level B / get-ff-occam | Yes (release assets) | sha256 manifest | Real ship path; npm unpublished |
| ART-033 | Skill card | `skills/occam` / skill install | Agents reading skill | Yes | No | Stale version/tool-count (EF-036) |

## Wave 4 additions (reverse artifact audit)

| Artifact ID | Name | Created by | Consumed by | Persisted? | Verifiable? | Notes |
|-------------|------|------------|-------------|------------|-------------|-------|
| ART-034 | Host signing key PEM | `ReceiptSigner.LoadOrCreate` on DI | All signing call sites | Yes (`~/.occam/keys/signing-key.pem`) | Public half in receipts | Minted even if `OCCAM_RECEIPTS=off` (EF-044); missing from prior map |
| ART-035 | Transcode response cache entry | Eligible transcode success | Later transcode lookup | Yes (`OCCAM_CACHE_DIR`) | Replays prior signed envelope | Full post-sign JSON; fragment key gap EF-045 |
| ART-036 | Temp CSS field-spec JSON | `CssExtractWorker` | css-extract worker argv | Temp file | No | Host→worker boundary; best-effort cleanup |
| ART-037 | Session import raw cookies | `occam-session` import | Human/operator | Yes (`sessions/_imports/`) | No | Plaintext retention (EF-054) |
| ART-038 | Cosign release `.bundle` | `sign-release.yml` | **Nothing shipped** | Yes (GH Release asset) | Manual `cosign verify-blob` only | Trust theater (EF-053) |
| ART-039 | translatedMarkdown | `TranslationService` via transcode | Agent | No | No | Warning codes on fail; non-fatal |

## Composition notes

- **Central acquisition hub:** `TranscodePipeline` / `OccamRouter` — used by transcode, digest, claim_check, attest, dataset_export, verify(live), playbook_save(verify dry-run). **Wave 4 correction:** router surface ranking ≠ "managed last-rung always"; public-ref + 404/410 short-circuit; managed fail never wins surface (EF-056).
- **Bypass hub:** probe, map, search (provider), extract_knowledge (css-extract), playbook_heal (skeleton), playbook_lint (pure).
- **Trust hub:** Receipts/Merkle — produced widely; verified by `occam_verify` (+ CLI). **Wave 4:** playbook_save + key mint bypass receipts master; marketplace/release cosign not consumed by install.
- **Playbook hub:** resolve/save/heal/lint + silent auto on several pipeline tools. **Wave 4:** Core Sanitizer dead; local save ≠ publish sanitize.
- **Operator hub (W4):** onboard.json env injection, name-wide process kill, skill rmSync, Docker healthcheck hang.
