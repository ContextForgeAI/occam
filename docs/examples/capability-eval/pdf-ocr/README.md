# PDF / OCR capability decision

**Evaluation only.** This page scores the current text-layer PDF path and the
opt-in OCR helper. It does **not** ship a bundled OCR engine.

Re-run:

```bash
node scripts/lib/operator/pdf-ocr-eval.selftest.mjs
```

## Scores (fixture + one hand-built two-page PDF)

| Dimension | Score | What was measured |
|-----------|------:|-------------------|
| Reading order | 1.00 | Tokens from page 1 then page 2 stay in order |
| Tables | 0.23 | Cell words may survive; markdown tables are not reconstructed |
| Units | 0.67 | Units in the text stream stay; drawing-only units are lost |
| Page references | 1.00 | `---` page separators; in-body “page N” when present in the layer |
| Acquisition | 1.00 | Empty layer → `pdf_no_text_layer`; OCR off by default; map drops `.pdf`; typed walls stay typed |

Machine-readable snapshot: [`scores.json`](scores.json). These are scoped
fixture scores, not a live-PDF corpus.

## Decision

| Keep | Defer |
|------|-------|
| `unpdf` text-layer extract on the HTTP path | Bundled OCR engine (Tesseract or similar) |
| Opt-in `OCCAM_PDF_OCR` + operator-supplied `OCCAM_PDF_OCR_BIN` | Table reconstruction from PDF drawing operators |
| Honest empty-layer failure | Changing `occam_map` to treat `.pdf` as a page |

Fetch a known PDF URL with `occam_transcode`. Do not expect `occam_map` to
list `.pdf` links — they are filtered as assets.

## Implementation budget (after scoring)

Estimates for someone who already knows this repository. Not a schedule.

| If we later choose to… | Working days | Why it is deferred |
|------------------------|-------------:|--------------------|
| Reconstruct simple PDF tables / keep units beside numbers | 8–12 | Needs a second parser and new fixtures; current flatten is honest |
| Vendor a bundled OCR engine | 18–25 | Binaries, licenses, platform builds; Occam must not pretend it ships one |
| Include `.pdf` links in map/digest discovery | 2–3 | Changes discovery semantics; research CLI can already take an explicit PDF seed |

Recommended next investment is **not** OCR. Wait for a specialized corpus or
repeated user misses on scanned PDFs.

Configuration: [PDF OCR env](../../../configuration.md#pdf-ocr-optional-scanned-pdfs).
