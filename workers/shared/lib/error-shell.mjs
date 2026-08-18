/**
 * Tight browser/client render-error shell detector.
 * Whole-phrase / prefix-family only. Do not treat "try again", "something went wrong",
 * or a bare "err_" as standalone signals — those appear in legitimate troubleshooting prose.
 *
 * Tiny-extract band matches host EQM: totalChars < 500 && visibleProse < 280.
 * Markdown length alone is not enough — a 300–499 character article that quotes an
 * error phrase with substantial visible prose is not an error shell.
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

export function visibleContentChars(markdown) {
  let s = String(markdown ?? "");
  s = s.replace(/!\[[^\]]*\]\([^)]+\)/g, " ");
  s = s.replace(/\[([^\]]+)\]\([^)]+\)/g, "$1");
  s = s.replace(/^#{1,6}\s/gm, " ");
  s = s.replace(/^\s*[-*+]\s/gm, " ");
  s = s.replace(/\s+/g, " ");
  return s.trim().length;
}

export function looksLikeErrorShell(markdown) {
  if (typeof markdown !== "string") {
    return false;
  }
  const text = markdown.replace(APOSTROPHE, "'").trim();
  if (text.length === 0 || text.length >= 500) {
    return false;
  }
  if (visibleContentChars(text) >= 280) {
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
