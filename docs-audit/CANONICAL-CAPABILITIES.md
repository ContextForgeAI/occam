# Canonical capabilities

**Phase:** 5B  
**Generated:** 2026-07-26  
**Source:** 674 immutable records in `capabilities.json`, corrected by the Wave-4 layer.

> **Orchestrator reconciliation (Phase 5, after P5-02 taxonomy stress test).** The JSON companion
> `canonical-capabilities.json` is the authoritative structure and has been updated beyond the body
> of this file. Changes: `quality-failure-semantics` moved **PS-2 → PS-1** (the three post-processors
> run on the router result at `TranscodePipeline.cs:152-157`, before `FinishMaterialize`);
> `digest-synthesis` moved **PS-3 → PS-7** (digest fans out over N URLs rather than discovering them,
> so PS-7 is redefined as composition across multiple fetches — sources, vantages, time, jobs);
> `canonical-knowledge-ir` is retained as a family but flagged `DEAD_CLUSTER` with zero product
> capabilities (P5-02 proposed removing it; the slug is kept so its four CAPs keep an owner and the
> existing card stays reachable). Six CAP-level reassignments applied: CAP-088→`focus-selection`
> (alias of CAP-317), CAP-178 alias of CAP-211, CAP-084→`acquisition-routing`, CAP-086→`receipts`,
> CAP-326→`change-monitoring`, CAP-308→`token-budget`. Net: 9 systems, 39 registered families
> (38 live + 1 dead cluster), 38 product capabilities, 674 CAPs — all IDs preserved. Section bodies
> below that predate this reconciliation retain the original family placement for those items;
> **read the JSON for placement, this file for reasoning.**

## Methodology

The raw CAP ledger is preserved exactly by ID and name, but it is not used as the documentation hierarchy. Each CAP belongs to exactly one stable family and each family to exactly one product system. Each reachable family has one canonical `PRODUCT_CAPABILITY` parent; a wholly shipped-dead evidence family is parented to its family slug instead of being promoted as product behavior. Fine-grained parameters, mechanisms, exposure/configuration behavior, trust/artifact/failure properties, dead implementation, and duplicate candidates remain as evidence under that parent.

The provisional nine-system hypothesis is accepted with clarified boundaries. Configuration, platform behavior, automation, failure semantics, persistence, security, and privacy remain cross-cutting properties rather than additional systems. Playbooks remain in-band acquisition/knowledge overlays but warrant a product system because resolution, authoring, healing, validation, artifacts, and trust boundaries form an independently operated lifecycle. Dead C# code is marked `SHIPPED_DEAD`, not unshipped, because the Core compile glob includes it.

Evidence precedence is executable code, Wave-4 corrections, canonical ledgers, then subsystem/tool reports. Corrections are explicit below; no historical CAP name was rewritten. `BUGGY` denotes shipped behavior tied to an open EF or a Wave-4 correction that proves the behavior defective; it is not promoted as a feature.

## Counts

- Raw CAP records: **674**
- Product systems: **9**
- Families: **39**
- Canonical product capabilities: **38**

| Classification | Count |
|---|---:|
| ARTIFACT_PROPERTY | 57 |
| CONFIG_BEHAVIOR | 110 |
| DUPLICATE_CANDIDATE | 12 |
| EXPOSURE_BEHAVIOR | 61 |
| FAILURE_BEHAVIOR | 47 |
| IMPLEMENTATION_DETAIL | 34 |
| MECHANISM | 61 |
| PRODUCT_CAPABILITY | 38 |
| SUBCAPABILITY | 192 |
| TRUST_PROPERTY | 62 |

