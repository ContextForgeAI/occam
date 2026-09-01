/**
 * Operator verbs live in the release tarball CLI (`scripts/occam.mjs`), not in
 * the experimental npm MCP launcher. Intercept before spawning OccamMcp.Core so
 * `npx ff-occam connect` fails honestly instead of starting stdio MCP with junk argv.
 *
 * Keep the verb list aligned with scripts/lib/operator/occam-cli-subcommands.mjs
 * (names + aliases). Do not vendor the full operator tree into the npm package.
 */

/** @type {ReadonlySet<string>} */
export const OPERATOR_CLI_VERBS = Object.freeze(
  new Set([
    "doctor",
    "onboard",
    "settings",
    "connect",
    "disconnect",
    "uninstall",
    "help",
    "refresh",
    "restart",
    "smoke",
    "update",
    "session",
    "snippet",
    "skill",
    "control",
    "status",
  ]),
);

/**
 * @param {string[]} argv process.argv.slice(2)
 * @returns {string | null} matched verb, or null
 */
export function matchOperatorCliVerb(argv) {
  const first = argv[0];
  if (typeof first !== "string" || first.length === 0) return null;
  if (first.startsWith("-")) return null;
  const verb = first.toLowerCase();
  return OPERATOR_CLI_VERBS.has(verb) ? verb : null;
}

/**
 * @param {string} verb
 * @param {{ prefix?: string }} [opts]
 * @returns {string}
 */
export function formatOperatorCliRefusal(verb, opts = {}) {
  const prefix = opts.prefix ?? "[ff-occam]";
  return [
    `${prefix} '${verb}' is an operator CLI command, not an MCP host flag.`,
    `${prefix} The npm package only starts the MCP server (experimental).`,
    `${prefix} Install the release CLI, then run: occam ${verb}`,
    "",
    "Guarded install:",
    "  curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh | bash",
    "  # Windows: irm https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.ps1 | iex",
    "",
    "See INSTALL.md",
  ].join("\n");
}

/**
 * @param {string[]} argv
 * @param {{ prefix?: string }} [opts]
 * @returns {never | void} exits process when matched
 */
export function refuseOperatorCliVerbOrContinue(argv, opts = {}) {
  const verb = matchOperatorCliVerb(argv);
  if (!verb) return;
  console.error(formatOperatorCliRefusal(verb, opts));
  process.exit(1);
}
