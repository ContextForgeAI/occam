# Third-party notices

This file records attribution for third-party materials that are **copied into this
repository** (not ordinary npm/NuGet runtime dependencies resolved at install time).

---

## Graphify (agent skill)

| Field | Value |
|-------|-------|
| Component | Graphify agent skill (Cursor / Claude Code skill files) |
| Paths in this repo | `.agents/skills/graphify/`, `.claude/skills/graphify/` |
| Upstream project | [https://github.com/safishamsi/graphify](https://github.com/safishamsi/graphify) |
| Upstream version (vendored marker) | `0.8.41` (from `.graphify_version` in the skill trees) |
| License | MIT |
| Copyright | Copyright (c) 2026 Safi Shamsi |

The skill files may have been adapted for Occam client integration (for example platform-specific
invocation notes). Upstream license terms still apply to the Graphify portions.

### MIT License text (upstream)

```text
MIT License

Copyright (c) 2026 Safi Shamsi

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

Evidence: upstream `LICENSE` at tag/path corresponding to Graphify `v0.8.41`
(`https://raw.githubusercontent.com/safishamsi/graphify/v0.8.41/LICENSE`), plus the
in-tree skill sponsor link to `https://github.com/sponsors/safishamsi`.

> Note: Graphify skill trees may be omitted from some public snapshots. Attribution
> still applies wherever the skill files are present.

---

## Frozen documentation page captures (gate fixtures)

These HTML snapshots are used only as deterministic test fixtures. They are not
re-fetched during gates. Source URLs, immutability rules, and capture notes:
[docs/maintenance/FIXTURE_SOURCES.md](docs/maintenance/FIXTURE_SOURCES.md).

| Capture | Upstream | Attribution |
|---------|----------|-------------|
| `benchmarks/l0-gate/fixtures/golden/mdn-doc.html` | MDN Web Docs (HTTP request methods) | MDN content is typically offered under Creative Commons terms (see the live MDN page footer / Mozilla terms). Occam retains a frozen copy for regression only. |
| `benchmarks/l0-gate/fixtures/golden/nginx-doc.html` | nginx.org Beginner’s Guide | nginx documentation; frozen copy for regression only. |
| `benchmarks/l0-gate/fixtures/golden/wikipedia-article.html` | English Wikipedia article “Markdown” | **CC BY-SA** (Wikipedia). The frozen HTML may embed MediaWiki metadata from the capture era. |

Synthetic fixtures under the same directories are original Occam test material under the
repository LICENSE.