| Family | System | Members | Product capability | Public relevance |
|---|---|---:|---|---|
| `acquisition-routing` | PS-1 | 6 | CAP-052 | HIGH |
| `http-acquisition` | PS-1 | 4 | CAP-200 | HIGH |
| `browser-acquisition` | PS-1 | 32 | CAP-203 | HIGH |
| `managed-acquisition` | PS-1 | 10 | CAP-054 | HIGH |
| `network-safety` | PS-1 | 23 | CAP-151 | HIGH |
| `proxy-egress` | PS-1 | 11 | CAP-157 | HIGH |
| `session-fetch` | PS-1 | 19 | CAP-167 | HIGH |
| `access-consent` | PS-1 | 11 | CAP-095 | HIGH |
| `token-budget` | PS-2 | 15 | CAP-061 | HIGH |
| `focus-selection` | PS-2 | 11 | CAP-064 | HIGH |
| `structured-materialization` | PS-2 | 19 | CAP-081 | HIGH |
| `differential-materialization` | PS-2 | 9 | CAP-074 | HIGH |
| `response-cache` | PS-2 | 4 | CAP-085 | HIGH |
| `quality-failure-semantics` | PS-2 | 6 | CAP-094 | HIGH |
| `probe-diagnostics` | PS-3 | 18 | CAP-420 | HIGH |
| `site-mapping` | PS-3 | 20 | CAP-510 | HIGH |
| `web-search` | PS-3 | 12 | CAP-620 | HIGH |
| `digest-synthesis` | PS-3 | 11 | CAP-450 | HIGH |
| `schema-knowledge-extraction` | PS-4 | 13 | CAP-590 | HIGH |
| `canonical-knowledge-ir` | PS-4 | 4 |  | MEDIUM |
| `playbook-resolution` | PS-5 | 11 | CAP-491 | HIGH |
| `playbook-authoring` | PS-5 | 18 | CAP-562 | HIGH |
| `playbook-healing` | PS-5 | 25 | CAP-530 | HIGH |
| `playbook-validation` | PS-5 | 14 | CAP-750 | HIGH |
| `receipts` | PS-6 | 39 | CAP-090 | HIGH |
| `verification` | PS-6 | 11 | CAP-268 | HIGH |
| `claims-attestation` | PS-6 | 25 | CAP-690 | HIGH |
| `dataset-provenance` | PS-6 | 10 | CAP-770 | HIGH |
| `batch-jobs` | PS-7 | 19 | CAP-800 | HIGH |
| `change-monitoring` | PS-7 | 19 | CAP-830 | HIGH |
| `consensus-crosscheck` | PS-7 | 19 | CAP-850 | HIGH |
| `failure-atlas` | PS-7 | 10 | CAP-870 | HIGH |
| `runtime-transports` | PS-8 | 10 | CAP-003 | HIGH |
| `mcp-exposure` | PS-8 | 22 | CAP-007 | HIGH |
| `client-context` | PS-8 | 5 | CAP-400 | HIGH |
| `operator-cli` | PS-9 | 77 | CAP-920 | HIGH |
| `install-onboarding` | PS-9 | 40 | CAP-940 | HIGH |
| `host-connectors` | PS-9 | 22 | CAP-980 | HIGH |
| `packaging-distribution` | PS-9 | 20 | CAP-1020 | HIGH |

## PS-1 — Acquisition

Obtain source content safely through HTTP, browser, managed, proxy, and session-aware paths.

### Acquisition routing (`acquisition-routing`)

**Purpose:** Select backends, short-circuit terminal cases, escalate, and choose the surfaced outcome.  
**Canonical product capability:** CAP-052 — `http_then_browser` cascade (the actual default execution path)  
**Members:** 6 CAP IDs (CAP-050, CAP-051, CAP-052, CAP-088, CAP-104, CAP-112)  
**Evidence/caveats:** EF-056.

### HTTP acquisition (`http-acquisition`)

**Purpose:** Fetch and extract through the HTTP worker and daemon paths.  
**Canonical product capability:** CAP-200 — HTTP extract backend (`node_readability_turndown`).  
**Members:** 4 CAP IDs (CAP-059, CAP-200, CAP-201, CAP-202)  
**Evidence/caveats:** CAP family members; source reports in capabilities.json.

### Browser acquisition (`browser-acquisition`)

**Purpose:** Acquire rendered content through Playwright workers, daemons, and pools.  
**Canonical product capability:** CAP-203 — Browser extract backend (Playwright Chromium).  
**Members:** 32 CAP IDs (CAP-053, CAP-076, CAP-099, CAP-203, CAP-204, CAP-205, CAP-206, CAP-206b, CAP-207, CAP-208, CAP-209, CAP-219, CAP-220, CAP-221, CAP-222, CAP-223, CAP-224, CAP-228, CAP-229, CAP-230, CAP-231, CAP-232, CAP-234, CAP-236, CAP-237, CAP-243, CAP-244, CAP-245, CAP-246, CAP-247, CAP-248a, CAP-248b)  
**Evidence/caveats:** EF-002, EF-041.

### Managed acquisition (`managed-acquisition`)

**Purpose:** Escalate successful requests to configured third-party extraction providers.  
**Canonical product capability:** CAP-054 — Managed backend escalation subsystem (third-party scraping fallback)  
**Members:** 10 CAP IDs (CAP-054, CAP-055, CAP-056, CAP-057, CAP-058, CAP-238, CAP-239, CAP-240, CAP-241, CAP-242)  
**Evidence/caveats:** EF-003.

### Network safety (`network-safety`)

**Purpose:** Enforce URL, DNS, redirect, response-size, robots, and network failure policy.  
**Canonical product capability:** CAP-151 — DNS-rebinding-safe SSRF guard, HTTP worker  
**Members:** 23 CAP IDs (CAP-073, CAP-100, CAP-101, CAP-103, CAP-110, CAP-150, CAP-151, CAP-152, CAP-153, CAP-154, CAP-155, CAP-156, CAP-165, CAP-184, CAP-185, CAP-186, CAP-187, CAP-188, CAP-189, CAP-190, CAP-194, CAP-225, CAP-226)  
**Evidence/caveats:** EF-042, EF-043.

### Proxy and egress (`proxy-egress`)

**Purpose:** Configure static or rotating proxies and define their path-specific reach.  
**Canonical product capability:** CAP-157 — Static HTTP/HTTPS/SOCKS5 proxy for Node worker egress  
**Members:** 11 CAP IDs (CAP-102, CAP-157, CAP-158, CAP-159, CAP-160, CAP-161, CAP-162, CAP-163, CAP-164, CAP-166, CAP-193)  
**Evidence/caveats:** EF-007, EF-057.

