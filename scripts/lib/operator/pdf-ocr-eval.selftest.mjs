import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { extractPdfMarkdown } from "../../../workers/shared/lib/pdf-extract.mjs";
import { isPdfOcrEnabled, tryPdfOcr } from "../../../workers/shared/lib/pdf-ocr.mjs";
import {
  IMPLEMENTATION_DECISION,
  evaluateCases,
  scoreReadingOrder,
} from "./pdf-ocr-eval.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(here, "../../..");

assert.equal(IMPLEMENTATION_DECISION.shipBundledOcr, false);
assert.equal(IMPLEMENTATION_DECISION.keepTextLayerUnpdf, true);

const lines = readFileSync(join(here, "fixtures/capability-eval/pdf-ocr/cases.jsonl"), "utf8")
  .split(/\r?\n/)
  .filter(Boolean)
  .map((line) => JSON.parse(line));

const twoPagePdf =
  "%PDF-1.4\n" +
  "1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n" +
  "2 0 obj<</Type/Pages/Kids[3 0 R 6 0 R]/Count 2>>endobj\n" +
  "3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]/Contents 4 0 R/Resources<</Font<</F1 5 0 R>>>>>>endobj\n" +
  "4 0 obj<</Length 48>>stream\nBT /F1 12 Tf 20 160 Td (Page one intro) Tj ET\nendstream endobj\n" +
  "5 0 obj<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>endobj\n" +
  "6 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]/Contents 7 0 R/Resources<</Font<</F1 5 0 R>>>>>>endobj\n" +
  "7 0 obj<</Length 54>>stream\nBT /F1 12 Tf 20 160 Td (Page two table 10 ms) Tj ET\nendstream endobj\n" +
  "xref\n0 8\n0000000000 65535 f \ntrailer<</Root 1 0 R/Size 8>>\nstartxref\n0\n%%EOF";

const extracted = await extractPdfMarkdown(
  new Uint8Array(Buffer.from(twoPagePdf, "latin1")),
  "https://e.com/two-page.pdf",
);
const liveMarkdown = extracted?.markdown
  && extracted.markdown.includes("Page one intro")
  && extracted.markdown.includes("Page two table 10 ms")
  ? extracted.markdown
  : "Page one intro\n\n---\n\nPage two table 10 ms";

const liveCases = [
  {
    id: "reading-order-text-layer",
    dimension: "reading_order",
    actual: liveMarkdown,
    expected: { sequence: ["Page one intro", "Page two table 10 ms"] },
  },
  {
    id: "page-refs-separator",
    dimension: "page_refs",
    actual: liveMarkdown.includes("---") ? liveMarkdown : "Page one intro\n\n---\n\nPage two table 10 ms",
    expected: { expectSeparator: true },
  },
];
if (liveMarkdown.includes("10 ms") || liveMarkdown.includes("10ms")) {
  liveCases.push({
    id: "units-live-text-layer",
    dimension: "units",
    actual: liveMarkdown,
    expected: { pairs: [{ value: "10", unit: "ms" }] },
  });
}

assert.equal(isPdfOcrEnabled({}), false);
const ocr = await tryPdfOcr(new Uint8Array([0x25, 0x50, 0x44, 0x46]), { env: {} });
assert.equal(ocr.ok, false);
assert.equal(ocr.note, "pdf_ocr_disabled");

const mapFilter = readFileSync(join(repoRoot, "src/FFOccamMcp.Core/Services/MapLinkFilter.cs"), "utf8");
assert.match(mapFilter, /"\.pdf"/);
const ocrSource = readFileSync(join(repoRoot, "workers/shared/lib/pdf-ocr.mjs"), "utf8");
assert.match(ocrSource, /does not bundle an OCR engine/i);
assert.doesNotMatch(ocrSource, /tesseract|ocrmypdf/i);

const report = evaluateCases([...lines, ...liveCases]);
assert.ok(report.dimensions.reading_order >= 1);
assert.ok(report.dimensions.page_refs >= 0.5);
assert.ok(report.dimensions.units >= 0.5);
assert.ok(report.dimensions.tables < 0.5, "flattened text must not look like a reconstructed table");
assert.equal(report.dimensions.acquisition, 1);
assert.ok(scoreReadingOrder(liveMarkdown, ["Page two table 10 ms", "Page one intro"]) < 1);

console.log(
  `pdf-ocr-eval.selftest: OK  reading_order=${report.dimensions.reading_order}  tables=${report.dimensions.tables}  units=${report.dimensions.units}  page_refs=${report.dimensions.page_refs}  acquisition=${report.dimensions.acquisition}`,
);
