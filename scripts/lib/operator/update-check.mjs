import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { readOccamVersion } from "./onboard-schema.mjs";
import { isPublishedReleaseRid, resolvePublishedRid } from "../resolve-rid.mjs";

/**
 * @returns {string}
 */
export function detectReleaseRid(
  platform = process.platform,
  arch = process.arch,
  override = process.env.OCCAM_RID?.trim(),
) {
  if (override) {
    if (!isPublishedReleaseRid(override)) {
      throw new Error(
        `unsupported OCCAM_RID: ${override} (published RIDs: win-x64, linux-x64, osx-arm64)`,
      );
    }
    return override;
  }
  return resolvePublishedRid(platform, arch);
}

/**
 * @param {string} occamHome
 */
export function readInstalledVersion(occamHome) {
  const versionPath = join(occamHome, "VERSION");
  if (existsSync(versionPath)) {
    const text = readFileSync(versionPath, "utf8").trim();
    if (text) {
      return text.replace(/^v/i, "");
    }
  }

  const fromChangelog = readOccamVersion(occamHome);
  if (fromChangelog !== "unknown") {
    return fromChangelog.replace(/^v/i, "");
  }

  return "unknown";
}

/**
 * @param {string} a
 * @param {string} b
 */
export function compareVersions(a, b) {
  const parse = (v) =>
    v
      .replace(/^v/i, "")
      .split(/[.-]/)
      .map((part) => Number.parseInt(part, 10))
      .map((n) => (Number.isFinite(n) ? n : 0));

  const av = parse(a);
  const bv = parse(b);
  const len = Math.max(av.length, bv.length);

  for (let i = 0; i < len; i += 1) {
    const diff = (av[i] ?? 0) - (bv[i] ?? 0);
    if (diff !== 0) {
      return diff > 0 ? 1 : -1;
    }
  }

  return 0;
}

/**
 * @param {string} releaseBase e.g. .../releases/download/v0.8.12
 */
export function releaseBaseToApiUrl(releaseBase) {
  const trimmed = releaseBase.replace(/\/$/, "");
  const match = /^(.*\/releases)\/download\/v[^/]+$/i.exec(trimmed);
  if (match) {
    return `${match[1]}`;
  }

  return trimmed;
}

/**
 * @param {typeof fetch} fetchFn
 * @param {string} releasesApiUrl full URL to releases list or …/releases/latest
 */
export async function fetchLatestReleaseTag(fetchFn, releasesApiUrl) {
  const allowHttp = process.env.OCCAM_RELEASE_ALLOW_HTTP === "1";
  if (releasesApiUrl.startsWith("http://") && !allowHttp) {
    return {
      latest: null,
      error: "HTTP release URL blocked — set OCCAM_RELEASE_ALLOW_HTTP=1 on trusted LAN",
    };
  }

  try {
    // Prefer a list endpoint so prereleases (rc.*) are visible. GitHub's
    // /releases/latest ignores prereleases and 404s when only RCs exist.
    let listUrl = releasesApiUrl;
    if (/\/releases\/latest\/?$/i.test(listUrl)) {
      listUrl = listUrl.replace(/\/latest\/?$/i, "") + "?per_page=15";
    } else if (/\/releases\/?$/i.test(listUrl) && !/[?&]per_page=/.test(listUrl)) {
      listUrl += (listUrl.includes("?") ? "&" : "?") + "per_page=15";
    }

    // Do not use AbortSignal here: on Windows Node 24, abort timers + process.exit
    // can trip libuv UV_HANDLE_CLOSING assertions after a successful fetch.
    const response = await fetchFn(listUrl, {
      headers: { Accept: "application/vnd.github+json" },
    });

    if (!response.ok) {
      return { latest: null, error: `release API HTTP ${response.status}` };
    }

    const body = await response.json();
    /** @type {unknown[]} */
    const items = Array.isArray(body) ? body : body && typeof body === "object" ? [body] : [];
    for (const item of items) {
      if (!item || typeof item !== "object") continue;
      const row = /** @type {{ draft?: boolean, tag_name?: string }} */ (item);
      if (row.draft) continue;
      if (typeof row.tag_name === "string" && row.tag_name.trim()) {
        return { latest: row.tag_name.replace(/^v/i, ""), error: null };
      }
    }

    return { latest: null, error: "release API missing tag_name" };
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    return { latest: null, error: message };
  }
}

/**
 * @returns {string}
 */
export function defaultReleasesApiUrl() {
  const explicit = process.env.OCCAM_RELEASES_API_URL?.trim();
  if (explicit) {
    return explicit;
  }

  return "https://api.github.com/repos/ContextForgeAI/occam/releases/latest";
}

/**
 * @param {{ occamHome: string, fetch?: typeof fetch }} [opts]
 */
export async function checkForUpdate(opts) {
  const occamHome = opts.occamHome;
  const installed = readInstalledVersion(occamHome);
  const rid = detectReleaseRid();

  const explicitLatest = process.env.OCCAM_LATEST_VERSION?.trim()?.replace(/^v/i, "");
  let latest = explicitLatest ?? null;
  /** @type {string | null} */
  let error = null;

  if (!latest) {
    const apiUrl = defaultReleasesApiUrl();
    const fetchFn = opts?.fetch ?? globalThis.fetch;

    if (typeof fetchFn === "function") {
      const remote = await fetchLatestReleaseTag(fetchFn, apiUrl);
      latest = remote.latest;
      error = remote.error;
    } else {
      error = "fetch unavailable — set OCCAM_LATEST_VERSION";
    }
  }

  const effectiveLatest = latest ?? installed;
  const updateAvailable =
    installed !== "unknown" &&
    latest !== null &&
    compareVersions(latest, installed) > 0;

  /** @type {string} */
  let upgradeHint;
  if (updateAvailable) {
    upgradeHint = [
      `Newer release v${latest} available (installed v${installed}).`,
      `Re-run the one-line installer to update, or set OCCAM_VERSION=${latest}.`,
    ].join(" ");
  } else if (error) {
    upgradeHint = `Could not check for updates: ${error}. Set OCCAM_LATEST_VERSION to compare manually.`;
  } else {
    upgradeHint = `Installed v${installed} — up to date.`;
  }

  return {
    installed,
    latest: latest ?? (error ? null : effectiveLatest),
    rid,
    updateAvailable,
    upgradeHint,
    error,
  };
}