### Session-aware fetch (`session-fetch`)

**Purpose:** Apply local session headers, cookies, and browser storage state where supported.  
**Canonical product capability:** CAP-167 — Local session-profile files (`OCCAM_SESSIONS_ROOT/<id>.json`)  
**Members:** 19 CAP IDs (CAP-068, CAP-069, CAP-167, CAP-168, CAP-169, CAP-170, CAP-171, CAP-172, CAP-173, CAP-174, CAP-175, CAP-176, CAP-177, CAP-178, CAP-182, CAP-191, CAP-192, CAP-227, CAP-249)  
**Evidence/caveats:** EF-017, EF-039, EF-040, EF-054, ART-037.

### Access and consent handling (`access-consent`)

**Purpose:** Detect login/challenge walls and handle consent overlays without claiming universal bypass.  
**Canonical product capability:** CAP-095 — Challenge-page detection (post-processor + router-level parity)  
**Members:** 11 CAP IDs (CAP-095, CAP-096, CAP-107, CAP-179, CAP-180, CAP-181, CAP-183, CAP-210, CAP-211, CAP-212, CAP-213)  
**Evidence/caveats:** CAP family members; source reports in capabilities.json.

## PS-2 — Materialization

Transform acquired content into bounded, focused, structured, differential, and quality-signalled responses.

### Token budgeting (`token-budget`)

**Purpose:** Allocate and enforce content and sidecar token budgets.  
**Canonical product capability:** CAP-061 — Two-layer budget split (`BudgetOwnership`)  
**Members:** 15 CAP IDs (CAP-060, CAP-061, CAP-062, CAP-063, CAP-066, CAP-067, CAP-300, CAP-301, CAP-302, CAP-303, CAP-304, CAP-305, CAP-309, CAP-310, CAP-337)  
**Evidence/caveats:** CAP family members; source reports in capabilities.json.

### Focus and selection (`focus-selection`)

**Purpose:** Select relevant sections, paragraphs, and blocks for a caller intent.  
**Canonical product capability:** CAP-064 — `focus_query` (relevance targeting + honesty signal)  
**Members:** 11 CAP IDs (CAP-064, CAP-065, CAP-077, CAP-087, CAP-307, CAP-311, CAP-312, CAP-313, CAP-316, CAP-317, CAP-327)  
**Evidence/caveats:** CAP family members; source reports in capabilities.json.

### Structured materialization (`structured-materialization`)

**Purpose:** Produce blocks, tables, feeds, chunks, media references, and codecs.  
**Canonical product capability:** CAP-081 — `translate_to` (host-side LibreTranslate codec)  
**Members:** 19 CAP IDs (CAP-075, CAP-078, CAP-079, CAP-080, CAP-081, CAP-084, CAP-086, CAP-109, CAP-111, CAP-306, CAP-314, CAP-318, CAP-319, CAP-320, CAP-329, CAP-331, CAP-334, CAP-335, CAP-336)  
**Evidence/caveats:** EF-004, EF-010, EF-055, ART-039.

### Differential materialization (`differential-materialization`)

**Purpose:** Produce conditional and block-level change responses.  
**Canonical product capability:** CAP-074 — `if_none_match` (conditional/differential response)  
**Members:** 9 CAP IDs (CAP-074, CAP-082, CAP-083, CAP-089, CAP-308, CAP-323, CAP-324, CAP-325, CAP-326)  
**Evidence/caveats:** CAP family members; source reports in capabilities.json.

### Response cache (`response-cache`)

**Purpose:** Persist and replay opt-in materialized response envelopes.  
**Canonical product capability:** CAP-085 — `cache_ttl_s` (opt-in on-disk response cache)  
**Members:** 4 CAP IDs (CAP-085, CAP-315, CAP-321, CAP-322)  
**Evidence/caveats:** EF-001, EF-045, ART-035.

### Quality and failure semantics (`quality-failure-semantics`)

**Purpose:** Classify extraction quality, normalize failures, and expose recovery decisions.  
**Canonical product capability:** CAP-094 — PostProcessor pipeline ordering  
**Members:** 6 CAP IDs (CAP-094, CAP-097, CAP-098, CAP-105, CAP-106, CAP-108)  
**Evidence/caveats:** CAP family members; source reports in capabilities.json.

## PS-3 — Discovery

Diagnose, find, map, rank, and synthesize web sources before or across extraction.

### Probe diagnostics (`probe-diagnostics`)

**Purpose:** Predict extractability and access characteristics with a bounded pre-fetch probe.  
**Canonical product capability:** CAP-420 — Core capability: cheap pre-fetch diagnosis, confirmed HTTP-only / backend-isolated  
**Members:** 18 CAP IDs (CAP-420, CAP-421, CAP-422, CAP-423, CAP-424, CAP-425, CAP-426, CAP-427, CAP-428, CAP-429, CAP-430, CAP-431, CAP-432, CAP-433, CAP-434, CAP-435, CAP-436, CAP-437)  
**Evidence/caveats:** CAP family members; source reports in capabilities.json.

### Site mapping (`site-mapping`)

