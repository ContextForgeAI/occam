# Appendix — Status labels

Brief definitions for exposure and documentation status labels used across the Docs v3 handbook and `DOCUMENTATION-EXPOSURE-MATRIX.md`.

---

## STABLE

**Meaning:** Shipped, reachable in default or documented configurations, and the handbook treatment reflects current code behavior.

**Use when:** Core MCP tools, honesty contract, acquisition ladder, token budget, receipts, verification, exposure model, operator install/connect/doctor paths, and other always-on product surfaces.

**Does not imply:** Truth/origin/identity guarantees, GA npm install, cosign-verified supply chain, or that every edge case has a runtime CHECK.

---

## LIMITED

**Meaning:** Shipped and reachable, but with named ceilings, asymmetric surfaces, or incomplete env coverage that must appear in the same section as the feature name.

**Use when:**

- MCP vs CLI verify asymmetry (`manifest` CLI-only; `live` MCP-only).
- Session tier matrix (probe/map/heal/extract reach different session depth).
- Managed acquisition (third-party egress; not a `backend_policy` value).
- `OCCAM_RECEIPTS=off` (signing policy incomplete — key mint and playbook save still sign).
- Claims/attest vocabulary (retrieval and heuristics, not proof).
- Playbook v1 signatures (gate fields unsigned; v2 improves integrity scope only).

**Synonym in matrix:** Often overlaps `PUBLIC_ADVANCED` or `OPERATOR` with explicit honesty paragraphs.

---

## EXPERIMENTAL

**Meaning:** Opt-in via env flag or explicit parameter; behavior and retention known to have gaps; not part of default `tools/list`.

**Use when:**

- `OCCAM_WATCH_MCP` / `occam_watch`
- `OCCAM_BATCH_MCP` / batch tools and related BatchServer mode
- `OCCAM_CONSENSUS_MCP` / `occam_crosscheck` (multi-source comparison only — never consensus proof)
- `OCCAM_ATLAS_MCP` / `occam_failure_atlas`
- `cache_ttl_s` response cache
- Managed provider backends when enabled
- npm `@ff-occam/mcp` (NOT GA — treat as experimental / non-public install path per OD-3)

**Rule:** State the env gate and honesty limits in the **same paragraph** as the tool or feature name.

---

## INTERNAL

**Meaning:** Mechanism exists in the shipped binary or repo but is not a user-facing product capability page — maintainer, contributor, or handbook-only depth.

**Use when:**

- Architecture internals ([Chapter 26](26-architecture-internals.md))
- Dead-but-shipped types and unreachable code paths
- Falsification protocol ([Chapter 27](27-checking-this-book.md))
- Engineering audit artifacts under `docs-audit/`
- Cosign release `.bundle` — required only when `signaturePolicy=required-cosign-v1` (published `1.0.0-rc.3`); legacy `1.0.0-rc.2` remains SHA-256-only; authenticity ≠ page truth
- Gate markers, PB codenames, and maintainer runbooks

**Rule:** May appear in handbook pessimism; do not headline in public task guides as product features.

---

## Quick reference table

| Label | Default visible? | Trust claims allowed? | Example |
|-------|------------------|----------------------|---------|
| STABLE | Yes (core path) | Integrity-only, with forbidden-claims list | `occam_transcode`, honesty contract |
| LIMITED | Yes, with caveats | Same, plus surface asymmetry called out | CLI manifest verify, session tiers |
| EXPERIMENTAL | No — env/param gate | Limits-first; no consensus proof | watch, batch, crosscheck |
| INTERNAL | Handbook / engineering | N/A — not product marketing | dead register, audit ledgers |

---

## Related exposure classes (matrix)

The handbook status labels map to family-level classes in `DOCUMENTATION-EXPOSURE-MATRIX.md`:

| Matrix class | Handbook label (typical) |
|--------------|-------------------------|
| `PUBLIC_CORE` | STABLE |
| `PUBLIC_ADVANCED` | STABLE or LIMITED |
| `OPERATOR` | STABLE (operator-first) |
| `EXPERIMENTAL` | EXPERIMENTAL |
| `DO_NOT_DOCUMENT_AS_FEATURE` | INTERNAL or LIMITED warning only |
| `REFERENCE_ONLY` | STABLE in reference docs |

When labels disagree, the **honesty schema** (`docs-audit/HONESTY-SCHEMA-MAP.md`) and **owner decisions** (`docs-audit/OWNER-DECISIONS.md`) override promotional naming.
