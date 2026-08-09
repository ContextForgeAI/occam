/**
 * Managed ownership + post-verify cleanup decisions (product-agnostic).
 *
 * Ownership heuristic (Wave 1):
 * - explicit env marker OCCAM_CONNECT_MANAGED=occam-managed:v1, or
 * - registration already points at Occam launcher / wrapper.
 *
 * Custom user `ff-occam` entries without those signals are not overwritten
 * unless the operator passes --force / OCCAM_CONNECT force path.
 */
import { OCCAM_MANAGED_ENV_KEY, OCCAM_MANAGED_MARKER } from "./kinds.mjs";
import { normalizePathish } from "./process.mjs";
import { normalizeOccamHome } from "./launch-spec.mjs";
import { posix as pathPosix, win32 as pathWin32 } from "node:path";

const MANAGED_PATH_SUFFIXES = [
  "/scripts/launch-mcp-host.mjs",
  "/scripts/occam-wrapper.sh",
];

/** @param {string} value */
function stripOuterQuotes(value) {
  const text = String(value || "").trim();
  if (
    text.length >= 2 &&
    ((text.startsWith('"') && text.endsWith('"')) ||
      (text.startsWith("'") && text.endsWith("'")))
  ) {
    return text.slice(1, -1);
  }
  return text;
}

/** @param {string} value */
function isWindowsPathish(value) {
  const normalized = normalizePathish(stripOuterQuotes(value));
  return /^[A-Za-z]:\//.test(normalized) || normalized.startsWith("//");
}

/** @param {string} value */
function managedPathRoot(value) {
  const raw = normalizePathish(stripOuterQuotes(value));
  if (!raw) return null;
  const windows = isWindowsPathish(raw);
  const normalized = windows
    ? normalizePathish(pathWin32.normalize(raw.replace(/\//g, "\\")))
    : pathPosix.normalize(raw);
  const comparable = windows ? normalized.toLowerCase() : normalized;
  for (const suffix of MANAGED_PATH_SUFFIXES) {
    const expectedSuffix = windows ? suffix.toLowerCase() : suffix;
    if (comparable.endsWith(expectedSuffix)) {
      const root = normalized.slice(0, -suffix.length).replace(/\/+$/, "");
      return root || null;
    }
  }
  return null;
}

/** @param {string} left @param {string} right */
function samePath(left, right) {
  const rawLeft = normalizePathish(stripOuterQuotes(left));
  const rawRight = normalizePathish(stripOuterQuotes(right));
  if (!rawLeft || !rawRight) return false;
  const windows = isWindowsPathish(rawLeft) || isWindowsPathish(rawRight);
  const normalize = windows
    ? (value) => normalizePathish(pathWin32.normalize(value.replace(/\//g, "\\"))).toLowerCase()
    : (value) => pathPosix.normalize(value);
  return normalize(rawLeft) === normalize(rawRight);
}

/**
 * @param {{ command?: string, args?: string[], env?: Record<string, string>, cwd?: string }|null|undefined} entry
 * @param {string} [occamHome]
 */
export function looksLikeOccamManagedEntry(entry, occamHome = "") {
  if (!entry) return false;
  const env = entry.env || {};
  const marked = env[OCCAM_MANAGED_ENV_KEY] === OCCAM_MANAGED_MARKER;
  const home = normalizeOccamHome(occamHome);
  const pathRoots = [entry.command || "", ...(entry.args || [])]
    .map(managedPathRoot)
    .filter(Boolean);

  // Generic inspection without a requested root preserves the legacy signal:
  // a marker or an exact generated launcher/wrapper path means Occam-managed.
  if (!home) return marked || pathRoots.length > 0;

  // With a requested root (all mutation/removal paths), every ownership-bearing
  // path must bind to that exact root. A marker alone is intentionally not
  // enough: it is shared by every Occam installation on the machine.
  const explicitRoots = [];
  if (env.OCCAM_HOME) explicitRoots.push(env.OCCAM_HOME);
  if (entry.cwd) explicitRoots.push(entry.cwd);
  if ([...pathRoots, ...explicitRoots].some((root) => !samePath(root, home))) {
    return false;
  }

  if (pathRoots.length > 0) return true;
  return marked && explicitRoots.length > 0;
}

/**
 * Per-host cleanup after apply mutated config but verify did not Ready.
 * Boundary is per-host (not global all-or-nothing).
 *
 * @param {{
 *   applied: boolean,
 *   action: string|null|undefined,
 *   verifyOk: boolean,
 *   configured?: boolean,
 *   requiresRestart?: boolean,
 *   requiresUserAction?: boolean,
 *   hostBlocked?: boolean,
 * }} state
 * @returns {'none'|'remove'|'restore'}
 */
export function decidePostVerifyCleanup(state) {
  const preserveRegistration =
    state.requiresRestart === true ||
    (state.configured === true &&
      state.requiresUserAction === true &&
      state.hostBlocked === true);
  if (!state.applied || state.verifyOk || preserveRegistration) return "none";
  if (state.action === "add") return "remove";
  if (state.action === "update") return "restore";
  return "none";
}