**Purpose:** Discover and rank site links from homepages, sitemaps, and robots files.  
**Canonical product capability:** CAP-510 — HTTP-only design, no backend escalation ever  
**Members:** 20 CAP IDs (CAP-510, CAP-511, CAP-512, CAP-513, CAP-514, CAP-515, CAP-516, CAP-517, CAP-518, CAP-519, CAP-520, CAP-521, CAP-522, CAP-523, CAP-524, CAP-525, CAP-526, CAP-527, CAP-528, CAP-529)  
**Evidence/caveats:** CAP family members; source reports in capabilities.json.

### Web search (`web-search`)

**Purpose:** Search configured providers and rank results using extractability evidence.  
**Canonical product capability:** CAP-620 — `query` (required input) + validation  
**Members:** 12 CAP IDs (CAP-620, CAP-621, CAP-622, CAP-623, CAP-624, CAP-625, CAP-626, CAP-627, CAP-628, CAP-629, CAP-630, CAP-631)  
**Evidence/caveats:** CAP family members; source reports in capabilities.json.

### Digest synthesis (`digest-synthesis`)

**Purpose:** Discover, fetch, and combine multiple sources into a bounded digest.  
**Canonical product capability:** CAP-450 — `urls` schema union + "urls and/or source_url" input contract  
**Members:** 11 CAP IDs (CAP-450, CAP-451, CAP-452, CAP-453, CAP-454, CAP-455, CAP-456, CAP-457, CAP-458, CAP-459, CAP-460)  
**Evidence/caveats:** CAP family members; source reports in capabilities.json.

## PS-4 — Knowledge extraction

Map source content into caller-supplied typed schemas and the shipped canonical-knowledge implementation.

### Schema-driven knowledge extraction (`schema-knowledge-extraction`)

**Purpose:** Resolve a schema and extract typed facts with the CSS worker.  
**Canonical product capability:** CAP-590 — Recipe D: resolve → schema-match → CSS-extract (the actual pipeline)  
**Members:** 13 CAP IDs (CAP-590, CAP-591, CAP-592, CAP-593, CAP-594, CAP-595, CAP-596, CAP-597, CAP-598, CAP-599, CAP-600, CAP-601, CAP-602)  
**Evidence/caveats:** EF-013, EF-014, EF-055, ART-036.

### Canonical knowledge IR (`canonical-knowledge-ir`)

**Purpose:** Represent canonical facts, entities, relationships, spans, and semantic tables; much is shipped but unreachable.  
**Canonical product capability:** none reachable; members are parented to family `canonical-knowledge-ir`  
**Members:** 4 CAP IDs (CAP-328, CAP-330, CAP-332, CAP-333)  
**Evidence/caveats:** CAP family members; source reports in capabilities.json.

## PS-5 — Playbooks

Resolve, apply, heal, validate, sign, and persist extraction playbooks and genomes.

### Playbook resolution (`playbook-resolution`)

**Purpose:** Resolve local, user, community, seed, and live-genome inputs with field-level precedence.  
**Canonical product capability:** CAP-491 — Four-tier resolution with fallback-per-field, not per-document, override  
**Members:** 11 CAP IDs (CAP-070, CAP-071, CAP-072, CAP-490, CAP-491, CAP-492, CAP-493, CAP-494, CAP-495, CAP-496, CAP-497)  
**Evidence/caveats:** CAP family members; source reports in capabilities.json.

### Playbook authoring (`playbook-authoring`)

