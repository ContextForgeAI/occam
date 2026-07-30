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
 * Adapters that cannot observe host runtime (maxHostLevel < HOST_DISCOVERS)
 * may reach "Configured" when registration matches — never perpetual
 * "restart required" without evidence, and never fake Ready.
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
  const canDiscoverHost = state.maxHostLevel >= VERIFICATION_LEVELS.HOST_DISCOVERS;
  const hostDiscovered =
    canDiscoverHost && state.hostLevel >= VERIFICATION_LEVELS.HOST_DISCOVERS;
  const configValid = state.hostLevel >= VERIFICATION_LEVELS.CONFIG_VALID;

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

  // Ready only when the adapter can and did confirm host discovery.
  if (occamOk && hostDiscovered && !state.requiresRestart) {
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

  // Restart/reload only with caller evidence (e.g. config mutated this run).
  if (occamOk && configValid && state.requiresRestart) {
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

  // Config correct; CLI cannot prove the running host session loaded it.
  if (occamOk && configValid && !canDiscoverHost && !state.requiresRestart) {
    return {
      configured: true,
      requiresUserAction: false,
      hostBlocked: false,
      ready: false,
      status: "Configured",
      message:
        "Registration is correct; open or reload the host to use Occam (runtime not verifiable from the CLI)",
      occamLevel: state.occamLevel,
      hostLevel: state.hostLevel,
      requiresRestart: false,
    };
  }

  if (occamOk && configured) {
    return {
      configured: true,
      requiresUserAction: false,
      hostBlocked: false,
      ready: false,
      status: "Configured — host discovery incomplete",
      message:
        "Config was written, but Occam could not confirm the app loaded the connection yet",
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
 * @param {object} c
 */
function connectionNeedsRestart(c) {
  return (
    c.readyState?.requiresRestart === true ||
    /restart required/i.test(c.readyState?.status || "")
  );
}

/**
 * @param {object} c
 */
function connectionNeedsAction(c) {
  return (
    c.readyState?.requiresUserAction === true && c.readyState?.hostBlocked === true
  );
}

/**
 * @param {object} c
 */
function connectionConfiguredOnly(c) {
  return (
    c.readyState?.ready !== true &&
    !connectionNeedsRestart(c) &&
    !connectionNeedsAction(c) &&
    (c.readyState?.status === "Configured" ||
      (c.hostVerify?.ok === true && c.readyState?.configured === true))
  );
}

/**
 * Top-level Ready requires every targeted connection to be ready.
 * One host verify failure after apply → Partial (never global Ready).
 * If peers are Ready and remaining hosts need only restart or user action,
 * surface Almost ready / Action required rather than Partial.
 * Config-only hosts (no runtime verify) aggregate as Configured — not a restart loop.
 *
 * @param {Array<{ name?: string, readyState?: { ready?: boolean, status?: string, requiresRestart?: boolean, requiresUserAction?: boolean, hostBlocked?: boolean, configured?: boolean }, hostVerify?: { ok?: boolean } }>} connections
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
    (c) => c.readyState?.ready !== true && connectionNeedsAction(c),
  );
  const restartPending = connections.filter(
    (c) => c.readyState?.ready !== true && connectionNeedsRestart(c),
  );
  const configuredPending = connections.filter((c) => connectionConfiguredOnly(c));

  const onlyRecoverableBlockers = connections.every((c) => {
    if (c.readyState?.ready === true) return true;
    if (connectionConfiguredOnly(c)) return true;
    const restart = connectionNeedsRestart(c);
    const actionRequired = connectionNeedsAction(c);
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
  if (
    onlyRecoverableBlockers &&
    configuredPending.length > 0 &&
    configuredPending.length +
      connections.filter((c) => c.readyState?.ready === true).length ===
      connections.length
  ) {
    const names = configuredPending.map((c) => c.name).filter(Boolean);
    return {
      ready: false,
      status: "Configured",
      message: names.length
        ? `Occam is configured for ${names.join(", ")}`
        : "Occam is configured for the selected app(s)",
    };
  }

  return {
    ready: false,
    status: "Action required",
    message: connections
      .map((c) => `${c.name}: ${c.readyState?.status || "unknown"}`)
      .join("; "),
  };
}
