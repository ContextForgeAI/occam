# Canonical knowledge IR

**Slug:** `canonical-knowledge-ir` · **Product system:** PS-4 Knowledge extraction · **CAPs:** 4 · **Public relevance:** MEDIUM, but not a usable feature.

## What it is

A shipped set of internal codecs and canonical knowledge types for facts, entities, relationships, spans, and semantic structures (CAP-328/330/332/333; `Knowledge/Canonical/**`; `Codecs/**`).

Most of this family is dead or computed-and-discarded. Whole-glob Core compilation means the types ship in the AOT binary, but no public surface makes them usable (C8; `SHIPPED-CODE-MAP.md`; `DEAD-OR-UNREACHABLE.md:11-13`).

## Why it exists

The code models an intended canonical intermediate representation between worker extraction and output codecs. It appears designed to support compact markdown/JSON knowledge encodings and provenance attachment (CAP-328/330/332/333).

That architectural intention is not equivalent to a reachable product capability.

## User-visible entrypoints

None.

`occam_transcode` can perform internal canonical extraction work, but only the markdown passthrough response path is selected; no MCP `codec` parameter selects `CompactMarkdownCodec` or `JsonKnowledgeCodec` (CAP-328; `DEAD-OR-UNREACHABLE.md:11`).

`occam_extract_knowledge` returns its own `facts[]` model and does not instantiate or emit this IR (CAP-287/590; `PRODUCT-ARCHITECTURE.md:87`).

## Core behavior

The live transcode path can build canonical knowledge internally, then discard it when the selected response codec is markdown passthrough (CAP-330/333; `DEAD-OR-UNREACHABLE.md:12`).

Canonical `Fact`, `Entity`, and `Relationship` types are defined but never instantiated in the production source call graph (CAP-332; `DEAD-OR-UNREACHABLE.md:13`).

## Advanced behavior

| Component | Status | Evidence |
|---|---|---|
| `CompactMarkdownCodec` | Registered/compiled; no selectable MCP path | CAP-328 |
| `JsonKnowledgeCodec` | Registered/compiled; no selectable MCP path | CAP-328 |
| Canonical extraction/materializer | Runs in portions of transcode then result discarded | CAP-330/333 |
| `Fact` / `Entity` / `Relationship` | Defined; no production instantiation | CAP-332 |

## Automatic / silent behavior

Internal canonical extraction work may execute on every transcode because the pipeline forces internal block/table features, even though the canonical result is not exposed (AUTOMATIC-BEHAVIORS #18; `PRODUCT-ARCHITECTURE.md:61,197`).

This is internal cost without a user-visible canonical artifact (CAP-330/333).

## Parameters

None.

No public `codec`, IR format, entity extraction, relationship extraction, or canonical-knowledge response parameter exists (CAP-328).

## Configuration

None.

No environment variable makes the dead codecs selectable or exposes the canonical model.

## Backends

Not applicable as an independent feature.

The computed path sits after normal transcode acquisition/materialization inputs; it does not own an HTTP, browser, managed, or CSS backend (CAP-330/333).

## Sessions / state

None.

The internal objects are ephemeral and discarded. No canonical-IR store, cache, index, or export is present.

## Network behavior

Not applicable.

Any network activity belongs to the caller's transcode/acquisition path, not to this family.

## Artifacts produced

No user-visible canonical IR artifact is produced.

The existing artifact ontology intentionally lists no artifact for this family; ART-014 is the separate `occam_extract_knowledge` facts response and must not be conflated with canonical IR (`ARTIFACT-ONTOLOGY.md:78-86`).

## Trust / provenance properties

None on a public surface.

Dead/internal `KnowledgeProvenance` bridge fields and `MaterializedProvenanceResolver` do not make canonical IR verifiable because the resolver has no callers (CAP-285/286; `DEAD-OR-UNREACHABLE.md:7,26`; TRUST-MODEL §11).

## Failure / fallback behavior

No public failure contract exists because there is no public invocation.

The live transcode path falls through to markdown passthrough rather than reporting canonical-IR failure or availability (CAP-328/330/333).

## Platform differences

None documented for semantics.

Whole-glob compilation applies across shipped Core builds; “dead” does not mean “absent from the binary” (C8; `CANONICAL-AUDIT-INDEX.md:74-75`).

## Composition with other capabilities

- Internally adjacent to PS-2 materialization and transcode blocks/tables (CAP-330/333).
- Not consumed by `schema-knowledge-extraction`; that tool has a separate schema/facts model (CAP-590).
- `MaterializedProvenanceResolver` is also dead and does not bridge the IR to PS-6 verification (CAP-286).

## Known limitations

- No public entrypoint.
- No selectable codec parameter.
- No emitted artifact.
- Canonical types are not instantiated.
- Computed output is discarded.
- No persistence, verification, or documented consumer.

Therefore this family must not be presented as a usable “knowledge graph,” entity API, relationship API, canonical export, or structured response feature.

## Engineering findings

- CAP-328: registered codecs are unreachable.
- CAP-330/CAP-333: canonical extraction runs and is discarded.
- CAP-332: canonical model types are never instantiated.
- C8: these dead types still ship because Core uses whole-glob compilation.

No new EF is proposed; the dead/unreachable ledger already records the condition.

## Code evidence

- `src/FFOccamMcp.Core/Knowledge/Canonical/**`
- `src/FFOccamMcp.Core/Codecs/**`
- `src/FFOccamMcp.Core/Knowledge/MaterializationPlanner.cs`
- `src/FFOccamMcp.Core/Routing/TranscodePipeline.cs`
- CAP-328, CAP-330, CAP-332, CAP-333.
- `docs-audit/DEAD-OR-UNREACHABLE.md:11-13`
- `docs-audit/PRODUCT-ARCHITECTURE.md:197`
- Conflict C8 in `CANONICAL-AUDIT-INDEX.md:74-75`.

## Public-doc relevance

Do not document as shipped user functionality. At most, an architecture note may say internal canonical-IR scaffolding ships but is unreachable and discarded.

Any public claim that Occam exposes canonical facts/entities/relationships from this IR would be false.

## Handbook relevance

None for user workflows.

For maintainers, use as an explicit dead-code boundary and decision point: either wire a real entrypoint/artifact with tests and trust semantics, or remove the unused shipped surface. Until then, handbook authors should route structured extraction to `occam_extract_knowledge` and state its separate limitations.
