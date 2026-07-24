/**
 * Single source of truth for MCP host wiring snippets (stdio: command / args / env / cwd).
 * Aligns with https://modelcontextprotocol.io/docs/develop/connect-local-servers
 */
import { join } from "node:path";

/** @typedef {'cursor-workspace'|'cursor-global'|'hermes'|'openclaw'|'claude-desktop'|'generic-stdio'|'cli-only'} ConnectionKind */
/** @typedef {'cursor'|'hermes'|'openclaw'|'claude-desktop'|'generic-stdio'|'cli-only'} HostTarget */

export const CONNECTION_KINDS = [
  "cursor-workspace",
  "cursor-global",
  "hermes",
  "openclaw",
  "claude-desktop",
  "generic-stdio",
  "cli-only",
];

const WORKSPACE_FOLDER = "${workspaceFolder}";

/**
 * @param {HostTarget} hostTarget
 * @param {{ workspace?: boolean }} [options]
 * @returns {ConnectionKind}
 */
export function hostTargetToConnectionKind(hostTarget, options = {}) {
  switch (hostTarget) {
    case "cli-only":
      return "cli-only";
    case "hermes":
      return "hermes";
    case "openclaw":
      return "openclaw";
    case "claude-desktop":
      return "claude-desktop";
    case "generic-stdio":
      return "generic-stdio";
    case "cursor":
    default:
      return options.workspace ? "cursor-workspace" : "cursor-global";
  }
}

/**
 * @param {string} occamHome
 */
function normalizeHome(occamHome) {
  return occamHome.replace(/\\/g, "/");
}

/**
 * @param {string} home
 */
function launcherPath(home) {
  return join(home, "scripts", "launch-mcp-host.mjs").replace(/\\/g, "/");
}

/**
 * @param {string} home
 */
function wrapperPath(home) {
  return join(home, "scripts", "occam-wrapper.sh").replace(/\\/g, "/");
}

/**
 * @param {Record<string, string>} env
 */
function mergeEnv(env) {
  return Object.fromEntries(
    Object.entries(env)
      .filter(([, v]) => v != null && String(v).length > 0)
      .map(([k, v]) => [k, String(v)]),
  );
}

/**
 * @param {{ occamHome: string, connectionKind: ConnectionKind, env?: Record<string, string>, workspaceFolder?: string }} params
 */
export function buildMcpSnippet(params) {
  const home = normalizeHome(params.occamHome);
  const kind = params.connectionKind;
  const env = mergeEnv({ OCCAM_HOME: home, ...params.env });

  if (kind === "cli-only") {
    return {
      connectionKind: kind,
      format: "none",
      mcpConfig: null,
      message: "cli-only — no MCP host wiring. Use: node scripts/hermes-smoke.mjs",
    };
  }

  if (kind === "hermes") {
    const wrapper = wrapperPath(home);
    const yaml = buildHermesYaml(home, env);
    return {
      connectionKind: kind,
      format: "yaml",
      mcpConfig: null,
      hermesYaml: yaml,
      mcpServers: {
        "ff-occam": {
          command: wrapper,
          env: { ...env },
        },
      },
    };
  }

  const launcher = launcherPath(home);
  /** @type {{ command: string, args: string[], env: Record<string, string>, cwd?: string }} */
  const server = {
    command: "node",
    args: [],
    env: { ...env },
  };

  if (kind === "cursor-workspace") {
    const wf = params.workspaceFolder ?? WORKSPACE_FOLDER;
    server.args = [`${wf}/scripts/launch-mcp-host.mjs`];
    server.cwd = wf;
    server.env.OCCAM_HOME = wf;
    if (!server.env.OCCAM_BANNER && !server.env.WT_OCCAM_BANNER) {
      server.env.WT_OCCAM_BANNER = "0";
    }
  } else {
    server.args = [launcher];
    server.cwd = home;
    if (kind === "cursor-global" || kind === "claude-desktop" || kind === "openclaw" || kind === "generic-stdio") {
      if (!server.env.OCCAM_BANNER && !server.env.WT_OCCAM_BANNER) {
        server.env.OCCAM_BANNER = "0";
        server.env.WT_OCCAM_BANNER = "0";
      }
    }
  }

  const mcpConfig = {
    mcpServers: {
      "ff-occam": server,
    },
  };

  return {
    connectionKind: kind,
    format: "json",
    mcpConfig,
    hermesYaml: null,
    message: null,
  };
}

/**
 * @param {string} home
 * @param {Record<string, string>} env
 */
export function buildHermesYaml(home, env) {
  const wrapper = wrapperPath(home);
  const lines = [
    "mcp_servers:",
    "  ff-occam:",
    `    command: "${wrapper}"`,
    "    env:",
  ];
  for (const [k, v] of Object.entries(env).sort(([a], [b]) => a.localeCompare(b))) {
    lines.push(`      ${k}: "${v}"`);
  }
  return `${lines.join("\n")}\n`;
}

/**
 * @param {import("./onboard-flow.mjs").buildOnboardResult extends never ? never : ReturnType<import("./onboard-flow.mjs").buildOnboardResult>} result
 * @param {{ workspace?: boolean }} [options]
 */
export function buildSnippetFromOnboardResult(result, options = {}) {
  const connectionKind = hostTargetToConnectionKind(result.hostTarget, options);
  return buildMcpSnippet({
    occamHome: result.occamHome,
    connectionKind,
    env: result.env,
  });
}

/**
 * @param {ConnectionKind} connectionKind
 */
export function getConnectionNextSteps(connectionKind) {
  switch (connectionKind) {
    case "cursor-workspace":
      return [
        "Copy .cursor/mcp.json.example → .cursor/mcp.json in the repo workspace.",
        "Reload MCP in Cursor Settings → MCP (https://cursor.com/docs/context/mcp).",
        "Run: node scripts/hermes-smoke.mjs",
      ];
    case "cursor-global":
      return [
        "Paste the JSON below into Cursor Settings → MCP → Edit config.",
        "Reload MCP servers.",
        "Run: node scripts/hermes-smoke.mjs",
      ];
    case "hermes":
      return [
        "Merge the YAML below into ~/.hermes/config.yaml under mcp_servers.",
        "Disable legacy web-transcoder if present.",
        "Reload MCP in Hermes (/reload-mcp).",
        "Run: node scripts/hermes-smoke.mjs",
      ];
    case "openclaw":
    case "claude-desktop":
    case "generic-stdio":
      return [
        "Paste the JSON below into your host MCP registry.",
        "Reload or restart the MCP host.",
        "Run: node scripts/hermes-smoke.mjs",
      ];
    case "cli-only":
      return [
        "No MCP host wiring — scripts/smoke only.",
        "Run: node scripts/hermes-smoke.mjs",
        "Docs: INSTALL.md",
      ];
    default:
      return ["Run: node scripts/hermes-smoke.mjs"];
  }
}
