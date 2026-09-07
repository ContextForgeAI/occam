# Citation inspector

`occam cite` calls `occam_claim_check` and writes the retrieved blocks plus
an explicit reminder that **you** judge support vs refute. `verdict` stays
`not_evaluated`. `found:false` is a retrieval-negative, not a fetch failure.

Not a new MCP tool and not a truth detector.

Recorded folders use toolchain `ff-occam/1.0.0-rc.2`. That is not a GitHub
Release `v1.0.0` certification.

| Folder | What it shows |
|--------|----------------|
| [found](found/) | Live MDN closure claim — `found:true`, passage returned |
| [not-found](not-found/) | Invented API-key claim — `found:false`, `proven:true` (complete leaf set, no BM25 hit) |
| [fetch-failed](fetch-failed/) | Page unread — `ok:false timeout` (constructed) |

```bash
occam cite --from-json docs/examples/citation-inspector/_inputs/found.json --out tmp/cite
```

Live (uses `OCCAM_PROFILE=researcher` so `occam_claim_check` is exposed):

```bash
occam cite --claim "A closure remembers variables after its parent scope exits." \
  --url https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide/Functions \
  --out tmp/cite
```
