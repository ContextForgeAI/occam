/**
 * Release tarballs ship workers/ without node_modules. get-ff-occam runs doctor
 * to install them; the npm MCP cache path historically skipped that step and
 * then failed extracts with missing @mozilla/readability.
 */
import { spawnSync } from "node:child_process";
import { existsSync } from "node:fs";
import { join } from "node:path";
import { isWorkersInstalled } from "./host-install-gate.mjs";

/**
 * @param {string} home OCCAM_HOME (cache extract or tarball root)
 * @param {{ prefix?: string }} [opts]
 * @returns {{ ok: boolean, skipped?: boolean, error?: string }}
 */
export function ensureWorkersInstalled(home, opts = {}) {
  const prefix = opts.prefix ?? "[ff-occam/mcp]";
  const workersRoot = join(home, "workers");
  if (!existsSync(join(workersRoot, "package.json"))) {
    return { ok: true, skipped: true };
  }
  if (isWorkersInstalled(home)) {
    return { ok: true, skipped: true };
  }

  console.error(`${prefix} Installing worker npm dependencies under ${workersRoot}...`);
  const result = spawnSync(
    process.platform === "win32" ? "npm.cmd" : "npm",
    ["install", "--no-fund", "--no-audit"],
    {
      cwd: workersRoot,
      env: process.env,
      encoding: "utf8",
      stdio: ["ignore", "pipe", "pipe"],
      windowsHide: true,
      shell: process.platform === "win32",
    },
  );

  if (result.status !== 0) {
    const detail = String(result.stderr || result.stdout || "").trim().slice(0, 800);
    return {
      ok: false,
      error:
        `${prefix} worker npm install failed (exit ${result.status ?? "unknown"}).\n` +
        (detail ? `${detail}\n` : "") +
        `${prefix} Or use the guarded installer: curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh | bash`,
    };
  }

  if (!isWorkersInstalled(home)) {
    return {
      ok: false,
      error: `${prefix} worker npm install finished but workers/http-extract/node_modules is still missing.`,
    };
  }

  console.error(`${prefix} Worker dependencies ready.`);
  return { ok: true };
}
