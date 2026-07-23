import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { homedir } from "node:os";
import { basename, dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));

/** @returns {string} */
export function resolveSessionsRoot() {
  const env = process.env.OCCAM_SESSIONS_ROOT?.trim();
  if (env) {
    return resolve(env);
  }
  return join(homedir(), ".occam", "sessions");
}

/** @param {string} id */
export function isValidSessionId(id) {
  if (!id || id.includes("..") || id.includes("/") || id.includes("\\")) {
    return false;
  }
  return /^[a-zA-Z0-9._-]+$/.test(id);
}

/**
 * @param {string} filePath
 * @param {string | null | undefined} hostFilter e.g. stackoverflow.com; omit or --all for every domain
 */
export function parseNetscapeCookies(filePath, hostFilter) {
  const raw = readFileSync(filePath, "utf8");
  const now = Math.floor(Date.now() / 1000);
  const parts = [];
  const hosts = new Set();
  let skippedExpired = 0;

  for (const line of raw.split(/\r?\n/)) {
    if (!line || line.startsWith("#")) {
      continue;
    }
    const cols = line.split("\t");
    if (cols.length < 7) {
      continue;
    }
    const [domain, , , , expiry, name, ...rest] = cols;
    const value = rest.join("\t");
    const dom = (domain ?? "").toLowerCase().replace(/^\./, "");
    if (hostFilter) {
      const host = hostFilter.toLowerCase().replace(/^\./, "");
      if (!dom.includes(host) && !host.includes(dom)) {
        continue;
      }
    }
    const exp = parseInt(expiry, 10);
    if (exp > 0 && exp < now) {
      skippedExpired += 1;
      continue;
    }
    if (dom) {
      hosts.add(dom);
    }
    parts.push(`${name}=${value}`);
  }

  const cookie = parts.join("; ");
  return {
    cookie,
    count: parts.length,
    skippedExpired,
    hosts: [...hosts].sort(),
    cookieBytes: Buffer.byteLength(cookie, "utf8"),
  };
}

/** @param {string} sessionsRoot */
export function listSessionProfiles(sessionsRoot) {
  if (!existsSync(sessionsRoot)) {
    return [];
  }
  return readdirSync(sessionsRoot, { withFileTypes: true })
    .filter((e) => e.isFile() && e.name.endsWith(".json") && !e.name.startsWith("."))
    .map((e) => {
      const id = e.name.replace(/\.json$/i, "");
      const path = join(sessionsRoot, e.name);
      let meta = null;
      let headerKeys = [];
      try {
        const parsed = JSON.parse(readFileSync(path, "utf8"));
        if (parsed?._occam && typeof parsed._occam === "object") {
          meta = parsed._occam;
        }
        headerKeys = Object.keys(parsed).filter(
          (k) => k !== "_occam" && typeof parsed[k] === "string",
        );
      } catch {
        // ignore
      }
      return { id, path, meta, headerKeys };
    })
    .sort((a, b) => a.id.localeCompare(b.id));
}

/** @param {string} sessionsRoot */
export function ensureSessionsLayout(sessionsRoot) {
  mkdirSync(sessionsRoot, { recursive: true });
  mkdirSync(join(sessionsRoot, "_imports"), { recursive: true });
  mkdirSync(join(sessionsRoot, "states"), { recursive: true });

  const readmePath = join(sessionsRoot, "README.md");
  if (!existsSync(readmePath)) {
    const template = join(__dirname, "..", "templates", "occam-sessions-README.md");
    writeFileSync(readmePath, readFileSync(template, "utf8"), "utf8");
  }

  const gitignorePath = join(sessionsRoot, ".gitignore");
  if (!existsSync(gitignorePath)) {
    writeFileSync(
      gitignorePath,
      "# Occam session profiles — secrets stay local\n*\n!README.md\n!.gitignore\n",
      "utf8",
    );
  }
}

/**
 * @param {{
 *   sessionsRoot: string,
 *   id: string,
 *   headers: Record<string, string>,
 *   meta?: Record<string, unknown>,
 *   storageState?: string,
 *   force?: boolean,
 * }} options
 */
export function writeSessionProfile(options) {
  const { sessionsRoot, id, headers, meta, storageState, force } = options;
  if (!isValidSessionId(id)) {
    throw new Error(`Invalid session id "${id}" — use [a-zA-Z0-9._-] only (no / or ..).`);
  }
  const outPath = join(sessionsRoot, `${id}.json`);
  if (existsSync(outPath) && !force) {
    throw new Error(`Profile already exists: ${outPath} (pass --force to overwrite)`);
  }
  /** @type {Record<string, unknown>} */
  const body = meta ? { _occam: meta, ...headers } : { ...headers };
  if (storageState) {
    body.storageState = storageState;
  }
  writeFileSync(outPath, `${JSON.stringify(body, null, 2)}\n`, "utf8");
  return outPath;
}

/** Suggest id from host + purpose */
export function suggestId(host, purpose = "default") {
  const site = host.toLowerCase().replace(/^www\./, "");
  const safePurpose = purpose.replace(/[^a-zA-Z0-9._-]/g, "-");
  return `${site}.${safePurpose}`;
}

export function templateImportsPath(sessionsRoot, sourceFile) {
  const name = basename(sourceFile);
  const dest = join(sessionsRoot, "_imports", name);
  return dest;
}
