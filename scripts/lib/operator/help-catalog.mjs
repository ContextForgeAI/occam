import { COMMAND_REGISTRY, findCommand } from "./occam-command-registry.mjs";
import { HELP_SCHEMA_VERSION, readOccamVersion } from "./onboard-schema.mjs";

/** Post-install operator checklist — surfaced in occam-help and JSON catalog. */
export const OPERATOR_NEXT_STEPS = [
  {
    id: "path",
    summary: "Add occam to PATH (once per shell profile)",
    command: 'export PATH="$OCCAM_HOME/scripts:$PATH"',
    doc: "docs/getting-started.md#operator-cli",
  },
  {
    id: "control",
    summary: "Open operator menu (settings, doctor, updates, help)",
    command: "occam",
    doc: "docs/getting-started.md#operator-cli",
  },
  {
    id: "verify-install",
    summary: "Confirm doctor + published host + browser smoke",
    command: "occam doctor",
    alt: "node scripts/lib/verify-install.mjs",
    doc: "INSTALL.md#verify",
  },
  {
    id: "connect",
    summary: "Wire MCP host — snippet or onboard wizard",
    command: "occam onboard",
    alt: "occam snippet",
    doc: "docs/getting-started.md#wire-into-cursor",
  },
  {
    id: "reload-mcp",
    summary: "Reload MCP in host (Cursor Settings → MCP, Hermes /reload-mcp, …)",
    doc: "docs/getting-started.md",
  },
  {
    id: "hermes-smoke",
    summary: "stdio smoke — initialize, tools/list, occam_probe",
    command: "occam smoke",
    doc: "INSTALL.md#verify",
  },
];

/**
 * @param {{ tier?: string, query?: string }} [opts]
 */
export function buildHelpViewModel(opts = {}) {
  const tier = opts.tier?.trim().toLowerCase();
  const query = opts.query?.trim().toLowerCase();

  let commands = [...COMMAND_REGISTRY];
  if (tier) {
    commands = commands.filter((row) => row.tier === tier);
  }

  if (query) {
    commands = commands.filter(
      (row) =>
        row.id.toLowerCase().includes(query) ||
        row.summary.toLowerCase().includes(query) ||
        row.usage.toLowerCase().includes(query),
    );
  }

  commands.sort((a, b) => a.id.localeCompare(b.id));

  return {
    schema_version: HELP_SCHEMA_VERSION,
    generator: readOccamVersion(),
    title: "FF-Occam CLI",
    tiers: ["operator", "ci", "maintainer"],
    commands,
    nextSteps: OPERATOR_NEXT_STEPS,
  };
}

/** @param {string} id */
export function buildCommandDetail(id) {
  const row = findCommand(id);
  if (!row) {
    return null;
  }

  const related = COMMAND_REGISTRY.filter(
    (other) =>
      other.id !== row.id &&
      other.seeAlso &&
      row.seeAlso &&
      other.seeAlso === row.seeAlso,
  ).slice(0, 5);

  return {
    ...row,
    relatedCommands: related.map((r) => r.id),
  };
}
