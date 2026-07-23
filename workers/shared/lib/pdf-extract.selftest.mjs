import assert from "node:assert/strict";
import {
  isPdfContentType,
  looksPdfUrl,
  shouldTryPdf,
  hasPdfMagic,
  extractPdfMarkdown,
} from "./pdf-extract.mjs";

// --- detection helpers ---------------------------------------------------------------------------
assert.equal(isPdfContentType("application/pdf"), true);
assert.equal(isPdfContentType("application/pdf; charset=binary"), true);
assert.equal(isPdfContentType("text/html"), false);

assert.equal(looksPdfUrl("https://e.com/paper.pdf"), true);
assert.equal(looksPdfUrl("https://e.com/paper.pdf?dl=1"), true);
assert.equal(looksPdfUrl("https://e.com/page"), false);

// Commit to the PDF path on a pdf content-type, or a .pdf URL that is not clearly HTML.
assert.equal(shouldTryPdf("application/pdf", "https://e.com/x"), true);
assert.equal(shouldTryPdf("application/octet-stream", "https://e.com/x.pdf"), true);
assert.equal(shouldTryPdf("text/html", "https://e.com/x.pdf"), false, "HTML at a .pdf URL must stay on the normal path");
assert.equal(shouldTryPdf("text/html", "https://e.com/page"), false);

assert.equal(hasPdfMagic(new Uint8Array(Buffer.from("%PDF-1.7\n...", "latin1"))), true);
assert.equal(hasPdfMagic(new Uint8Array(Buffer.from("<!doctype html>", "latin1"))), false);

// --- end-to-end text extraction on a hand-built single-page PDF ----------------------------------
const samplePdf =
  "%PDF-1.4\n" +
  "1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n" +
  "2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n" +
  "3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]/Contents 4 0 R/Resources<</Font<</F1 5 0 R>>>>>>endobj\n" +
  "4 0 obj<</Length 44>>stream\nBT /F1 24 Tf 20 100 Td (Hello PDF) Tj ET\nendstream endobj\n" +
  "5 0 obj<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>endobj\n" +
  "xref\n0 6\n0000000000 65535 f \ntrailer<</Root 1 0 R/Size 6>>\nstartxref\n0\n%%EOF";
const bytes = new Uint8Array(Buffer.from(samplePdf, "latin1"));

const result = await extractPdfMarkdown(bytes, "https://e.com/sample.pdf");
assert.ok(result, "expected a parse result");
assert.equal(result.pages, 1);
assert.ok(result.markdown.includes("Hello PDF"), `markdown should contain extracted text, got: ${result.markdown}`);

// Non-PDF bytes → null (caller treats as not-a-pdf).
assert.equal(await extractPdfMarkdown(new Uint8Array(Buffer.from("not a pdf", "latin1")), "https://e.com/x"), null);

console.log("pdf-extract.selftest OK");
