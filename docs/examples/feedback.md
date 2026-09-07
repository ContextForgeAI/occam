# Feedback template

Use this when a recorded [workflow](gallery.md) or
[context pack](context-packs/),
[site-research folder](site-research/),
[docs change brief](docs-change-brief/), or
[citation inspect](citation-inspector/) missed something you needed.
Do not send secrets, cookies, or full page HTML.

Name the host you ran (`receipt.toolchain`, pack `manifest.toolchain`, or
`occam --help`). Compare it to the [release evidence ledger](release-evidence.md)
before calling the miss a Release `v1.0.0` defect.

## Task

What were you trying to do? One sentence.

## Expected content

Which command, condition, unit, or citation should have been in the result?

## What Occam returned

- Tool or CLI command (for example `occam pack` / `occam research` / `occam_transcode`)
- `ok` / `failure.code` if present
- Whether omissions were listed
- Host stamp (`receipt.toolchain` or `occam --help` version)

## Redacted diagnostics

Paste only:

- The URL(s)
- `contentHash` / `materializationKey` if relevant
- `compile.omitted` or pack `omissions.json`
- Settings (`max_tokens`, `focus_query`, `backend_policy`)

Strip cookies, `session_profile` ids, internal hosts, and personal paths.
