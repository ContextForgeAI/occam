/**
 * Tight browser/client render-error shell detector.
 * Whole-phrase / prefix-family only. Do not treat "try again", "something went wrong",
 * or a bare "err_" as standalone signals — those appear in legitimate troubleshooting prose.
 *
 * Tiny-extract band (worker): markdown length < 500. Host EQM applies the same phrases
 * plus the existing short_quality char band.
 */

const APOSTROPHE = /[\u2018\u2019]/g;

const PHRASES = [
  "this page couldn't load",
  "this page could not load",
  "this page isn't working",
  "this page is not working",
  "reload to try again",
  "aw snap",
];

const ERR_INTERNET = /\berr_internet_disconnected\b/i;
const ERR_NAME = /\berr_name_not_resolved\b/i;
const ERR_CONNECTION = /\berr_connection_[a-z0-9_]+\b/i;

export function looksLikeErrorShell(markdown) {
  if (typeof markdown !== "string") {
    return false;
  }
  const text = markdown.replace(APOSTROPHE, "'").trim();
  if (text.length === 0 || text.length >= 500) {
    return false;
  }
  return hasTightErrorShellSignal(text);
}

export function hasTightErrorShellSignal(text) {
  const haystack = String(text ?? "").replace(APOSTROPHE, "'");
  const lower = haystack.toLowerCase();
  for (const phrase of PHRASES) {
    if (lower.includes(phrase)) {
      return true;
    }
  }
  return ERR_INTERNET.test(haystack) || ERR_NAME.test(haystack) || ERR_CONNECTION.test(haystack);
}

export function applyErrorShellAccess(access, markdown) {
  if (!looksLikeErrorShell(markdown) || access == null || typeof access !== "object") {
    return access;
  }
  return {
    ...access,
    has_usable_content: false,
    error_shell: true,
  };
}
