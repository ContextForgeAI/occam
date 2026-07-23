import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";

/** Keep in sync with profiles/occam-fetch-defaults.json */
export const FALLBACK_USER_AGENT =
  "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

export const FALLBACK_ACCEPT = "text/html,application/xhtml+xml";

/** @type {{ userAgent: string, accept: string } | null} */
let cached = null;

function resolveOccamHome() {
  const env = process.env.OCCAM_HOME?.trim();
  if (env && existsSync(env)) {
    return env;
  }
  return null;
}

/** @returns {{ userAgent: string, accept: string }} */
export function getDefaultFetchHeaders() {
  if (cached) {
    return cached;
  }

  const home = resolveOccamHome();
  const path = home ? join(home, "profiles", "occam-fetch-defaults.json") : null;
  if (path && existsSync(path)) {
    try {
      const parsed = JSON.parse(readFileSync(path, "utf8"));
      cached = {
        userAgent: String(parsed.userAgent ?? FALLBACK_USER_AGENT),
        accept: String(parsed.accept ?? FALLBACK_ACCEPT),
      };
      return cached;
    } catch {
      // fall through
    }
  }

  cached = { userAgent: FALLBACK_USER_AGENT, accept: FALLBACK_ACCEPT };
  return cached;
}

export function getDefaultUserAgent() {
  return getDefaultFetchHeaders().userAgent;
}
