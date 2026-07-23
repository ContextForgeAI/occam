import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { readOccamVersion } from "./onboard-schema.mjs";

/**
 * @returns {string}
 */
export function detectReleaseRid() {
  const override = process.env.OCCAM_RID?.trim();
  if (override) {
    return override;
  }

  if (process.platform === "win32") {
    return "win-x64";
  }

  if (process.platform === "darwin") {
    return process.arch === "arm64" ? "osx-arm64" : "osx-x64";
  }

  return "linux-x64";
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
 * @param {string} releasesApiUrl full URL to latest release JSON endpoint
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
    const response = await fetchFn(releasesApiUrl, {
      headers: { Accept: "application/json" },
      signal: AbortSignal.timeout(15_000),
    });

    if (!response.ok) {
      return { latest: null, error: `release API HTTP ${response.status}` };
    }

    const body = await response.json();
    const tag = typeof body.tag_name === "string" ? body.tag_name : null;
    if (!tag) {
      return { latest: null, error: "release API missing tag_name" };
    }

    return { latest: tag.replace(/^v/i, ""), error: null };
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
      `Level B: curl get-ff-occam.sh with OCCAM_VERSION=${latest}`,
      `Or: install.sh --from-url .../ff-occam-${latest}-${rid}.tar.gz`,
    ].join(" ");
  } else if (error) {
    upgradeHint = `Could not check remote release: ${error}. Set OCCAM_LATEST_VERSION to compare manually.`;
  } else {
    upgradeHint = `Installed v${installed} — up to date with channel latest.`;
  }

  return {
    installed,
    latest: effectiveLatest,
    rid,
    updateAvailable,
    upgradeHint,
    error,
  };
}
