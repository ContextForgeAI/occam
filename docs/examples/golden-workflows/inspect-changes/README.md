# Inspect changes since a previous read

Optional configuration: pass the prior `contentHash` as `if_none_match` on
the same URL **and** the same materialization settings.

This capture used GitHub Release host **v1.1.1**. It returned
`unchanged:true` and empty markdown — the second read matched the first-read
hash from the same session.

- [result-summary.json](result-summary.json)
- [settings.json](settings.json) · [input-metadata.json](input-metadata.json)

`diff_against` is a separate opt-in for block-level deltas and was not used
here.
