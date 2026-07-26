# Schema-driven knowledge extraction

**Slug:** `schema-knowledge-extraction` · **Product system:** PS-4 Knowledge extraction · **CAPs:** 13 · **Public relevance:** HIGH.

## What it is

`occam_extract_knowledge` resolves a playbook `knowledge_schema`, chooses a page class, builds field specifications, and runs the dedicated CSS extraction worker to return typed-looking `facts[]` (CAP-590–602; `KnowledgeExtractService.cs:15-147`).

It is a separate fetch/extraction spine. It bypasses `TranscodePipeline`, `OccamRouter`, materialization, post-processors, and Receipt v1 (`PRODUCT-ARCHITECTURE.md:87`; CAP-591).

## Why it exists

- Extract named fields such as title, price, or author from a site recipe rather than returning markdown (CAP-590).
- Reuse playbook tier resolution and page-class routing for structured knowledge (CAP-590).
- Permit CSS, raw-regex, Nuxt-state, constants, multiplicity, and numeric division in schema field specs (CAP-599).
- Return partial field evidence when the overall worker reports failure (CAP-602).

## User-visible entrypoints

| Surface | Role | Evidence |
|---|---|---|
| MCP `occam_extract_knowledge` | Three-parameter structured extraction | `OccamExtractKnowledgeTool.cs:12-54`; CAP-590 |
| `workers/css-extract/css-extract.mjs` | Dedicated one-shot worker, internal | CAP-592/597 |
| Resolved playbook schema | Required input artifact from PS-5 | `KnowledgeSchemaPlanner.cs`; CAP-590 |

The tool is present in all profiles, including `reader` (`OccamToolProfile.cs`; `occam_extract_knowledge.md:315`).

## Core behavior

1. Validate URL/policy and run shared fetch preflight/session header resolution (CAP-590).
2. Resolve local → user → community → seed playbook tiers (CAP-590/CAP-491).
3. Require a nonempty `knowledge_schema`; select the longest matching `genome.page_classes` pattern or `default` (CAP-590).
4. Parse each field's `selector`, `attr`, `multiple`, and `divide` into a temp JSON plan (CAP-590; ART-036).
5. Spawn a fresh CSS Node worker for HTTP fetch/extraction (CAP-592/597).
6. For selected failures and a permitting policy, invoke a throwaway Playwright session inside that worker (CAP-593).
7. Rebuild host-side `facts[]`, metadata, backend, and optional partial facts (CAP-590/602).

## Advanced behavior

| Mode | Behavior | Evidence |
|---|---|---|
| CSS fields | Selector scoped to document; text/html/href/src/other attributes | `css-schema-extract.mjs`; CAP-590 |
| `attr:"regex"` | Raw-HTML regex; `{id}` from `/item/(\d+)/` | CAP-599 |
| `attr:"const"` | Returns selector literal without page interaction | CAP-599 |
| `attr:"nuxt"` | Extracts and evaluates `window.__NUXT__`, then follows a path | CAP-598 |
| `divide` | Numeric unit conversion after extraction | CAP-599 |
| `multiple` | Returns multiple selected values, converted host-side | `FieldSpecParser.cs`; CAP-590 |
| `base_selector` | Worker row mode exists, but host mapper cannot consume it | CAP-600; EF-014 |

## Automatic / silent behavior

- Every call creates a field-spec temp file and a fresh Node process; no daemon exists (CAP-597; ART-036).
- HTTP and host process-capture timeouts are effectively fixed at 45 seconds (CAP-592).
- Browser fallback is attempted only after `401`, `403`, `429`, `timeout`, or `extraction_failed` when policy permits (CAP-593).
- Browser fallback launches an unpooled Chromium context without the shared concurrency gate, consent dismissal, recipe interaction, or challenge detection (CAP-593).
- Session headers apply to the HTTP leg but do not reach the browser fallback (CAP-594).
- Declared `confidence` is never assigned, remains 0.0, and is omitted from JSON (CAP-595).
- `receipt` is always telemetry, regardless of `OCCAM_RECEIPTS` (CAP-596/CAP-287/EF-006).

## Parameters

| Name | Default | Effect | Evidence |
|---|---|---|---|
| `url` | required | Target and schema/page-class match input | CAP-590 |
| `backend_policy` | `http_then_browser` | Governs whether CSS worker may attempt its narrow browser fallback | CAP-593 |
| `session_profile` | `null` | Header-only on HTTP leg; absent from browser fallback | CAP-594 |

There is no schema JSON parameter: schema comes only from resolved playbooks. There is no token budget, focus, cache, diff, blocks/tables/chunks, screenshot, translation, receipt, managed backend, or llms.txt parameter (CAP-591).

## Configuration

| Setting | Effect | Evidence |
|---|---|---|
| `OCCAM_HOME` / worker path resolution | Locates CSS worker | `WorkerPaths.cs`; CAP-590 |
| `OCCAM_SESSIONS_ROOT` / request-header file | Resolves session/header input | CAP-590/594 |
| `OCCAM_HTTP_PROXY`, `OCCAM_HTTPS_PROXY`, `OCCAM_NO_PROXY` | Applied to CSS worker process/HTTP fetch | `EgressProxyConfig.ApplyTo`; CAP-592 |
| `OCCAM_ALLOW_PRIVATE_URLS` | Shared preflight escape hatch | CAP-100 |
| Playbook roots | Determine schema source | CAP-491 |

No extract-knowledge-specific env setting exists (`occam_extract_knowledge.md:316`).

## Backends

The primary backend is `css-extract.mjs` using `egressFetch`. Its optional browser fallback imports `browser-session.mjs`, launches a fresh session, performs `goto`, waits 1.5 seconds, captures HTML, and closes (CAP-592/593).

