/**
 * Unified `occam <subcommand>` map — data only.
 * @typedef {'node'|'shell'|'powershell'|'internal'} DelegateKind
 * @typedef {{
 *   name: string,
 *   aliases?: string[],
 *   summary: string,
 *   delegate: DelegateKind,
 *   script?: string,
 *   internalAction?: string,
 *   registryId?: string,
 *   passthrough?: boolean,
 * }} CliSubcommand
 */

/** @type {CliSubcommand[]} */
export const CLI_SUBCOMMANDS = [
  {
    name: "doctor",
    summary: "Preflight — workers, Playwright, AOT host",
    delegate: "shell",
    script: "occam-doctor",
    registryId: "occam-doctor",
  },
  {
    name: "onboard",
    aliases: ["settings"],
    summary: "Profile wizard → ~/.occam/onboard.json + MCP snippet",
    delegate: "node",
    script: "occam-onboard.mjs",
    registryId: "occam-onboard",
    passthrough: true,
  },
  {
    name: "help",
    summary: "CLI catalog and command detail",
    delegate: "node",
    script: "occam-help.mjs",
    registryId: "occam-help",
    passthrough: true,
  },
  {
    name: "refresh",
    aliases: ["restart"],
    summary: "Stop MCP host processes + re-run doctor",
    delegate: "node",
    script: "occam-refresh-host.mjs",
    registryId: "occam-refresh-host",
    passthrough: true,
  },
  {
    name: "smoke",
    summary: "stdio MCP smoke — tools/list + occam_probe",
    delegate: "node",
    script: "hermes-smoke.mjs",
    registryId: "hermes-smoke",
    passthrough: true,
  },
  {
    name: "update",
    summary: "Check for a newer release (read-only)",
    delegate: "internal",
    internalAction: "update",
  },
  {
    name: "session",
    summary: "Session profiles — init, list, import, export-state",
    delegate: "node",
    script: "occam-session.mjs",
    registryId: "occam-session",
    passthrough: true,
  },
  {
    name: "snippet",
    summary: "Paste-ready MCP JSON for OCCAM_HOME",
    delegate: "node",
    script: "lib/print-mcp-snippet.mjs",
    registryId: "print-mcp-snippet",
    passthrough: true,
  },
  {
    name: "skill",
    summary: "Install portable occam skill for any agent harness",
    delegate: "node",
    script: "occam-skill-install.mjs",
    registryId: "occam-skill-install",
    passthrough: true,
  },
  {
    name: "control",
    summary: "Interactive operator menu (soft TUI)",
    delegate: "internal",
    internalAction: "control",
  },
  {
    name: "status",
    summary: "Install summary — version, onboard, optional update",
    delegate: "internal",
    internalAction: "status",
  },
  {
    name: "contract",
    aliases: ["version-surface"],
    summary: "Public MCP tools/list contract + version-surface fingerprint",
    delegate: "node",
    script: "check-public-mcp-contract.mjs",
    registryId: "check-public-mcp-contract",
    passthrough: true,
  },
];

/** @param {string} name */
export function findSubcommand(name) {
  const key = name.trim().toLowerCase();
  return CLI_SUBCOMMANDS.find(
    (row) =>
      row.name === key || row.aliases?.some((alias) => alias.toLowerCase() === key),
  );
}

/** @returns {string} */
export function formatSubcommandUsage() {
  const lines = ["occam — unified FF-Occam operator CLI", "", "Usage:", "  occam                  Open control menu (TTY)", "  occam <command> [args]", ""];
  lines.push("Commands:");
  for (const row of CLI_SUBCOMMANDS) {
    const alias = row.aliases?.length ? ` (${row.aliases.join(", ")})` : "";
    lines.push(`  ${row.name.padEnd(10)} ${row.summary}${alias}`);
  }
  lines.push("", "Global flags: --json", "Env: OCCAM_HOME, PATH should include $OCCAM_HOME/scripts");
  return lines.join("\n");
}
