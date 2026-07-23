import { createInterface } from "node:readline/promises";
import { stdin as input, stdout as output } from "node:process";
import { runControlAction, spawnOccamHelpCatalog, showStatus } from "./control-actions.mjs";
import {
  renderActionResult,
  renderControlHeader,
  renderControlMenu,
} from "./render/control-tty-renderer.mjs";
import { readInstalledVersion } from "./update-check.mjs";

/** @type {Record<string, 'doctor'|'onboard'|'help'|'refresh'|'smoke'|'update'|'status'>} */
const MENU_KEYS = {
  "1": "onboard",
  "2": "doctor",
  "3": "update",
  "4": "help",
  "5": "refresh",
  "6": "smoke",
  s: "status",
};

/**
 * @param {string} occamHome
 * @param {{ json?: boolean }} [opts]
 */
export async function runControlLoop(occamHome, opts = {}) {
  if (opts.json) {
    const status = await showStatus(occamHome);
    console.log(JSON.stringify(status.data ?? status, null, 2));
    return status.ok ? 0 : 1;
  }

  if (!process.stdin.isTTY) {
    console.error("error: occam control requires an interactive TTY (or use: occam status --json)");
    return 1;
  }

  const version = readInstalledVersion(occamHome);
  const rl = createInterface({ input, output });

  try {
    let running = true;
    while (running) {
      console.log(renderControlHeader(occamHome, version));
      console.log(renderControlMenu());
      const raw = (await rl.question("Choice: ")).trim().toLowerCase();

      if (!raw || raw === "q" || raw === "quit" || raw === "exit") {
        running = false;
        continue;
      }

      if (raw === "h") {
        spawnOccamHelpCatalog(occamHome);
        await rl.question("\nPress Enter to return to menu...");
        continue;
      }

      const action = MENU_KEYS[raw];
      if (!action) {
        console.log(renderActionResult({ ok: false, message: `Unknown choice: ${raw}` }));
        await rl.question("\nPress Enter to continue...");
        continue;
      }

      const result = await runControlAction(action, occamHome);
      console.log(renderActionResult(result));
      await rl.question("\nPress Enter to continue...");
    }

    return 0;
  } finally {
    rl.close();
  }
}
