# Fixture sources and immutability

Golden and regression HTML/XML under `benchmarks/` are **test inputs**, not live
downloads at gate time. Do not “refresh” captures casually — changing bytes changes
assertions.

---

## Immutability policy

1. Prefer **synthetic** minimal fixtures for new cases.
2. Captured real pages must be **frozen**: commit the bytes, record source URL and
   approximate capture date here, and attribute the upstream license in
   [THIRD_PARTY_NOTICES.md](../../THIRD_PARTY_NOTICES.md).
3. After any intentional fixture edit, re-run the consuming gate
   (`L9` golden / `rc2-regression` / related units) and update expectations in the
   same change.
4. Fixture servers must not require remote CSS/JS; extractors operate on local HTML.

---

## Captured third-party pages (L9 golden)

| File | Source (approximate) | Capture note | License / terms |
|------|----------------------|--------------|-----------------|
| `benchmarks/l0-gate/fixtures/golden/mdn-doc.html` | MDN — “HTTP request methods” documentation page | Frozen snapshot used for extract/marker assertions; page chrome may include third-party consent/script references that are **not fetched** at test time | MDN content: typically CC-BY-SA (see page / MDN terms); retain attribution |
| `benchmarks/l0-gate/fixtures/golden/nginx-doc.html` | nginx.org — Beginner’s Guide | Frozen snapshot; may contain promotional chrome from the capture era | nginx documentation terms; retain attribution |
| `benchmarks/l0-gate/fixtures/golden/wikipedia-article.html` | English Wikipedia — “Markdown” article | Frozen snapshot; MediaWiki generator/revision metadata may appear in HTML | **CC BY-SA** (Wikipedia); retain attribution |

Exact upstream revision IDs may appear inside the frozen HTML. Treat those as
historical metadata of the capture, not as a live pin.

---

## Synthetic fixtures

All other `*.html` / `*.xml` under `benchmarks/l0-gate/fixtures/golden/` and
`benchmarks/rc2-regression/fixtures/` are handwritten minimal pages for detectors and
regression cases. No third-party copyright claim beyond Occam’s own license.

---

## Product assets (not gate fixtures)

| File | Note |
|------|------|
| `packages/vscode-extension/assets/icon.svg` | Extension icon (private/experimental package; not part of the public RC snapshot) |