**Purpose:** Verify, sign, persist, and journal local playbook drafts.  
**Canonical product capability:** CAP-562 — `verify=true` dry-run quality gate (the tool's own headline behavior)  
**Members:** 18 CAP IDs (CAP-560, CAP-561, CAP-562, CAP-563, CAP-564, CAP-565, CAP-566, CAP-567, CAP-568, CAP-569, CAP-570, CAP-571, CAP-572, CAP-573, CAP-574, CAP-575, CAP-576, CAP-577)  
**Evidence/caveats:** EF-005, EF-044.

### Playbook healing (`playbook-healing`)

**Purpose:** Capture browser skeleton evidence and propose bounded repair anchors.  
**Canonical product capability:** CAP-530 — `url` + `failure_reason` (required inputs, identity of the heal attempt)  
**Members:** 25 CAP IDs (CAP-530, CAP-531, CAP-532, CAP-533, CAP-534, CAP-535, CAP-536, CAP-537, CAP-538, CAP-539, CAP-540, CAP-541, CAP-542, CAP-543, CAP-544, CAP-545, CAP-546, CAP-547, CAP-548, CAP-549, CAP-550, CAP-551, CAP-552, CAP-553, CAP-554)  
**Evidence/caveats:** CAP family members; source reports in capabilities.json.

### Playbook validation (`playbook-validation`)

**Purpose:** Lint and validate playbooks while exposing parser and sanitizer boundaries.  
**Canonical product capability:** CAP-750 — Pure, network-free, single-shot validation contract  
**Members:** 14 CAP IDs (CAP-750, CAP-751, CAP-752, CAP-753, CAP-754, CAP-755, CAP-756, CAP-757, CAP-758, CAP-759, CAP-760, CAP-761, CAP-762, CAP-763)  
**Evidence/caveats:** EF-015, EF-047.

## PS-6 — Trust and provenance

Create and inspect receipts, proofs, claims, attestations, and auditable datasets without overstating guarantees.

### Receipts and proofs (`receipts`)

**Purpose:** Build signed positive/negative receipts, Merkle proofs, capsules, and time-anchor sidecars.  
**Canonical product capability:** CAP-090 — Receipt v1 positive signing  
**Members:** 39 CAP IDs (CAP-090, CAP-091, CAP-092, CAP-093, CAP-250, CAP-251, CAP-252, CAP-253, CAP-254, CAP-255, CAP-256, CAP-257, CAP-261, CAP-263, CAP-264, CAP-265, CAP-266, CAP-267, CAP-269, CAP-270, CAP-271, CAP-272, CAP-273, CAP-275, CAP-277, CAP-278, CAP-279, CAP-280, CAP-281, CAP-282, CAP-283, CAP-284, CAP-285, CAP-286, CAP-287, CAP-288, CAP-289, CAP-290, CAP-291)  
**Evidence/caveats:** ART-034.

### Receipt verification (`verification`)

**Purpose:** Verify receipts, detached signatures, proofs, citations, histories, and live re-fetches.  
**Canonical product capability:** CAP-268 — `OccamVerifyTool` — mode dispatch  
**Members:** 11 CAP IDs (CAP-258, CAP-259, CAP-260, CAP-262, CAP-268, CAP-274, CAP-276, CAP-650, CAP-651, CAP-652, CAP-653)  
**Evidence/caveats:** EF-011, EF-012.

### Claims and attestation (`claims-attestation`)

**Purpose:** Retrieve evidence for claims and aggregate citation status without implying stronger proof.  
**Canonical product capability:** CAP-690 — `occam_claim_check` as a fact-grounding primitive (SI-16)  
**Members:** 25 CAP IDs (CAP-690, CAP-691, CAP-692, CAP-693, CAP-694, CAP-695, CAP-696, CAP-697, CAP-698, CAP-699, CAP-700, CAP-701, CAP-702, CAP-703, CAP-720, CAP-721, CAP-722, CAP-723, CAP-724, CAP-725, CAP-726, CAP-727, CAP-728, CAP-729, CAP-730)  
**Evidence/caveats:** CAP family members; source reports in capabilities.json.

### Dataset provenance (`dataset-provenance`)

**Purpose:** Export auditable URL sets and manifests with explicit receipt limitations.  
**Canonical product capability:** CAP-770 — Tool entry point: schema, validation, dispatch  
**Members:** 10 CAP IDs (CAP-770, CAP-771, CAP-772, CAP-773, CAP-774, CAP-775, CAP-776, CAP-777, CAP-778, CAP-779)  
**Evidence/caveats:** EF-016, EF-018.

## PS-7 — Monitoring and multi-source

Run durable or multi-source jobs: batch, watch, consensus, and failure aggregation.

### Batch jobs (`batch-jobs`)

**Purpose:** Submit, process, persist, and retrieve asynchronous extraction jobs.  
**Canonical product capability:** CAP-800 — Batch execution core is code-shared, instance-isolated  
**Members:** 19 CAP IDs (CAP-800, CAP-801, CAP-802, CAP-803, CAP-804, CAP-805, CAP-806, CAP-807, CAP-808, CAP-809, CAP-810, CAP-811, CAP-812, CAP-813, CAP-814, CAP-815, CAP-816, CAP-817, CAP-818)  
**Evidence/caveats:** EF-037, EF-038.

### Change monitoring (`change-monitoring`)

**Purpose:** Persist watch targets and append chained change history.  
**Canonical product capability:** CAP-830 — `occam_watch` forces block collection unconditionally (JsonBlocks=true always)  
**Members:** 19 CAP IDs (CAP-830, CAP-831, CAP-832, CAP-833, CAP-834, CAP-835, CAP-836, CAP-837, CAP-838, CAP-839, CAP-840, CAP-841, CAP-842, CAP-843, CAP-844, CAP-845, CAP-846, CAP-847, CAP-848)  
**Evidence/caveats:** EF-019, EF-020.

### Consensus crosscheck (`consensus-crosscheck`)

**Purpose:** Compare multiple sources and return a consensus-oriented verdict.  
**Canonical product capability:** CAP-850 — `occam_crosscheck` MCP tool surface  
**Members:** 19 CAP IDs (CAP-850, CAP-851, CAP-852, CAP-853, CAP-854, CAP-855, CAP-856, CAP-857, CAP-858, CAP-859, CAP-860, CAP-861, CAP-862, CAP-863, CAP-864, CAP-865, CAP-866, CAP-867, CAP-868)  
**Evidence/caveats:** EF-031, EF-032.

### Failure atlas (`failure-atlas`)

**Purpose:** Aggregate per-session failure observations and expose atlas summaries.  
**Canonical product capability:** CAP-870 — `occam_failure_atlas` tool contract  
**Members:** 10 CAP IDs (CAP-870, CAP-871, CAP-872, CAP-873, CAP-874, CAP-875, CAP-876, CAP-877, CAP-878, CAP-879)  
**Evidence/caveats:** CAP family members; source reports in capabilities.json.

## PS-8 — Runtime and exposure

Expose capabilities through MCP transports, profiles, gates, instructions, and ambient client context.

### Runtime transports (`runtime-transports`)

**Purpose:** Run stdio, local WebSocket, remote authenticated WebSocket, and batch-server modes.  
**Canonical product capability:** CAP-003 — Transport mode: stdio (default)  
**Members:** 10 CAP IDs (CAP-002, CAP-003, CAP-004, CAP-005, CAP-006, CAP-020, CAP-021, CAP-023, CAP-024, CAP-027)  
**Evidence/caveats:** CAP family members; source reports in capabilities.json.

### MCP exposure (`mcp-exposure`)

**Purpose:** Register core and gated tools under profiles and advertise runtime instructions.  
**Canonical product capability:** CAP-007 — Core MCP catalog: 15 always-registered names (profile=full)  
**Members:** 22 CAP IDs (CAP-001, CAP-007, CAP-008, CAP-009, CAP-010, CAP-011, CAP-012, CAP-013, CAP-014, CAP-015, CAP-016, CAP-017, CAP-018, CAP-019, CAP-022, CAP-025, CAP-026, CAP-028, CAP-1000, CAP-1001, CAP-1002, CAP-1003)  
**Evidence/caveats:** EF-033.

### Client context (`client-context`)

**Purpose:** Store ambient client context and derive advisory profile and budget information.  
**Canonical product capability:** CAP-400 — Idempotent inspect-only read (no side effects)  
**Members:** 5 CAP IDs (CAP-400, CAP-401, CAP-402, CAP-403, CAP-404)  
**Evidence/caveats:** CAP family members; source reports in capabilities.json.

## PS-9 — Operator surface

Install, operate, connect, package, refresh, and distribute the host and its support assets.

### Operator CLI (`operator-cli`)

**Purpose:** Dispatch host and wrapper commands for lifecycle, sessions, refresh, keys, and inspection.  
**Canonical product capability:** CAP-920 — `version-surface` deployment diagnostic (Surface A)  
**Members:** 77 CAP IDs (CAP-350, CAP-351, CAP-352, CAP-353, CAP-354, CAP-355, CAP-356, CAP-357, CAP-358, CAP-359, CAP-360, CAP-361, CAP-362, CAP-363, CAP-364, CAP-365, CAP-366, CAP-367, CAP-368, CAP-369, CAP-370, CAP-371, CAP-372, CAP-373, CAP-374, CAP-375, CAP-376, CAP-377, CAP-378, CAP-379, CAP-380, CAP-381, CAP-382, CAP-383, CAP-384, CAP-385, CAP-386, CAP-387, CAP-388, CAP-389, CAP-390, CAP-391, CAP-392, CAP-393, CAP-394, CAP-395, CAP-880, CAP-881, CAP-882, CAP-883, CAP-884, CAP-885, CAP-900, CAP-901, CAP-902, CAP-903, CAP-904, CAP-920, CAP-921, CAP-922, CAP-923, CAP-924, CAP-925, CAP-926, CAP-927, CAP-928, CAP-929, CAP-930, CAP-931, CAP-932, CAP-933, CAP-934, CAP-935, CAP-936, CAP-937, CAP-938, CAP-939)  
**Evidence/caveats:** EF-022, EF-023, EF-025, EF-049.

### Install and onboarding (`install-onboarding`)

**Purpose:** Diagnose prerequisites, install runtime assets, configure onboarding, and verify installations.  
**Canonical product capability:** CAP-940 — `occam-doctor.ps1` / `.sh` unified preflight pipeline  
**Members:** 40 CAP IDs (CAP-940, CAP-941, CAP-942, CAP-943, CAP-944, CAP-945, CAP-946, CAP-947, CAP-948, CAP-949, CAP-950, CAP-951, CAP-952, CAP-953, CAP-954, CAP-955, CAP-956, CAP-957, CAP-958, CAP-959, CAP-960, CAP-961, CAP-962, CAP-963, CAP-964, CAP-965, CAP-966, CAP-967, CAP-968, CAP-969, CAP-970, CAP-971, CAP-972, CAP-973, CAP-974, CAP-975, CAP-976, CAP-977, CAP-978, CAP-979)  
**Evidence/caveats:** EF-028, EF-029, EF-030, EF-050.

### Host connectors (`host-connectors`)

**Purpose:** Generate, apply, verify, and roll back host-specific MCP configuration.  
**Canonical product capability:** CAP-980 — occam connect orchestrator (runConnect) — multi-adapter plan/apply/verify  
**Members:** 22 CAP IDs (CAP-980, CAP-981, CAP-982, CAP-983, CAP-984, CAP-985, CAP-986, CAP-987, CAP-988, CAP-989, CAP-990, CAP-991, CAP-992, CAP-993, CAP-994, CAP-995, CAP-996, CAP-997, CAP-998, CAP-999, CAP-1040, CAP-1041)  
**Evidence/caveats:** EF-021.

### Packaging and distribution (`packaging-distribution`)

**Purpose:** Build and distribute AOT, tarball, Docker, npm, skill, and CI release surfaces.  
**Canonical product capability:** CAP-1020 — `@ff-occam/mcp` npm bin: download-or-delegate host launcher  
**Members:** 20 CAP IDs (CAP-1020, CAP-1021, CAP-1022, CAP-1023, CAP-1024, CAP-1025, CAP-1026, CAP-1027, CAP-1028, CAP-1029, CAP-1030, CAP-1031, CAP-1032, CAP-1033, CAP-1034, CAP-1035, CAP-1036, CAP-1037, CAP-1038, CAP-1039)  
**Evidence/caveats:** EF-034, EF-035, EF-036, EF-051, EF-052, EF-053, ART-038.

## Duplicate and merge candidates

Aliases retain their own CAP records and IDs. The survivor is only the canonical parent for normalization; this does not delete evidence.

| Surviving canonical ID | Aliases / merge candidates | Family |
|---|---|---|
| CAP-424 | CAP-527, CAP-543, CAP-594 | probe-diagnostics |
| CAP-691 | CAP-771 | claims-attestation |
| CAP-693 | CAP-772 | claims-attestation |
| CAP-457 | CAP-775, CAP-776 | digest-synthesis |
| CAP-756 | CAP-759, CAP-760, CAP-761, CAP-762 | playbook-validation |
| CAP-287 | CAP-596 | receipts |

## Wave-4 corrections

| CAP | Required correction |
|---|---|
| CAP-021 | Wave 4 proved that the Content-Length adapter is the WebSocket stream path, not stdio framing (GAP-011). |
| CAP-052 | The cascade short-circuits only 404/410 and public-reference pages; raw fallback uses FailureRanking, and managed failure never wins the surfaced result (EF-056/GAP-001). |
| CAP-104 | The router does not use markdown density for raw fallback and does not surface managed failures; see EF-056/GAP-001. |
| CAP-106 | ThinExtractBrowserExhausted is an additional stop condition omitted by the original description (GAP-016). |
| CAP-151 | The original cross-path safety framing does not extend to css-extract, which lacks DNS-pin and response-body-cap parity (EF-043/GAP-004). |
| CAP-600 | Row-mode is unreachable earlier than recorded: host parsers never set base_selector (GAP-025/EF-014). |
| CAP-758 | The cited PlaybookCommunitySanitizer is shipped but Core-dead; lint and local save do not invoke publish sanitization (EF-047/GAP-008). |
| CAP-1029 | The Docker HEALTHCHECK invokes unsupported --version, which falls into stdio and can remain unhealthy (EF-051/GAP-035). |
| CAP-1031 | Marketplace validation can be skipped yet treated as success before auto-merge; cosign/install verification is also incomplete (EF-052/053, GAP-036/037). |

## Dead and gated capabilities

`SHIPPED_DEAD` means present in shipped code or assets but unreachable from the product path. `GATED` means reachable only after its recorded profile, environment, or caller gate.

| CAP | Status | Family | Recorded name |
|---|---|---|---|
| CAP-011 | GATED | mcp-exposure | Opt-in MCP tools are not profile-filtered |
| CAP-012 | GATED | mcp-exposure | Opt-in: `OCCAM_BATCH_MCP` → batch_submit/status/results + hosted processor |
| CAP-013 | GATED | mcp-exposure | Opt-in: `OCCAM_WATCH_MCP` → `occam_watch` |
| CAP-014 | GATED | mcp-exposure | Opt-in: `OCCAM_CONSENSUS_MCP` → `occam_crosscheck` |
| CAP-015 | GATED | mcp-exposure | Opt-in: `OCCAM_ATLAS_MCP` → `occam_failure_atlas` + FailureAtlasSink DI wrap |
| CAP-073 | GATED | network-safety | Well-known site genome fetch (env-gated live network call) |
| CAP-085 | GATED | response-cache | `cache_ttl_s` (opt-in on-disk response cache) |
| CAP-165 | SHIPPED_DEAD | network-safety | ABSENT: rotation does not reach the persistent daemon / pool / CSS / dom-skeleton spawns |
| CAP-166 | SHIPPED_DEAD | proxy-egress | ABSENT: Core's own C# `HttpClient`s never honor `OCCAM_HTTP_PROXY`/`HTTPS_PROXY` |
| CAP-188 | SHIPPED_DEAD | network-safety | ABSENT: no automatic retry/backoff on transient network failures |
| CAP-190 | GATED | network-safety | Robots.txt / crawl-politeness layer (opt-in, off by default) |
| CAP-213 | GATED | access-consent | Recipe cookie injection (opt-in). |
| CAP-221 | GATED | browser-acquisition | Screenshot capture (opt-in). |
| CAP-243 | GATED | browser-acquisition | Per-domain opt-in. |
| CAP-248a | SHIPPED_DEAD | browser-acquisition | `IWorkerProcessSpawner`/`NodeWorkerProcessSpawner` is registered in DI (`OccamServiceCollectionExtensions.cs:30`) but ne |
| CAP-248b | SHIPPED_DEAD | browser-acquisition | `BrowserConcurrencyGate` (`Workers/BrowserConcurrencyGate.cs`) is a second, independent concurrency-gate implementation. |
| CAP-264 | SHIPPED_DEAD | receipts | Negative receipts (SI-03): `OccamTranscodeResponseBuilder.BuildNegativeReceipt` |
| CAP-279 | SHIPPED_DEAD | receipts | `OccamTranscodeResponseBuilder.BuildNegativeReceipt` — fan-out to failure paths |
| CAP-286 | SHIPPED_DEAD | receipts | `MaterializedProvenanceResolver` — UNREACHABLE / dead code |
| CAP-287 | SHIPPED_DEAD | receipts | `occam_extract_knowledge`'s "Receipt" is NOT a signed Receipt v1 — finding |
| CAP-303 | SHIPPED_DEAD | token-budget | `ResponseBudgetDiagnostics`: computed but never surfaced in any MCP response |
| CAP-321 | GATED | response-cache | File-backed opt-in response cache: eligibility, key, storage |
| CAP-324 | SHIPPED_DEAD | differential-materialization | UNREACHABLE FROM LIVE CODE: `ResponseBudgetMode.Unchanged` / `ResponseBudgetMode.DeltaOnly` are exercised only by a unit |
| CAP-328 | SHIPPED_DEAD | canonical-knowledge-ir | DEAD (from live-traffic perspective): `CompactMarkdownCodec` and `JsonKnowledgeCodec` are registered but never selectabl |
| CAP-330 | SHIPPED_DEAD | canonical-knowledge-ir | Canonical extraction runs on every live transcode call but its output is never serialized to any MCP response |
| CAP-331 | SHIPPED_DEAD | structured-materialization | DEAD: `MaterializedProvenanceResolver` / `ProvenanceTrace` — fully-implemented claim→evidence→source→receipt-leaf chain, |
| CAP-332 | SHIPPED_DEAD | canonical-knowledge-ir | DEAD: `Fact`, `Entity`, `Relationship` Canonical types are defined but never instantiated anywhere |
| CAP-333 | SHIPPED_DEAD | canonical-knowledge-ir | `CanonicalRetention`: real, tested budget-aware retention logic — for output nobody reads (see CAP-330) |
| CAP-334 | SHIPPED_DEAD | structured-materialization | `TableSemanticMaterializer`: compat/bench-only entry point, not the live table path |
| CAP-335 | SHIPPED_DEAD | structured-materialization | `SurfaceSpanAttacher.Attach`: block→markdown-offset bridge, computed but not exposed |
| CAP-373 | GATED | operator-cli | RFC3161 time anchor (opt-in receipt enhancement) |
| CAP-383 | GATED | operator-cli | Opt-in MCP tool-group flags |
| CAP-394 | GATED | operator-cli | Cookie injection / browser-extract-variant / virtual-scroll opt-ins |
| CAP-436 | SHIPPED_DEAD | probe-diagnostics | DEAD/UNREACHABLE: `probe.autoRedirect` HttpClient is registered but never selected |
| CAP-495 | GATED | playbook-resolution | Live `.well-known` genome fetch is fully caller-gated here (opposite of CAP-070's transcode finding) |
| CAP-496 | SHIPPED_DEAD | playbook-resolution | `page_class` / `knowledge_schema` matching runs, but its own failure codes are swallowed here |
| CAP-547 | GATED | playbook-healing | Profile-gated exposure: `occam_playbook_heal` is full-profile-only |
| CAP-552 | SHIPPED_DEAD | playbook-healing | DEAD CODE: unused `CreateHeadersScope` helper in `DomSkeletonWorker` |
| CAP-553 | SHIPPED_DEAD | playbook-healing | HIDDEN/DEAD: worker's `--consent-aggressive` flag is unreachable from the MCP tool |
| CAP-595 | SHIPPED_DEAD | schema-knowledge-extraction | `confidence` is a dead field: always `0.0` on success, never assigned |
| CAP-600 | SHIPPED_DEAD | schema-knowledge-extraction | `base_selector` row-mode: structured list/table extraction distinct from flat facts |
| CAP-622 | GATED | web-search | Provider selection via `OCCAM_SEARCH_PROVIDER` (off by default) |
| CAP-627 | GATED | web-search | `rerank` (opt-in extractability-based reordering via live probe fan-out) |
| CAP-779 | GATED | dataset-provenance | Profile-gated tool: only in `full` and `auditor`, not `reader`/`researcher` |
| CAP-840 | SHIPPED_DEAD | change-monitoring | DEAD: `IWatchStore.Remove` is unreachable from every live surface — no way to un-watch a URL |
| CAP-864 | SHIPPED_DEAD | consensus-crosscheck | Dead `"paywall"` failure-code branch, duplicated a second time |
| CAP-1003 | SHIPPED_DEAD | mcp-exposure | Outer `AddOccamMcpServer()` call in WS transport is protocol-dead |

## Uncertain classifications

None. Classification uncertainty was not used; bounded runtime/CI uncertainties remain engineering findings rather than unknown CAP ownership.

## Machine-readable companion

`canonical-capabilities.json` is the canonical per-CAP projection. It preserves every raw ID and unchanged name exactly once, and carries family, system, parent, aliases, relevance, status, and correction fields.
