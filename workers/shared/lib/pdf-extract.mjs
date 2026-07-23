/**
 * PDF text extraction for the HTTP worker (closes the unsupported_content_type gap on PDFs).
 *
 * Uses `unpdf` (a serverless pdfjs build — text extraction only, no `canvas`/native deps needed).
 * Returns LLM-ready markdown: a per-page text join with `---` separators, plus an optional title
 * heading from PDF metadata. Binary in, markdown out; the caller handles fetch + size cap.
 */

const PDF_MAGIC = "%PDF-";

/** @param {string | null | undefined} contentType */
export function isPdfContentType(contentType) {
  const ct = (contentType ?? "").toLowerCase();
  return ct.includes("application/pdf") || ct.includes("application/x-pdf");
}

/** @param {string | null | undefined} url */
export function looksPdfUrl(url) {
  if (!url) {
    return false;
  }
  try {
    const path = new URL(url).pathname.toLowerCase();
    return path.endsWith(".pdf");
  } catch {
    return false;
  }
}

/**
 * Decides whether to commit to the binary PDF path. True when the content-type is PDF
 * (authoritative), or the URL ends `.pdf` AND the content-type is not clearly HTML (covers servers
 * that mislabel PDFs as octet-stream). Reading the body as binary is one-way, so this must be
 * confident — an HTML page served at a `.pdf` URL stays on the normal extraction path.
 * @param {string | null | undefined} contentType
 * @param {string | null | undefined} url
 */
export function shouldTryPdf(contentType, url) {
  if (isPdfContentType(contentType)) {
    return true;
  }
  const ct = (contentType ?? "").toLowerCase();
  const htmlish = ct.includes("text/html") || ct.includes("application/xhtml");
  return looksPdfUrl(url) && !htmlish;
}

/** @param {Uint8Array} bytes — true when the buffer starts with the %PDF- magic (within first 1KB). */
export function hasPdfMagic(bytes) {
  if (!bytes || bytes.length < PDF_MAGIC.length) {
    return false;
  }
  const head = Buffer.from(bytes.subarray(0, Math.min(1024, bytes.length))).toString("latin1");
  return head.includes(PDF_MAGIC);
}

/**
 * Extracts markdown from a PDF byte buffer.
 * @param {Uint8Array} bytes
 * @param {string} url
 * @returns {Promise<{ markdown: string, title: string, pages: number } | null>}
 */
export async function extractPdfMarkdown(bytes, url) {
  if (!hasPdfMagic(bytes)) {
    return null;
  }

  // Imported lazily so the PDF dependency is only loaded when a PDF is actually encountered.
  const { getDocumentProxy, extractText, getMeta } = await import("unpdf");
  const doc = await getDocumentProxy(bytes);

  let title = "";
  try {
    const meta = await getMeta(doc);
    const t = meta?.info?.Title;
    if (typeof t === "string") {
      title = t.trim();
    }
  } catch {
    // Metadata is best-effort; absence is fine.
  }

  const { text, totalPages } = await extractText(doc, { mergePages: false });
  const pages = Array.isArray(text) ? text : [String(text ?? "")];

  const body = pages
    .map((p) => normalizeWhitespace(p))
    .filter((p) => p.length > 0)
    .join("\n\n---\n\n");

  if (body.length === 0) {
    // A PDF with no extractable text (scanned/image-only) — honest empty so the caller can fail.
    return { markdown: "", title, pages: totalPages ?? pages.length };
  }

  const markdown = title.length > 0 ? `# ${title}\n\n${body}` : body;
  return { markdown, title, pages: totalPages ?? pages.length };
}

function normalizeWhitespace(value) {
  return (value ?? "")
    .replace(/[ \t ]+/g, " ")
    .replace(/\s*\n\s*/g, "\n")
    .replace(/\n{3,}/g, "\n\n")
    .trim();
}
