/**
 * Opt-in OCR for scanned / image-only PDFs after the text layer is empty.
 *
 * Off by default. Requires OCCAM_PDF_OCR=1 and OCCAM_PDF_OCR_BIN pointing at a local
 * helper that accepts the PDF path as argv[1] and prints plain text / markdown to stdout.
 * Occam does not bundle an OCR engine.
 */

import { spawn } from "node:child_process";
import { mkdtemp, writeFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";

/**
 * @returns {boolean}
 */
export function isPdfOcrEnabled(env = process.env) {
  const v = String(env.OCCAM_PDF_OCR ?? "").trim().toLowerCase();
  return v === "1" || v === "true" || v === "yes";
}

/**
 * @param {Uint8Array} bytes
 * @param {{ timeoutMs?: number, env?: NodeJS.ProcessEnv }} [opts]
 * @returns {Promise<{ ok: true, markdown: string } | { ok: false, note: string }>}
 */
export async function tryPdfOcr(bytes, opts = {}) {
  const env = opts.env ?? process.env;
  if (!isPdfOcrEnabled(env)) {
    return { ok: false, note: "pdf_ocr_disabled" };
  }

  const bin = String(env.OCCAM_PDF_OCR_BIN ?? "").trim();
  if (!bin) {
    return { ok: false, note: "pdf_ocr_unconfigured" };
  }

  const timeoutMs = Math.min(
    Math.max(Number(env.OCCAM_PDF_OCR_TIMEOUT_MS) || opts.timeoutMs || 60_000, 1_000),
    300_000,
  );

  const extraArgs = String(env.OCCAM_PDF_OCR_ARGS ?? "")
    .trim()
    .split(/\s+/)
    .filter(Boolean);

  let dir;
  try {
    dir = await mkdtemp(join(tmpdir(), "occam-pdf-ocr-"));
    const pdfPath = join(dir, "input.pdf");
    await writeFile(pdfPath, bytes);

    const { exitCode, stdout } = await runCapture(bin, [...extraArgs, pdfPath], timeoutMs, env);
    if (exitCode === -2) {
      return { ok: false, note: "pdf_ocr_timeout" };
    }
    if (exitCode !== 0) {
      return { ok: false, note: "pdf_ocr_failed" };
    }

    const markdown = normalizeOcrText(stdout);
    if (!markdown) {
      return { ok: false, note: "pdf_ocr_empty" };
    }

    return { ok: true, markdown };
  } catch {
    return { ok: false, note: "pdf_ocr_failed" };
  } finally {
    if (dir) {
      try {
        await rm(dir, { recursive: true, force: true });
      } catch {
        /* ignore */
      }
    }
  }
}

/**
 * @param {string} bin
 * @param {string[]} args
 * @param {number} timeoutMs
 * @param {NodeJS.ProcessEnv} env
 */
function runCapture(bin, args, timeoutMs, env) {
  return new Promise((resolve) => {
    let settled = false;
    const child = spawn(bin, args, {
      env,
      windowsHide: true,
      stdio: ["ignore", "pipe", "pipe"],
    });
    let stdout = "";
    let stderr = "";
    child.stdout.setEncoding("utf8");
    child.stderr.setEncoding("utf8");
    child.stdout.on("data", (chunk) => {
      stdout += chunk;
    });
    child.stderr.on("data", (chunk) => {
      stderr += chunk;
    });

    const timer = setTimeout(() => {
      if (settled) return;
      settled = true;
      try {
        child.kill();
      } catch {
        /* ignore */
      }
      resolve({ exitCode: -2, stdout, stderr });
    }, timeoutMs);

    child.on("error", () => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      resolve({ exitCode: -1, stdout, stderr });
    });

    child.on("close", (code) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      resolve({ exitCode: code ?? -1, stdout, stderr });
    });
  });
}

/** @param {string} value */
export function normalizeOcrText(value) {
  return String(value ?? "")
    .replace(/\r\n/g, "\n")
    .replace(/[ \t\u00a0]+/g, " ")
    .replace(/ *\n */g, "\n")
    .replace(/\n{3,}/g, "\n\n")
    .trim();
}
