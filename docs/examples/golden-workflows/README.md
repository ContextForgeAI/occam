# Golden workflows

Three recorded agent jobs with machine-readable settings and outputs.
These are inspectable captures, not global quality claims.

| Workflow | Job | Tools | Public URL(s) |
|----------|-----|-------|---------------|
| [Understand a documentation instruction](understand-instruction/) | Command, steps, conditions, citations, omissions | `occam_transcode` | MDN JavaScript Functions |
| [Compare known sources](compare-sources/) | Two official docs, honest focus reporting | `occam_digest` | nginx proxy module + beginner's guide |
| [Inspect changes](inspect-changes/) | Cheap re-read after a stored hash | `occam_transcode` + `if_none_match` | Same MDN Functions URL |

Each folder has `settings.json`, `input-metadata.json`, captured Markdown,
and a constrained or unchanged outcome. Screenshots are not evidence.

**Host stamp:** recaptured 2026-09-07 from GitHub Release **v1.1.1**
(win-x64 tarball, commit `a27c523`). Receipts report
`toolchain: ff-occam/1.1.1`. That is **not** the ledger's
`publicBuild=true` identity (`ff-occam/1.0.0`). Re-run with the same
arguments to refresh.

Gallery view: [Workflow gallery](../gallery.md) ·
ledger: [Release evidence](../release-evidence.md) ·
missed a detail: [Feedback](../feedback.md).
