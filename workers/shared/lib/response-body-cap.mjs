const MIN_BYTES = 64 * 1024;
const MAX_BYTES = 16 * 1024 * 1024;
// Q-012: the original 1 MiB default hard-failed (response_too_large) on common heavy-HTML
// pages before extraction was even attempted — the 1k sweep flagged 44 such pages that
// Firecrawl extracted fine (cnn, theguardian, bloomberg, cloudflare, gmail). Modern pages
// routinely exceed 1 MiB of raw HTML while their extracted content is small, so 1 MiB was
// just too low. 8 MiB covers the long tail while staying under the 16 MiB hard ceiling;
// genuinely oversize pages still fail honestly (no partial-cited-as-full). Override via env.
const DEFAULT_BYTES = 8 * 1024 * 1024;
const ENV_KEY = "OCCAM_MAX_RESPONSE_BYTES";
const MODE_KEY = "OCCAM_HTTP_OVERSIZE_MODE";

export class ResponseTooLargeError extends Error {
  /**
   * @param {number} bytesRead
   * @param {number} maxBytes
   */
  constructor(bytesRead, maxBytes) {
    super("response_too_large");
    this.name = "ResponseTooLargeError";
    this.code = "response_too_large";
    this.bytesRead = bytesRead;
    this.maxBytes = maxBytes;
  }
}

/** @returns {number} */
export function resolveMaxResponseBytes() {
  const raw = process.env[ENV_KEY]?.trim();
  if (!raw) {
    return DEFAULT_BYTES;
  }

  const parsed = Number.parseInt(raw, 10);
  if (!Number.isFinite(parsed) || parsed <= 0) {
    throw new Error(`invalid ${ENV_KEY}`);
  }

  return Math.min(MAX_BYTES, Math.max(MIN_BYTES, parsed));
}

/** @returns {"fail" | "partial"} */
export function resolveHttpOversizeMode() {
  const raw = process.env[MODE_KEY]?.trim()?.toLowerCase();
  return raw === "partial" ? "partial" : "fail";
}

/**
 * Fast-fail when Content-Length header exceeds cap (no body read).
 * @param {Response} response
 * @param {number} maxBytes
 */
export function rejectIfContentLengthExceeds(response, maxBytes) {
  const raw = response.headers?.get("content-length")?.trim();
  if (!raw) {
    return;
  }

  const contentLength = Number.parseInt(raw, 10);
  if (!Number.isFinite(contentLength) || contentLength < 0) {
    return;
  }

  if (contentLength > maxBytes) {
    throw new ResponseTooLargeError(contentLength, maxBytes);
  }
}

/**
 * @param {Buffer} buffer
 * @param {number} maxBytes
 */
export function snapHtmlToSafeBoundary(buffer, maxBytes) {
  const capped = buffer.subarray(0, Math.min(buffer.length, maxBytes));
  const text = capped.toString("utf8");
  const lastGt = text.lastIndexOf(">");
  if (lastGt >= 0) {
    return text.slice(0, lastGt + 1);
  }

  return text;
}

/**
 * @typedef {{ html: string, truncated: boolean, bytesRead: number, maxBytes: number }} ResponseBodyReadResult
 */

/**
 * @param {Response} response
 * @param {number} maxBytes
 * @returns {Promise<ResponseBodyReadResult>}
 */
export async function readResponseBodyForExtract(response, maxBytes) {
  const mode = resolveHttpOversizeMode();
  if (mode === "partial") {
    return readResponseBodyPartial(response, maxBytes);
  }

  const html = await readResponseBodyWithCap(response, maxBytes);
  return {
    html,
    truncated: false,
    bytesRead: Buffer.byteLength(html, "utf8"),
    maxBytes,
  };
}

/**
 * @param {Response} response
 * @param {number} maxBytes
 * @returns {Promise<string>}
 */
export async function readResponseBodyWithCap(response, maxBytes) {
  rejectIfContentLengthExceeds(response, maxBytes);

  const body = response.body;
  if (!body || typeof body.getReader !== "function") {
    const text = await response.text();
    const byteLength = Buffer.byteLength(text, "utf8");
    if (byteLength > maxBytes) {
      throw new ResponseTooLargeError(byteLength, maxBytes);
    }

    return text;
  }

  const reader = body.getReader();
  /** @type {Uint8Array[]} */
  const chunks = [];
  let total = 0;

  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) {
        break;
      }

      if (!value) {
        continue;
      }

      total += value.byteLength;
      if (total > maxBytes) {
        throw new ResponseTooLargeError(total, maxBytes);
      }

      chunks.push(value);
    }
  } finally {
    try {
      await reader.cancel();
    } catch {
      // best effort
    }
  }

  return Buffer.concat(chunks).toString("utf8");
}

/**
 * @param {Response} response
 * @param {number} maxBytes
 * @returns {Promise<ResponseBodyReadResult>}
 */
async function readResponseBodyPartial(response, maxBytes) {
  const body = response.body;
  if (!body || typeof body.getReader !== "function") {
    const text = await response.text();
    const byteLength = Buffer.byteLength(text, "utf8");
    if (byteLength <= maxBytes) {
      return { html: text, truncated: false, bytesRead: byteLength, maxBytes };
    }

    const html = snapHtmlToSafeBoundary(Buffer.from(text, "utf8"), maxBytes);
    return { html, truncated: true, bytesRead: byteLength, maxBytes };
  }

  const reader = body.getReader();
  /** @type {Uint8Array[]} */
  const chunks = [];
  let total = 0;
  let truncated = false;

  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) {
        break;
      }

      if (!value) {
        continue;
      }

      if (total + value.byteLength > maxBytes) {
        const remaining = maxBytes - total;
        if (remaining > 0) {
          chunks.push(value.subarray(0, remaining));
          total += remaining;
        }

        truncated = true;
        break;
      }

      total += value.byteLength;
      chunks.push(value);
    }
  } finally {
    try {
      await reader.cancel();
    } catch {
      // best effort
    }
  }

  const buffer = Buffer.concat(chunks);
  const html = truncated ? snapHtmlToSafeBoundary(buffer, maxBytes) : buffer.toString("utf8");
  return {
    html,
    truncated,
    bytesRead: truncated ? Math.max(total, Buffer.byteLength(html, "utf8")) : total,
    maxBytes,
  };
}
