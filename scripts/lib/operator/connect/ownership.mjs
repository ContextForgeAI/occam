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

/**
 * @param {{ command?: string, args?: string[], env?: Record<string, string>, cwd?: string }|null|undefined} entry
 * @param {string} [occamHome]
 */
export function looksLikeOccamManagedEntry(entry, occamHome = "") {
  if (!entry) return false;
  const env = entry.env || {};
  if (env[OCCAM_MANAGED_ENV_KEY] === OCCAM_MANAGED_MARKER) return true;

  const home = normalizeOccamHome(occamHome);
  const cmd = normalizePathish(entry.command || "");
  const args = (entry.args || []).map((a) => normalizePathish(a));

  if (cmd.includes("occam-wrapper.sh") || cmd.endsWith("/occam-wrapper.sh")) {
    return true;
  }
  if (args.some((a) => a.includes("launch-mcp-host.mjs"))) {
    return true;
  }
  if (home && cmd.includes(normalizePathish(home)) && cmd.includes("occam-wrapper")) {
    return true;
  }
  return false;
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
