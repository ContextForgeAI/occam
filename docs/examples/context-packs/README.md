# Context packs

A context pack is a **folder** for one task: selected sources, a declared
budget, cited excerpts, omissions, and a manifest that counts wrapper
overhead. It is assembled by `occam pack` from existing tools
(`occam_transcode`, `occam_digest`, `occam_search`). It is **not** a new
MCP tool and not a second extractor.

These three packs were assembled from the [golden workflow](../golden-workflows/)
captures (2026-09-05, host toolchain `ff-occam/1.0.0-rc.2`). That is not a
GitHub Release `v1.0.0` certification.

| Pack | Task | Outcome |
|------|------|---------|
| [Understand an instruction](understand-instruction/) | MDN function scope / closures | Focus hit; 18 sections omitted and named |
| [Compare sources](compare-sources/) | nginx `proxy_pass` vs `proxy_read_timeout` | Both pages ok; focus weak; timeout not invented |
| [Inspect changes](inspect-changes/) | Same MDN URL + stored hash | `unchanged:true`, empty body preserved |

Each folder has `manifest.json`, `sources.json`, `omissions.json`, and
`excerpts.txt`. Rebuild from the recorded payloads:

```bash
occam pack --task "…" --from-json docs/examples/context-packs/_inputs/understand-instruction.json --out tmp/pack --budget 800 --focus "function scope closures"
```

Live (spawns the local MCP host):

```bash
occam pack --task "Show how function scope and closures work" \
  --url https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide/Functions \
  --focus "function scope closures" --budget 800 \
  --out tmp/pack
```

If a source fails, the pack is still written and the failure stays in
`sources.json`. Do not fill gaps from memory.

Gallery: [Workflow gallery](../gallery.md) ·
named builds: [Release evidence](../release-evidence.md) ·
feedback: [what to send](../feedback.md).
