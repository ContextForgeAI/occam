import assert from "node:assert/strict";
import { writeFile, mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { isPdfOcrEnabled, normalizeOcrText, tryPdfOcr } from "./pdf-ocr.mjs";

assert.equal(isPdfOcrEnabled({}), false);
assert.equal(isPdfOcrEnabled({ OCCAM_PDF_OCR: "1" }), true);
assert.equal(normalizeOcrText("  a \n\n\n b  "), "a\n\nb");

assert.deepEqual(
  await tryPdfOcr(new Uint8Array([1, 2, 3]), { env: {} }),
  { ok: false, note: "pdf_ocr_disabled" },
);

assert.deepEqual(
  await tryPdfOcr(new Uint8Array([1, 2, 3]), { env: { OCCAM_PDF_OCR: "1" } }),
  { ok: false, note: "pdf_ocr_unconfigured" },
);

const dir = await mkdtemp(join(tmpdir(), "occam-pdf-ocr-st-"));
const helper = join(dir, "ocr-helper.mjs");
try {
  await writeFile(
    helper,
    "process.stdout.write('OCR_OK_FROM_HELPER\\n');\n",
  );

  const ok = await tryPdfOcr(new Uint8Array(Buffer.from("%PDF-1.4\n")), {
    env: {
      OCCAM_PDF_OCR: "1",
      OCCAM_PDF_OCR_BIN: process.execPath,
      OCCAM_PDF_OCR_ARGS: helper,
      OCCAM_PDF_OCR_TIMEOUT_MS: "10000",
    },
  });
  assert.equal(ok.ok, true);
  assert.match(ok.markdown, /OCR_OK_FROM_HELPER/);
} finally {
  await rm(dir, { recursive: true, force: true });
}

console.log("pdf-ocr.selftest OK");
