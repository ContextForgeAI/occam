import {
  resolveAndValidateHost,
  SsrfBlockedError,
} from "../../../workers/shared/lib/private-ip.mjs";

export class DemoUrlError extends Error {
  constructor(code, message) {
    super(message);
    this.name = "DemoUrlError";
    this.code = code;
  }
}

/** @param {unknown} input @param {{ resolve?: typeof resolveAndValidateHost }} [options] */
export async function validateDemoUrl(input, options = {}) {
  if (typeof input !== "string" || input.length === 0 || input.length > 2048) {
    throw new DemoUrlError("invalid_url", "url must be a non-empty string of at most 2048 characters");
  }
  let parsed;
  try {
    parsed = new URL(input);
  } catch {
    throw new DemoUrlError("invalid_url", "url must be an absolute HTTP or HTTPS URL");
  }
  if (parsed.protocol !== "http:" && parsed.protocol !== "https:") {
    throw new DemoUrlError("invalid_url", "only HTTP and HTTPS URLs are allowed");
  }
  if (parsed.username || parsed.password) {
    throw new DemoUrlError("url_credentials_blocked", "URL credentials are not allowed");
  }
  const expectedPort = parsed.protocol === "http:" ? "80" : "443";
  if (parsed.port && parsed.port !== expectedPort) {
    throw new DemoUrlError("port_blocked", "the public demo allows only ports 80 and 443");
  }
  parsed.hash = "";
  const resolve = options.resolve ?? resolveAndValidateHost;
  try {
    await resolve(parsed.hostname.replace(/^\[|\]$/g, ""), { allowPrivate: false });
  } catch (error) {
    if (error instanceof SsrfBlockedError) {
      const code = error.failure === "dns_resolution_failed" ? "dns_error" : "private_url_blocked";
      throw new DemoUrlError(code, code === "dns_error"
        ? "the hostname could not be resolved"
        : "private, local, and reserved destinations are blocked");
    }
    throw error;
  }
  return parsed.toString();
}
