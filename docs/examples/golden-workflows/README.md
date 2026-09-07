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

**Host stamp:** recaptured 2026-09-07 through the workspace MCP
host. Receipts report `toolchain: ff-occam/1.0.0-rc.2`. That is **not** a
claim that GitHub Release host `v1.0.0` is byte-identical. Re-run with the
same arguments to refresh. MDN `contentHash` matched the 2026-09-05
recording; nginx excerpts were refreshed.

Gallery view: [Workflow gallery](../gallery.md) ·
ledger: [Release evidence](../release-evidence.md) ·
missed a detail: [Feedback](../feedback.md).
