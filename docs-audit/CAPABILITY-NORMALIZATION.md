# CAPABILITY-NORMALIZATION (Wave 2 notes)

Do **not** renumber existing CAP IDs. Mark for later cleanup.

## KEEP (high product value)

Surface taxonomy, routing cascade, managed providers, proxy rotation, sessions, receipts/Merkle/capsules, profile/opt-in matrix, tool-specific honesty contracts (claim_check/attest/verify/dataset), playbook tier trust model.

## MERGE_CANDIDATE

| Cluster | IDs | Rationale |
|---------|-----|-----------|
| Session headers-only gaps | CAP-424, CAP-527, CAP-543, CAP-594 | Same product gap across tools |
| No token budget on pipeline callers | CAP-691, CAP-771 | Same pattern: TranscodePipeline without budget |
| Forced playbook_policy=auto | CAP-693, CAP-772, attest edges | Shared silent force |
| Reduced receipts | CAP-457, CAP-775, CAP-776 | Dataset/digest receipt weakening |
| Lint vs save/resolve drift | CAP-756, CAP-759–762 | One “parser triad drift” product issue |
| Fake/non-Receipt receipt | CAP-287, CAP-596 | Same naming honesty issue |

## IMPLEMENTATION_DETAIL (keep in evidence, de-emphasize in public docs)

Many CAP-53x heal scoring internals, CAP-75x individual lint field checks, CAP-42x probe byte caps, CAP-52x map filter minutiae — valuable for handbook, not landing-page capabilities.

## DEAD/UNREACHABLE

See `DEAD-OR-UNREACHABLE.md`. Wave 2 adds: CAP-436 (probe), CAP-552/553 (heal), CAP-600 (row-mode dead to host), CAP-496 (resolve swallows schema failure codes).

## NEEDS_REVIEW

| Item | Why |
|------|-----|
| CAP count inflation (490→674 W3) | Many fine-grained IDs; normalize before public doc generation |
| Graph edge noise | some non-standard rel names from agents |
| “Product capability” bar | Re-apply KEEP vs IMPLEMENTATION_DETAIL before handbook chapters |

## Wave 4 second pass (prefer edges/corrections over new CAPs)

| Action | Target | Rationale |
|--------|--------|-----------|
| **CORRECT prose** | CAP-052 / CAP-104 | Cascade: 404/410 + `IsPublicReferencePage`; FailureRanking fallback; managed fail ≠ surface (EF-056) |
| **CORRECT / edge** | CAP-106 | Document `ThinExtractBrowserExhausted` stop (GAP-016) |
| **CORRECT** | CAP-758 | Drop dead `PlaybookCommunitySanitizer` citation (EF-047) |
| **CORRECT** | CAP-600 / EF-014 | Host never sends `base_selector` — row-mode dead earlier than modeled (W4-C) |
| **CORRECT** | CAP-021 | Content-Length framing is WS adapter, not stdio (GAP-011) |
| **CORRECT** | CAP-1029 | HEALTHCHECK line is broken in practice (EF-051) |
| **CORRECT** | CAP-1031 | Marketplace happy-path only; add skip→auto-merge + cosign break (EF-052/053) |
| **ADD EDGE** | CAP-151 ↛ css-extract | SSRF/body-cap parity gap (EF-043) |
| **ADD EDGE** | receipts master ↛ playbook_save | Already EF-005; reinforce |
| **ADD ART/FLOW** | ART-034…039, FLOW-019…022 | Reverse audits — no new CAP families required |
| **DO NOT MINT** | CAP-NEW-A/B/C/D/E/F/G bulk | Fold into corrections/EFs/edges; avoid 674→750 inflation |
| **OPTIONAL small CAPs** (defer to doc synthesis) | InstallShared×DI visibility; printable-escapes; onboard env injection; name-wide kill scope | Only if handbook needs explicit product-facing names |

**Wave 4 CAP growth policy applied:** genuinely new product *capabilities* were rare; most findings are wrong prose, missing edges, artifacts, failure/security semantics, or EFs.
