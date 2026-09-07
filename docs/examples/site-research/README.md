# Bounded site research

`occam research` discovers links (`occam_map`) then extracts pages
(`occam_transcode`) under URL, page, time, and byte budgets. Discovery and
extraction are **separate reports**. It is **not** a new MCP tool and not a
second crawler.

This recorded folder was assembled with `--from-json` from the compare-sources
golden excerpts (host toolchain `ff-occam/1.0.0-rc.2`). That is not a GitHub
Release `v1.0.0` certification.

| Folder | Seed | Stop |
|--------|------|------|
| [nginx-proxy](nginx-proxy/) | `https://nginx.org/en/docs/` | `budget_pages` after two successful extracts; off-site URL named in `discovery.outOfScope` |

Replay:

```bash
occam research --from-json docs/examples/site-research/_inputs/nginx-proxy.json \
  --out tmp/research --max-pages 2 --max-urls 8
```

Live (spawns the local MCP host):

```bash
occam research --seed https://nginx.org/en/docs/ \
  --focus "proxy_pass proxy_read_timeout" \
  --max-pages 4 --max-urls 16 --deadline-ms 60000 --max-bytes 1000000 \
  --out tmp/research
```

`--resume` continues the same `--out` folder. Ctrl+C records `stop.reason=cancelled`.
If a page fails, the folder is still written; do not fill gaps from memory.

Ranking used to order discovered URLs is the A1 spike — it does **not** change
`occam_search` default order. See [capability evaluation](../capability-eval/).
