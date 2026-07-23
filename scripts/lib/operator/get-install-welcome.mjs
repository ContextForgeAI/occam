#!/usr/bin/env node
/**
 * Welcome + setup mode for get-ff-occam.sh (logger-style banner, auto vs manual).
 *
 *   node get-install-welcome.mjs print
 *   node get-install-welcome.mjs prompt          # TTY → writes auto|manual to stdout
 *   node get-install-welcome.mjs resolve         # non-TTY: OCCAM_SETUP env → stdout
 */
import { createInterface } from "node:readline/promises";
import { stdin as input, stdout as output } from "node:process";
import { GET_INSTALL_WELCOME, SETUP_MODE_COPY } from "./get-install-copy.mjs";
import { horizontalRule, indent, sectionBox } from "./render/tty-layout.mjs";

const WIDTH = 52;

/** @param {boolean} useColor */
function renderProductBanner(useColor) {
  const g = useColor ? "\u001b[38;5;244m" : "";
  const w = useColor ? "\u001b[38;5;255m" : "";
  const c = useColor ? "\u001b[38;5;45m" : "";
  const ok = useColor ? "\u001b[38;5;46m" : "";
  const r = useColor ? "\u001b[0m" : "";

  const lines = [
    "",
    `${c}  ${GET_INSTALL_WELCOME.title}${r}`,
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
  const useColor = process.stdout.isTTY === true;
  process.stdout.write(renderProductBanner(useColor));
}

/**
 * @returns {Promise<"auto"|"manual">}
 */
export async function promptSetupMode() {
  if (!process.stdin.isTTY) {
    return resolveSetupFromEnv();
  }

  const rl = createInterface({ input, output });
  try {
    const raw = await rl.question(renderSetupMenu());
    const choice = raw.trim() || "1";
    if (choice === "2" || /^manual$/i.test(choice)) {
      return "manual";
    }
    return "auto";
  } finally {
    rl.close();
  }
}

/**
 * @returns {"auto"|"manual"}
 */
export function resolveSetupFromEnv() {
  const raw = process.env.OCCAM_SETUP?.trim().toLowerCase();
  if (raw === "manual" || raw === "2") {
    return "manual";
  }
  if (raw === "auto" || raw === "1" || !raw) {
    return "auto";
  }
  console.error(`error: invalid OCCAM_SETUP=${raw} (use auto|manual)`);
  process.exit(2);
}

async function main() {
  const cmd = process.argv[2] ?? "print";

  if (cmd === "print") {
    printWelcome();
    return;
  }

  if (cmd === "prompt") {
    printWelcome();
    const mode = await promptSetupMode();
    process.stdout.write(`${mode}\n`);
    return;
  }

  if (cmd === "resolve") {
    const mode = resolveSetupFromEnv();
    process.stdout.write(`${mode}\n`);
    return;
  }

  console.error(`usage: node get-install-welcome.mjs print|prompt|resolve`);
  process.exit(2);
}

main().catch((err) => {
  console.error(err.message || String(err));
  process.exit(1);
});