No `OccamRouter`, HTTP/browser backend classes, managed provider, daemon, browser pool, or transcode post-processor participates (CAP-591/593).

## Sessions / state

Session headers and explicit Cookie apply only to initial HTTP fetch. Playwright storageState and even forwarded header files are not applied to the fallback browser, which is anonymous (CAP-594).

ART-036 field-spec JSON is temporary and best-effort deleted. No response/cache state persists (ART-036; `STATE-MODEL.md:46`).

## Network behavior

- One HTTP fetch; optionally one browser navigation (CAP-592/593).
- No same-backend retry, managed escalation, robots throttle, domain tier, or response cache (CAP-591).
- Proxy applies to the worker process (CAP-592).
- Shared preflight blocks literal private targets, but the CSS worker lacks DNS-pin/body-cap parity with HTTP/browser workers (EF-043).
- `attr:"nuxt"` evaluates page-controlled code in the Node worker (CAP-598; EF-013).

## Artifacts produced

- ART-014: `facts[]`, metadata, backend, and a misleading telemetry `receipt` (`ARTIFACT-ONTOLOGY.md:82`).
- ART-036: temporary field-spec JSON from host to worker (`ARTIFACT-ONTOLOGY.md:142`).

Facts are ephemeral and machine-readable. No canonical IR entity/relationship graph is emitted.

## Trust / provenance properties

The `receipt` field is `{confidence,elapsedMs}` telemetry, not a signed Receipt v1. In practice confidence is omitted, leaving elapsed time (CAP-595/596; CAP-287; EF-006).

Facts have no content hash, signature, Merkle root, capsule, time anchor, or origin proof. `occam_verify` cannot verify them (TRUST-MODEL §2 C3; `ARTIFACT-ONTOLOGY.md:82`). Do not claim “verified knowledge extraction.”

## Failure / fallback behavior

| Code | Meaning | Evidence |
|---|---|---|
| `playbook_not_found` | No matching recipe | CAP-590/601 |
| `knowledge_schema_missing` | Resolved recipe lacks schema | CAP-590/601 |
| `page_class_unmatched` | No pattern/default class | CAP-590/601 |
| `knowledge_schema_empty` | Matched class has no fields | CAP-590/601 |
| `invalid_arguments` | URL/policy/field spec invalid | CAP-601 |
| `workers_unavailable` / `timeout` | Worker/path/deadline failure | CAP-592/601 |
| `http_401/403/429` | Initial HTTP failure; may trigger browser | CAP-593/601 |
| `extraction_failed` | Generic worker/network/parse failure | CAP-601 |

Failure may include `partialFacts` rebuilt from returned data (CAP-602). Challenge, login, thin, body-size, DNS, and TLS distinctions are generally absent because this spine lacks transcode classifiers (CAP-601).

## Platform differences

Worker spawning and process-group cleanup follow platform-specific mechanisms from `WorkerProcessGroup` (`PLATFORM-DIFFERENCES.md`). Private-key differences are not applicable because no Receipt v1 is produced. Extraction semantics have no declared OS-specific branch.

## Composition with other capabilities

- Consumes `playbook-resolution` for tiered schema/genome selection (CAP-590/491).
- Schemas may be drafted after `playbook-healing` and persisted by `playbook-authoring`.
- Bypasses acquisition/materialization spine despite fetching a URL (CAP-591).
- Does not feed `canonical-knowledge-ir`; live facts are a separate response model (CAP-330/333; `DEAD-OR-UNREACHABLE.md:12-13`).
- Does not compose with receipts/verification despite the field name (CAP-287).

## Known limitations

- Requires a resolvable nonempty schema (CAP-590).
- Browser fallback is anonymous, unpooled, and unconstrained by shared limiter (CAP-593/594).
- Row mode is worker-live but MCP-dead (CAP-600/EF-014).
- Confidence is dead (CAP-595).
- No Receipt v1 (CAP-596/EF-006).
- No body cap/DNS-pin parity on CSS worker (EF-043).
- No challenge/login/thin detection, robots, domain tiers, tokens, cache, managed providers, or post-processors (CAP-591).

## Engineering findings

- EF-006: telemetry named `Receipt` is not Receipt v1.
- EF-013: page-controlled Nuxt expression is evaluated in Node.
- EF-014: host parser never carries `base_selector`; worker row mode is unreachable from actual host plans.
- EF-043: CSS worker lacks DNS pinning and body cap.
- EF-055: extract-knowledge worker availability/configuration behavior is tracked in the canonical ledger.
- CAP-595/594: dead confidence and silent session downgrade.

## Code evidence

- `src/FFOccamMcp.Core/Tools/OccamExtractKnowledgeTool.cs:12-129`
- `src/FFOccamMcp.Core/Services/KnowledgeExtractService.cs:15-203`
- `src/FFOccamMcp.Core/Playbooks/KnowledgeSchemaPlanner.cs`
- `src/FFOccamMcp.Core/Extract/FieldSpecParser.cs`
- `src/FFOccamMcp.Core/Workers/CssExtractWorker.cs:13-130`
- `workers/css-extract/css-extract.mjs:31-128`
- `workers/css-extract/lib/css-schema-extract.mjs:51-163`
- CAP-590–602; CAP-287; ART-014/036; EF-006/013/014/043/055.

## Public-doc relevance

High. Public docs must describe the independent spine, required schema/page-class matching, real three parameters, field modes, browser/session limitations, failure codes, no token/body budget, and non-cryptographic telemetry receipt.

## Handbook relevance

Use for the Recipe D workflow: resolve schema → inspect page class → extract facts → handle partial facts. Include an explicit warning that these facts are not Receipt-v1-backed and that list/row schemas are not usable through the current host mapper.
