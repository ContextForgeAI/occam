# Docs change brief

`occam brief` re-reads chosen documentation URLs with `if_none_match` and
writes a folder: what is unchanged, what changed, what failed. Significant
items are a **keyword heuristic** (deprecation / default / breaking), not a
semantic changelog. It is **not** a new MCP tool and not `occam_watch`.

Recorded folders use toolchain `ff-occam/1.0.0-rc.2`. That is not a GitHub
Release `v1.0.0` certification.

| Folder | What it shows |
|--------|----------------|
| [mdn-unchanged](mdn-unchanged/) | Live recapture: same MDN hash → `unchanged:true` |
| [nginx-changed](nginx-changed/) | **Constructed** markdown to exercise `significance=significant` — not a live nginx.org extract |
| [typed-failure](typed-failure/) | `ok:false captcha_or_challenge` preserved |

```bash
occam brief --from-json docs/examples/docs-change-brief/_inputs/mdn-unchanged.json --out tmp/brief
```

Live:

```bash
occam brief --url https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide/Functions \
  --against bda66461b52d3d0aea8541e47706ebdbe09c242cc5ba2d537c3cb0293517a738 \
  --focus "function scope closures" --out tmp/brief
```

`--resume` reuses `contentHash` values from an existing `--out` folder.
If a URL fails, the brief is still written. Do not fill gaps from memory.
