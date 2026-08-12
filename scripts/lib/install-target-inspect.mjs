#!/usr/bin/env node
/**
 * Shared Level-B install-target inspection for uninstall and owned replace.
 * Kept free of heavy operator-CLI imports so bootstrap overlay downloads stay small.
 */
import {
  existsSync,
  lstatSync,
  readFileSync,
  realpathSync,
} from "node:fs";
import { homedir, platform } from "node:os";
import {
  isAbsolute,
  join,
  parse as parsePath,
  relative,
  resolve,
  posix as pathPosix,
  win32 as pathWin32,
} from "node:path";
import { resolveRid } from "./resolve-rid.mjs";

/** @param {string} path */
export function isRegularFile(path) {
  try {
    const st = lstatSync(path);
    return st.isFile() && !st.isSymbolicLink();
  } catch {
    return false;
  }
}

/** @param {string} value @param {string} [platformName] */
export function pathKey(value, platformName = platform()) {
  const normalized = (platformName === "win32"
    ? pathWin32.resolve(value)
    : pathPosix.resolve(String(value).replace(/\\/g, "/")))
    .replace(/\\/g, "/")
    .replace(/\/$/, "");
  return platformName === "win32" ? normalized.toLowerCase() : normalized;
}

/** @param {string} parent @param {string} child */
export function isSameOrInside(parent, child) {
  const rel = relative(resolve(parent), resolve(child));
  return rel === "" || (!rel.startsWith("..") && !isAbsolute(rel));
}

/**
 * Resolve a destructive target without silently accepting a relative or broad path.
 * @param {string} rawPath
 * @param {{ homeDir?: string, label?: string }} [opts]
 */
export function validateScopedPath(rawPath, opts = {}) {
  const label = opts.label || "target";
  const raw = String(rawPath || "").trim();
  if (!raw) {
    return { ok: false, path: "", reason: `${label} path is empty` };
  }
  if (!isAbsolute(raw)) {
    return { ok: false, path: raw, reason: `${label} path must be absolute` };
  }

  const absolute = resolve(raw);
  if (pathKey(absolute) === pathKey(parsePath(absolute).root)) {
    return { ok: false, path: absolute, reason: `${label} cannot be a filesystem root` };
  }

  const home = resolve(opts.homeDir || homedir());
  if (pathKey(absolute) === pathKey(home)) {
    return { ok: false, path: absolute, reason: `${label} cannot be the user home directory` };
  }

  return { ok: true, path: absolute, reason: "" };
}

/**
 * @param {string} occamHome
 * @param {{ homeDir?: string, rid?: string }} [opts]
 */
export function inspectInstallTarget(occamHome, opts = {}) {
  const scoped = validateScopedPath(occamHome, {
    homeDir: opts.homeDir,
    label: "OCCAM_HOME",
  });
  if (!scoped.ok) {
    return { kind: "install", action: "refuse", ...scoped };
  }

  const target = scoped.path;
  let stat;
  try {
    stat = lstatSync(target);
  } catch (err) {
    if (err && typeof err === "object" && err.code === "ENOENT") {
      return {
        kind: "install",
        action: "absent",
        ok: true,
        path: target,
        reason: "install tree is already absent",
      };
    }
    return {
      kind: "install",
      action: "refuse",
      ok: false,
      path: target,
      reason: `cannot inspect install tree: ${err instanceof Error ? err.message : String(err)}`,
    };
  }
  if (!stat.isDirectory()) {
    return {
      kind: "install",
      action: "refuse",
      ok: false,
      path: target,
      reason: "OCCAM_HOME is not a directory",
    };
  }
  if (stat.isSymbolicLink()) {
    return {
      kind: "install",
      action: "refuse",
      ok: false,
      path: target,
      reason: "symlinked install roots are not removed automatically",
    };
  }

  try {
    // Refuse Windows junctions / reparse roots where the leaf resolves elsewhere.
    // Do NOT refuse macOS/Linux ancestor convenience symlinks (/var → /private/var,
    // /tmp → /private/tmp): those change the absolute spelling without making the
    // install root itself a symlink (already refused above via isSymbolicLink).
    if (pathKey(realpathSync(target)) !== pathKey(target) && process.platform === "win32") {
      return {
        kind: "install",
        action: "refuse",
        ok: false,
        path: target,
        reason: "resolved install path differs from OCCAM_HOME",
      };
    }
  } catch (err) {
    return {
      kind: "install",
      action: "refuse",
      ok: false,
      path: target,
      reason: `cannot resolve install tree: ${err instanceof Error ? err.message : String(err)}`,
    };
  }

  if (existsSync(join(target, ".git"))) {
    return {
      kind: "install",
      action: "preserve",
      ok: true,
      path: target,
      reason: "source checkout detected; repository files are never uninstalled",
    };
  }

  const versionPath = join(target, "VERSION");
  const manifestPath = join(target, "release-manifest.json");
  const scripts = ["scripts/occam.mjs", "scripts/launch-mcp-host.mjs"];
  const missing = ["VERSION", "release-manifest.json", ...scripts].filter(
    (rel) => !isRegularFile(join(target, ...rel.split("/"))),
  );
  if (missing.length) {
    return {
      kind: "install",
      action: "refuse",
      ok: false,
      path: target,
      reason: `not a recognized release install (missing ${missing.join(", ")})`,
    };
  }

  let version = "";
  /** @type {{ version?: unknown, rid?: unknown, layout?: unknown }} */
  let manifest = {};
  try {
    version = readFileSync(versionPath, "utf8").trim();
    manifest = JSON.parse(readFileSync(manifestPath, "utf8"));
  } catch (err) {
    return {
      kind: "install",
      action: "refuse",
      ok: false,
      path: target,
      reason: `invalid release metadata: ${err instanceof Error ? err.message : String(err)}`,
    };
  }
  const expectedRid = opts.rid || resolveRid();
  const metadataProblems = [];
  if (!version) metadataProblems.push("VERSION is empty");
  if (!manifest || typeof manifest !== "object" || Array.isArray(manifest)) {
    metadataProblems.push("manifest root is not an object");
  } else {
    if (manifest.version !== version) metadataProblems.push("manifest version does not match VERSION");
    if (manifest.layout !== "level-b") metadataProblems.push("manifest layout is not level-b");
    if (manifest.rid !== expectedRid) metadataProblems.push(`manifest RID is not ${expectedRid}`);
  }
  const hostNames = expectedRid.startsWith("win-")
    ? ["OccamMcp.Core.exe", "FFOccamMcp.Core.exe"]
    : ["OccamMcp.Core", "FFOccamMcp.Core"];
  if (!hostNames.some((name) => isRegularFile(join(target, name)))) {
    metadataProblems.push(`missing ${hostNames.join(" or ")}`);
  }
  if (metadataProblems.length) {
    return {
      kind: "install",
      action: "refuse",
      ok: false,
      path: target,
      reason: `not a recognized release install (${metadataProblems.join("; ")})`,
    };
  }

  return {
    kind: "install",
    action: "remove",
    ok: true,
    path: target,
    reason: "recognized Level B Occam release tree",
  };
}
