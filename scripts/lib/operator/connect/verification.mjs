/**
 * Verification ladder — Ready requires host discovery when the adapter can reach it.
 *
 * LEVEL 0: Occam installed
 * LEVEL 1: host config registration valid
 * LEVEL 2: Occam MCP process launches
 * LEVEL 3: MCP initialize succeeds
 * LEVEL 4: tools/list succeeds
 * LEVEL 5: host reports Occam registered/discovered
 * LEVEL 6: optional tool invocation through host
 */

/** @typedef {0|1|2|3|4|5|6} VerificationLevel */

export const VERIFICATION_LEVELS = Object.freeze({
  INSTALLED: 0,
  CONFIG_VALID: 1,
  PROCESS_LAUNCHES: 2,
  INITIALIZE_OK: 3,
  TOOLS_LIST_OK: 4,
  HOST_DISCOVERS: 5,
  HOST_TOOL_CALL: 6,
});

/**
 * @param {VerificationLevel} level
 */
export function levelLabel(level) {
  switch (level) {
    case 0:
      return "Occam installed";
    case 1:
      return "host config registration valid";
    case 2:
      return "Occam MCP process launches";
    case 3:
      return "MCP initialize succeeds";
    case 4:
      return "tools/list succeeds";
    case 5:
      return "host reports Occam registered/discovered";
    case 6:
      return "host-mediated tool call succeeds";
    default:
      return `level ${level}`;
  }
}

/**
 * Honest Ready gate: connected + verified at least through Occam tools/list,
 * and host discovery when the adapter claims it can reach Level 5.
 *
 * @param {{
 *   occamLevel: VerificationLevel,
 *   hostLevel: VerificationLevel,
 *   maxHostLevel: VerificationLevel,
 *   requiresRestart?: boolean,
 *   configured?: boolean,
 *   requiresUserAction?: boolean,
 *   hostBlocked?: boolean,
 *   actionMessage?: string,
 * }} state
 */
export function evaluateReadyState(state) {
  const occamOk = state.occamLevel >= VERIFICATION_LEVELS.TOOLS_LIST_OK;
  const configured =
    state.configured === true || state.hostLevel >= VERIFICATION_LEVELS.CONFIG_VALID;
  const hostTarget = Math.min(state.maxHostLevel, VERIFICATION_LEVELS.HOST_DISCOVERS);
  const hostOk =
    hostTarget < VERIFICATION_LEVELS.HOST_DISCOVERS
      ? state.hostLevel >= VERIFICATION_LEVELS.CONFIG_VALID
      : state.hostLevel >= VERIFICATION_LEVELS.HOST_DISCOVERS;

  if (
    occamOk &&
    configured &&
    state.requiresUserAction === true &&
    state.hostBlocked === true
  ) {
    return {
      configured: true,
      requiresUserAction: true,
      hostBlocked: true,
      ready: false,
      status: "Configured — action required",
      message:
        state.actionMessage ||
        "Registration is valid but blocked by host trust, permission, or policy",
      occamLevel: state.occamLevel,
      hostLevel: state.hostLevel,
    };
  }

  if (occamOk && hostOk && !state.requiresRestart) {
    return {
      configured: true,
      requiresUserAction: false,
      hostBlocked: false,
      ready: true,
      status: "Ready",
      message: "Occam connected and verified",
      occamLevel: state.occamLevel,
      hostLevel: state.hostLevel,
    };
  }

  if (occamOk && hostOk && state.requiresRestart) {
    return {
      configured: true,
      requiresUserAction: false,
      hostBlocked: false,
      ready: false,
      status: "Configured — restart required",
      message: "Registration verified; restart or reload the host session to activate tools",
      occamLevel: state.occamLevel,
      hostLevel: state.hostLevel,
      requiresRestart: true,
    };
  }

  if (occamOk && configured) {
    return {
      configured: true,
      requiresUserAction: false,
      hostBlocked: false,
      ready: false,
      status: "Configured — host discovery incomplete",
      message: `Config written (${levelLabel(state.hostLevel)}); host discovery not confirmed`,
      occamLevel: state.occamLevel,
      hostLevel: state.hostLevel,
    };
  }

  return {
    configured: false,
    requiresUserAction: false,
    hostBlocked: false,
    ready: false,
    status: "Not ready",
    message: `Verification incomplete (occam=${state.occamLevel}, host=${state.hostLevel})`,
    occamLevel: state.occamLevel,
    hostLevel: state.hostLevel,
  };
}

/**
 * Top-level Ready requires every targeted connection to be ready.
 * One host verify failure after apply → Partial (never global Ready).
 * If peers are Ready and remaining hosts need only restart or user action,
 * surface Almost ready / Action required rather than Partial.
 *
 * @param {Array<{ name?: string, readyState?: { ready?: boolean, status?: string, requiresRestart?: boolean, requiresUserAction?: boolean, hostBlocked?: boolean }, hostVerify?: { ok?: boolean } }>} connections
 */
export function aggregateConnectionReady(connections) {
  if (!connections.length) {
    return { ready: false, status: "Not ready", message: "no host connections" };
  }
  const allReady = connections.every((c) => c.readyState?.ready === true);
  if (allReady) {
    return {
      ready: true,
      status: "Ready",
      message: `Connected and verified: ${connections.map((c) => c.name).join(", ")}`,
    };
  }

  const actionPending = connections.filter(
    (c) =>
      c.readyState?.ready !== true &&
      c.readyState?.requiresUserAction === true &&
      c.readyState?.hostBlocked === true,
  );
  const restartPending = connections.filter(
    (c) =>
      c.readyState?.ready !== true &&
      (c.readyState?.requiresRestart === true ||
        /restart required/i.test(c.readyState?.status || "")),
  );
  const onlyRecoverableBlockers = connections.every((c) => {
    if (c.readyState?.ready === true) return true;
    const restart =
      c.readyState?.requiresRestart === true ||
      /restart required/i.test(c.readyState?.status || "");
    const actionRequired =
      c.readyState?.requiresUserAction === true &&
      c.readyState?.hostBlocked === true;
    return (restart && c.hostVerify?.ok === true) || actionRequired;
  });
  if (onlyRecoverableBlockers && actionPending.length > 0) {
    const actions = actionPending.map((c) => c.name).filter(Boolean);
    const restarts = restartPending.map((c) => c.name).filter(Boolean);
    const details = [];
    if (actions.length) details.push(`Action required for ${actions.join(", ")}`);
    if (restarts.length) details.push(`restart ${restarts.join(", ")}`);
    return {
      ready: false,
      status: "Action required",
      message: details.join("; "),
    };
  }
  if (onlyRecoverableBlockers && restartPending.length > 0) {
    const needing = restartPending.map((c) => c.name).filter(Boolean);
    return {
      ready: false,
      status: "Almost ready",
      message: needing.length
        ? `Restart ${needing.join(", ")} once to activate Occam`
        : "Restart required host(s) to activate Occam",
    };
  }

  return {
    ready: false,
    status: "Partial",
    message: connections
      .map((c) => `${c.name}: ${c.readyState?.status || "unknown"}`)
      .join("; "),
  };
}
