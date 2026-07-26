# SESSION-LIFECYCLE (Wave 3 primary)

**Agent:** [S3-05 Session](9ecf69af-92db-406f-8022-8ad6ebb19c11) · full evidence: [`subsystems/session-lifecycle.md`](subsystems/session-lifecycle.md)  
**New CAPs:** CAP-880…885 (prefer reuse of Wave 1 CAP-150…194 / Wave 2 CAP-423/424, 527, 543, 594)

## State model (code)

`session_profile` is **not** one contract. Twelve surfaces accept the param; three tiers apply:

| Tier | Behavior | Surfaces |
|------|----------|----------|
| **1 — full** | Headers + Playwright `storageState` via `TranscodePipeline` / `FetchPreflight` / `FetchHeadersScope` | `occam_transcode`, `occam_digest`, `occam_claim_check`, `occam_attest`, `occam_dataset_export`, `occam_batch_submit`, `occam_watch`, `occam_crosscheck` |
| **2 — HTTP-only** | Headers apply; browser/`storageState` never in path | `occam_probe`, `occam_map` |
| **3 — headers-only browser fallback** | Browser may run; `storageState` silently dropped | `occam_playbook_heal` (DomSkeleton), `occam_extract_knowledge` (CssExtractWorker — no storageState param exists) |

Tool descriptions that say “same as occam_transcode” for heal/extract are **misleading** relative to Tier 3.

## Pool / isolation refinements (new)

- **CAP-881:** Per-call GUID header temp-file path forces browser-pool recycle on every headered/session call (path inequality, not content hash) — warm reuse only for fully anonymous traffic.
- **CAP-882:** Refines EF-002: real cookie-bleed vector is **anonymous→anonymous** shared `BrowserContext` (up to 10 runs / 400 MB), not session→session (those recycle first).
- **CAP-883:** `L2_SESSION_OK` covers only transcode/probe/digest/map — no gate for pool recycle or Tier-3 drops.
- **CAP-884:** Operator CLI and MCP host share the same default sessions root (`~/.occam/sessions`).
- **CAP-885:** See subsystem report for remaining cross-tool notes.

## Verify session drop

Confirmed in code; **no product fix in this wave**. Recorded as CAP-883 + existing EF-017.
