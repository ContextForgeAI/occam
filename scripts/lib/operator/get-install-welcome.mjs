#!/usr/bin/env node
/**
 * Welcome + setup mode for get-ff-occam.sh / .ps1 (logger-style banner).
 *
 *   node get-install-welcome.mjs print
 *   node get-install-welcome.mjs resolve   # OCCAM_SETUP → auto|manual (ask→auto when non-TTY)
 *   node get-install-welcome.mjs prompt    # same contract; ask+TTY shows menu (Enter=auto)
 */
import { createInterface } from "node:readline/promises";
import { stdin as input, stdout as output } from "node:process";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { GET_INSTALL_WELCOME, SETUP_MODE_COPY } from "./get-install-copy.mjs";
import {
  parseSetupChoice,
  readSetupIntent,
  resolveSetupIntent,
} from "./bootstrap-setup.mjs";
import { shouldUseInstallColor } from "./install-ux.mjs";
import { indent, sectionBox } from "./render/tty-layout.mjs";
const WIDTH = 52;

/**
 * Public install identity (quiet) or logger-style banner (verbose).
 * Host MCP stderr still owns the spaced wordmark in OccamStderrAnsiSink —
 * this module is the install/bootstrap twin, not a duplicate of that art.
 *
 * @param {boolean} useColor
 * @param {{ version?: string, verbose?: boolean }} [opts]
 */
export function renderProductBanner(useColor, opts = {}) {
  const g = useColor ? "\u001b[38;5;244m" : "";
  const c = useColor ? "\u001b[38;5;45m" : "";
  const r = useColor ? "\u001b[0m" : "";
  const version = (opts.version ?? process.env.OCCAM_VERSION)?.trim() || "";
  const title = version ? `${GET_INSTALL_WELCOME.title} ${version}` : GET_INSTALL_WELCOME.title;
  const verbose =
    opts.verbose === true ||
    process.env.OCCAM_VERBOSE === "1" ||
    process.env.OCCAM_VERBOSE === "true";

  // Public welcome is a single product line — internals stay behind OCCAM_VERBOSE.
  if (verbose) {
    const w = useColor ? "\u001b[38;5;255m" : "";
    const ok = useColor ? "\u001b[38;5;46m" : "";
    const lines = [
      "",
      `${c}  ${title}${r}`,
      `${g}${"─".repeat(WIDTH)}${r}`,
      `${g}  ARCHITECTURE${r}   ${w}${GET_INSTALL_WELCOME.architecture}${r}`,
      `${g}  MODE${r}           ${w}${GET_INSTALL_WELCOME.mode}${r}`,
      `${g}  WORKERS${r}        ${w}${GET_INSTALL_WELCOME.workers}${r}`,
      `${g}${"─".repeat(WIDTH)}${r}`,
    ];
    for (const row of GET_INSTALL_WELCOME.statusRows) {
      const pad = Math.max(1, 14 - row.label.length);
      lines.push(`${ok}  ✓${r} ${g}${row.label}${" ".repeat(pad)}${w}${row.value}${r}`);
    }
    lines.push(`${g}${"─".repeat(WIDTH)}${r}`);
    lines.push(`${g}  ${GET_INSTALL_WELCOME.tagline}${r}`);
    lines.push("");
    return lines.join("\n");
  }

  return ["", `${c}  ${title}${r}`, `${g}  ${GET_INSTALL_WELCOME.tagline}${r}`, ""].join("\n");
}

function renderSetupMenu() {
  const { auto, manual, description, hint } = SETUP_MODE_COPY;
  return [
    sectionBox(SETUP_MODE_COPY.title, [description, ""]),
    indent("Choose setup mode:"),
    "",
    indent(`[1] ${auto.label.padEnd(8)} ${auto.summary}`),
    indent(`[2] ${manual.label.padEnd(8)} ${manual.summary}`),
    "",
    indent(hint),
    "",
    `› Setup [1]: `,
  ].join("\n");
}

export function printWelcome() {
  process.stdout.write(renderProductBanner(shouldUseInstallColor()));
}

/**
 * Non-interactive resolution (ask collapses to auto).
 * @param {NodeJS.ProcessEnv} [env]
 * @returns {"auto"|"manual"}
 */
export function resolveSetupFromEnv(env = process.env) {
  try {
    const intent = resolveSetupIntent({ env, interactive: false });
    return intent === "ask" ? "auto" : intent;
  } catch (err) {
    console.error(`error: ${err instanceof Error ? err.message : String(err)}`);
    process.exit(2);
  }
}

/**
 * Full contract: menu only when OCCAM_SETUP=ask and stdin is a TTY.
 * @param {{
 *   env?: NodeJS.ProcessEnv;
 *   stdin?: NodeJS.ReadStream;
 *   stdout?: NodeJS.WriteStream;
 *   askQuestion?: (prompt: string) => Promise<string>;
 * }} [opts]
 * @returns {Promise<"auto"|"manual">}
 */
export async function promptSetupMode(opts = {}) {
  const env = opts.env ?? process.env;
  const stdin = opts.stdin ?? input;
  const stdout = opts.stdout ?? output;
  const interactive = stdin.isTTY === true;

  let intent;
  try {
    intent = readSetupIntent(env);
  } catch (err) {
    console.error(`error: ${err instanceof Error ? err.message : String(err)}`);
    process.exit(2);
  }

  if (intent === "auto") {
    return "auto";
  }
  if (intent === "manual") {
    return "manual";
  }

  // ask
  if (!interactive) {
    return "auto";
  }

  if (opts.askQuestion) {
    const raw = await opts.askQuestion(renderSetupMenu());
    return parseSetupChoice(raw);
  }

  const rl = createInterface({ input: stdin, output: stdout });
  try {
    const raw = await rl.question(renderSetupMenu());
    return parseSetupChoice(raw);
  } finally {
    rl.close();
  }
}

async function main() {
  const cmd = process.argv[2] ?? "print";

  if (cmd === "print") {
    printWelcome();
    return;
  }

  if (cmd === "resolve") {
    const mode = resolveSetupFromEnv();
    process.stdout.write(`${mode}\n`);
    return;
  }

  if (cmd === "prompt") {
    printWelcome();
    const mode = await promptSetupMode();
    process.stdout.write(`${mode}\n`);
    return;
  }

  console.error(`usage: node get-install-welcome.mjs print|prompt|resolve`);
  process.exit(2);
}

function isDirectRun() {
  const entry = process.argv[1];
  if (!entry) return false;
  try {
    return fileURLToPath(import.meta.url) === resolve(entry);
  } catch {
    return false;
  }
}

if (isDirectRun()) {
  main().catch((err) => {
    console.error(err.message || String(err));
    process.exit(1);
  });
}