# Release evidence

Every demonstration in this repository names the host that produced it.
Machine-readable ledger: [`release-evidence.json`](release-evidence.json)
(checked by `node scripts/check-docs.mjs`).

**Public build** here means the published GitHub Release host identity
`ff-occam/1.0.0`. A named workspace or RC toolchain is still required — it is
just not a Release certification.

| Id | Artifact | Captured | Toolchain | Public Release `1.0.0`? |
|----|----------|----------|-----------|-------------------------|
| `current-proof-example-com` | [success-result.json](current-proof/success-result.json) | 2026-08-28 | `ff-occam/1.0.0` | Yes |
| `current-proof-failure` | [failure-result.json](current-proof/failure-result.json) | 2026-08-28 | `ff-occam/1.0.0` | Yes |
| `current-proof-representative` | [representative-measurement.json](current-proof/representative-measurement.json) | 2026-08-28 | source revision `b3c212c6…` | No — controlled fixture |
| `golden-understand-instruction` | [input-metadata.json](golden-workflows/understand-instruction/input-metadata.json) | 2026-09-07 | `ff-occam/1.0.0-rc.2` | No — workspace MCP |
| `golden-compare-sources` | [input-metadata.json](golden-workflows/compare-sources/input-metadata.json) | 2026-09-07 | `ff-occam/1.0.0-rc.2` | No — workspace MCP |
| `golden-inspect-changes` | [input-metadata.json](golden-workflows/inspect-changes/input-metadata.json) | 2026-09-07 | `ff-occam/1.0.0-rc.2` | No — workspace MCP |
| `pack-understand-instruction` | [manifest.json](context-packs/understand-instruction/manifest.json) | 2026-09-05 | `ff-occam/1.0.0-rc.2` | No — assembled from golden |
| `pack-compare-sources` | [manifest.json](context-packs/compare-sources/manifest.json) | 2026-09-05 | `ff-occam/1.0.0-rc.2` | No — assembled from golden |
| `pack-inspect-changes` | [manifest.json](context-packs/inspect-changes/manifest.json) | 2026-09-05 | `ff-occam/1.0.0-rc.2` | No — assembled from golden |
| `research-nginx-proxy` | [research-state.json](site-research/nginx-proxy/research-state.json) | 2026-09-06 | `ff-occam/1.0.0-rc.2` | No — assembled from golden |
| `brief-mdn-unchanged` | [brief-state.json](docs-change-brief/mdn-unchanged/brief-state.json) | 2026-09-07 | `ff-occam/1.0.0-rc.2` | No — recapture / fixture |
| `cite-found` | [inspect.json](citation-inspector/found/inspect.json) | 2026-09-07 | `ff-occam/1.0.0-rc.2` | No — workspace MCP |

Reproduce the public-build smoke from
[current proof](current-proof/README.md#reproduce). Refresh a workflow from
its `settings.json` on whatever host you have, then compare the new
`receipt.toolchain` (or `occam --help` version) to this table. Do not cite a
workspace capture as "Release v1.0.0 did this."

`occam pack`, `occam research`, `occam brief`, and `occam cite` ship in GitHub
Release host `1.1.1`. Recorded example rows remain workspace
`ff-occam/1.0.0-rc.2` until recapture; do not set `publicBuild=true` on them yet.

Gallery: [Workflow gallery](gallery.md). Missed content:
[feedback template](feedback.md).
