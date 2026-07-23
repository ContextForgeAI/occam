/**
 * P2-11a — HTTP plain-text pass-through (raw README, text/* URLs).
 */

export function isPlainTextContentType(contentType) {
  const ct = (contentType ?? "").toLowerCase().split(";")[0].trim();
  return ct === "text/plain" || ct === "application/octet-stream";
}

export function isUtf8TextBody(body) {
  if (typeof body !== "string" || body.length === 0) {
    return false;
  }
  if (body.includes("\0")) {
    return false;
  }
  const sample = body.length > 4096 ? body.slice(0, 4096) : body;
  let nonPrintable = 0;
  for (let i = 0; i < sample.length; i += 1) {
    const code = sample.charCodeAt(i);
    if (code === 9 || code === 10 || code === 13) {
      continue;
    }
    if (code < 32 || code === 127) {
      nonPrintable += 1;
    }
  }
  return nonPrintable / sample.length < 0.02;
}

export function looksLikeMarkdown(text) {
  const trimmed = text.trim();
  if (trimmed.length === 0) {
    return false;
  }
  return /^#\s/m.test(trimmed)
    || /^##\s/m.test(trimmed)
    || trimmed.includes("```")
    || /^[-*]\s/m.test(trimmed);
}

export function plainTextToMarkdown(body, url = "") {
  const text = body.replace(/^\uFEFF/, "").trim();
  if (text.length === 0) {
    return "";
  }
  let pathname = "";
  try {
    pathname = new URL(url).pathname.toLowerCase();
  } catch (err) {
    const code = err && typeof err === "object" && "code" in err ? String(err.code) : "invalid_url";
    console.error(`[occam.worker] plain_text_url_parse_failed code=${code}`);
    pathname = "";
  }
  if (pathname.endsWith(".md") || pathname.endsWith(".markdown") || looksLikeMarkdown(text)) {
    return text;
  }
  return `\`\`\`\n${text}\n\`\`\``;
}

export function shouldPassThroughPlainText(contentType, body) {
  if (!isPlainTextContentType(contentType)) {
    return false;
  }
  return isUtf8TextBody(body);
}
