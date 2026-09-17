# Trust

Hub for integrity and proof surfaces. This page **links** deep docs; it does not
duplicate their contracts.

| Topic | Deep page |
|-------|-----------|
| Receipts | [receipts.md](receipts.md) |
| Verify modes | [receipt_verification.md](receipt_verification.md) |
| Canary protocol | [adr/0010-proof-of-read-canary.md](adr/0010-proof-of-read-canary.md), [PROBE_PROTOCOL.md](../PROBE_PROTOCOL.md) |
| Honest failures | [trust/honest-failures.md](trust/honest-failures.md) |
| Installation safety | [trust/installation-safety.md](trust/installation-safety.md) |
| Datasets | [datasets.md](datasets.md) |
| Capability inventory | [CAPABILITIES.md](CAPABILITIES.md) |

## Proof-of-read canary

HMAC-SHA256 sentinel bound to `(sessionId, time bucket)`
(`Canary/CanarySecret.cs:100–110`). Issuance is audited
(`CanaryService.cs:113+`, `CanaryIssueLog.cs`). Verification is a fixed
precedence state machine (`CanaryVerifier.cs:50+`).

| Wire verdict | Meaning |
|--------------|---------|
| `READ_VERIFIED` | Authentic, issued, fresh window |
| `READ_STALE` | Authentic, issued, older bucket |
| `HALLUCINATED` | No match in recognised window |
| `REPLAY_SUSPECT` | Authentic crypto, never issued by this host |

Source: `Canary/CanaryModels.cs:8–49`.

**Surface:** CLI `occam canary` + probe HTTP host **and** MCP tools
`occam_canary_issue` / `occam_canary_verify` (reader + full profiles;
`Tools/OccamCanaryIssueTool.cs`, `CanaryCliVerbs.cs`, `CanaryProbeServerHost.cs`).
Registered in `OccamToolNames`.

Env: `OCCAM_CANARY_*` — see [configuration.md](configuration.md).

## Receipts + Merkle

- Sign: ECDSA P-256 (`Receipts/ReceiptSigner.cs:13`)
- Policy: on by default; `OCCAM_RECEIPTS=off|0|false` disables (`ReceiptsPolicy.cs:12`)
- Blocks: ordered SHA-256 Merkle (`MerkleTree.cs:13`)
- Capsule: optional `occam://capsule/…` (`CapsuleCodec.cs`)
- Optional RFC3161 time anchor: `OCCAM_TIME_ANCHOR` + `OCCAM_TSA_URL`
  (`TimeAnchorService.cs`) — opt-in

A valid signature proves integrity relative to a key. It does **not** prove
semantic truth or authentic origin of the website.

## MCP trust tools

| Tool | Role | Code |
|------|------|------|
| `occam_verify` | offline / live / prove / citation / history | `Tools/OccamVerifyTool.cs:25–35` |
| `occam_canary_issue` | Mint canary URL + session (no sentinel) | `Tools/OccamCanaryIssueTool.cs` |
| `occam_canary_verify` | Adjudicate claimed sentinel → verdict | `Tools/OccamCanaryVerifyTool.cs` |
| `occam_claim_check` | Relevant blocks + Merkle citation; you judge support | `Claims/ClaimCheckService.cs:25`, tool `:18` |
| `occam_attest` | Fail-closed status (`supported` / …); Merkle ≠ support | `Attest/AttestService.cs`, tool `:22` |
| `occam_dataset_export` | Per-row receipts + manifest Merkle root | `Dataset/DatasetExportService.cs:25`, tool `:21` |

Tool pages: [occam_verify](tools/occam_verify.md) ·
[occam_canary_issue](tools/occam_canary_issue.md) ·
[occam_canary_verify](tools/occam_canary_verify.md) ·
[occam_claim_check](tools/occam_claim_check.md) ·
[occam_attest](tools/occam_attest.md) ·
[occam_dataset_export](tools/occam_dataset_export.md).

## Handoff checklist

1. Extract with receipts on (default).
2. Persist `receipt` (and `contentHash` / leaves as needed).
3. Later: `occam_verify` offline or live; `prove` / `citation` for block membership.
4. For claims in a report: `occam_attest` — gate on `status`, not BM25 alone.
5. For an auditable URL set: `occam_dataset_export` then per-row / manifest verify.
6. For agent-read proofs: `occam_canary_issue` → fetch → `occam_canary_verify` (or CLI `occam canary`).

## Related

- [CASCADE](CASCADE.md) — how a read is acquired before a receipt is attached
- [CAPABILITIES](CAPABILITIES.md) — status of each trust feature
- Handbook: [14 what a receipt proves](handbook/14-what-a-receipt-proves.md),
  [15 verifying](handbook/15-verifying.md),
  [16 evidence for claims](handbook/16-evidence-for-claims.md)
