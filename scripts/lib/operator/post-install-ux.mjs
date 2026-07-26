#!/usr/bin/env node
/**
 * Post-extract install UX — one progress flow:
 *   Install → Verify → Connect → Ready
 *
 * Called by get-ff-occam.sh / .ps1 after the release tarball is extracted.
 * Quiet by default; pass --verbose / OCCAM_VERBOSE=1 for doctor/smoke internals.
 *
 *   node scripts/lib/operator/post-install-ux.mjs --setup auto|manual [--version X] [--verbose]
 */
import { spawnSync } from "node:child_process";
import { existsSync, mkdirSync, writeFileSync } from "node:fs";
import { homedir, platform } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { runFlow } from "./onboard-flow.mjs";
import { writeOnboardConfig } from "./onboard-config.mjs";
import { runInstallConnectFlow } from "./install-connect-flow.mjs";
import {
  assertQuietTranscript,
  isInstallQuiet,
  isInstallVerbose,
  okLine,
  renderInstallingHeader,
  renderProductHeader,
  shouldUseInstallColor,
} from "./install-ux.mjs";
import { renderConnectTranscript } from "./connect/render.mjs";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const defaultHome = process.env.OCCAM_HOME?.trim() || join(scriptDir, "..", "..", "..");

/**
 * @param {string[]} argv
 */
function parseArgs(argv) {
  let setup = "auto";
  let version = process.env.OCCAM_VERSION?.trim() || "";
  let skipConnect = false;
  let downloadOk = false;
  /** @type {string[]} */
  const rest = [];
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === "--setup") setup = argv[++i]?.trim() || "auto";
    else if (a === "--version") version = argv[++i]?.trim() || version;
    else if (a === "--skip-connect") skipConnect = true;
    else if (a === "--download-ok") downloadOk = true;
    else if (a === "--verbose" || a === "--debug") rest.push(a);
    else if (a === "-h" || a === "--help") {
      console.log(
        `usage: node post-install-ux.mjs --setup auto|manual [--version X] [--download-ok] [--verbose] [--skip-connect]`,
      );
      process.exit(0);
    }
  }
  if (setup !== "auto" && setup !== "manual") {
    console.error(`error: --setup must be auto|manual (got ${setup})`);
    process.exit(2);
  }
  return { setup, version, skipConnect, downloadOk, argv: [...argv, ...rest] };
}

/**
 * @param {string} command
 * @param {string[]} args
 * @param {{ cwd: string, env: NodeJS.ProcessEnv, quiet: boolean }} opts
 */
function runCaptured(command, args, opts) {
  const r = spawnSync(command, args, {
    cwd: opts.cwd,
    env: opts.env,
    encoding: "utf8",
    shell: false,
  });
  const stdout = r.stdout || "";
  const stderr = r.stderr || "";
  if (!opts.quiet || r.status !== 0) {
    if (stdout) process.stdout.write(stdout.endsWith("\n") ? stdout : `${stdout}\n`);
    if (stderr) process.stderr.write(stderr.endsWith("\n") ? stderr : `${stderr}\n`);
  }
  return r.status ?? 1;
}

/**
 * @param {string} occamHome
 * @param {boolean} quiet
 * @param {NodeJS.ProcessEnv} env
 */
function runDoctor(occamHome, quiet, env) {
  const isWin = platform() === "win32";
  const doctorPs1 = join(occamHome, "scripts", "occam-doctor.ps1");
  const doctorSh = join(occamHome, "scripts", "occam-doctor.sh");
  const childEnv = {
    ...env,
    OCCAM_HOME: occamHome,
    OCCAM_INSTALL_QUIET: quiet ? "1" : "0",
  };
  if (isWin && existsSync(doctorPs1)) {
    return runCaptured(
      "powershell",
      ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", doctorPs1, "-SkipBuild"],
      { cwd: occamHome, env: childEnv, quiet },
    );
  }
  if (existsSync(doctorSh)) {
    return runCaptured("bash", [doctorSh, "--skip-build"], {
      cwd: occamHome,
      env: childEnv,
      quiet,
    });
  }
  console.error("error: occam-doctor script missing");
  return 1;
}

/**
 * Silent onboard.json defaults — known OCCAM_HOME, no host phantom, no snippet dump.
 * @param {string} occamHome
 */
function writeSilentOnboard(occamHome) {
  const result = runFlow({
    occamHome,
    hostTarget: "generic-stdio",
    browser: "bundled",
    proxy: "no",
    profile: "default",
  });
  writeOnboardConfig(result);
  return result;
}

/**
 * Persist connect-last.json (same path as occam-connect.mjs).
 * @param {object} report
 */
function writeConnectLast(report) {
  try {
    const dir = join(homedir(), ".occam");
    mkdirSync(dir, { recursive: true });
    writeFileSync(join(dir, "connect-last.json"), JSON.stringify(report, null, 2), "utf8");
  } catch {
    // best-effort
  }
}

async function main() {
  const opts = parseArgs(process.argv.slice(2));
  const occamHome = process.env.OCCAM_HOME?.trim() || defaultHome;
  const quiet = isInstallQuiet(process.env, process.argv);
  const verbose = isInstallVerbose(process.env, process.argv);
  const interactive = process.stdin.isTTY === true && process.stdout.isTTY === true;

  const outLines = [];
  const emit = (line = "") => {
    outLines.push(line);
    console.log(line);
  };

  // First contact: product identity. Plain text is the contract transcript;
  // ANSI cyan on a capable TTY (same palette as get-install-welcome / host banner).
  const productHeader = renderProductHeader(opts.version);
  outLines.push(productHeader);
  if (shouldUseInstallColor(process.stdout, process.env)) {
    console.log(`\u001b[38;5;45m${productHeader}\u001b[0m`);
  } else {
    console.log(productHeader);
  }
  emit("");
  emit(renderInstallingHeader());
  if (opts.downloadOk) {
    emit(okLine("Download verified"));
  }

  // Stage: Install runtime (doctor = npm workers + browser + selftests)
  const doctorStatus = runDoctor(occamHome, quiet, process.env);
  if (doctorStatus !== 0) {
    console.error("Install failed during runtime setup (doctor). Re-run with --verbose for details.");
    process.exit(doctorStatus);
  }
  emit(okLine("Runtime installed"));
  emit(okLine("Browser ready"));

  // Stage: Verify
  const verifyJs = join(occamHome, "scripts", "lib", "verify-install.mjs");
  const verifyArgs = ["--skip-build"];
  if (quiet) verifyArgs.push("--quiet");
  if (opts.version) verifyArgs.push("--version", opts.version);
  let st = runCaptured(process.execPath, [verifyJs, ...verifyArgs], {
    cwd: occamHome,
    env: { ...process.env, OCCAM_HOME: occamHome, OCCAM_INSTALL_QUIET: quiet ? "1" : "0" },
    quiet,
  });
  if (st !== 0) {
    console.error("Install failed during verify-install. Re-run with --verbose for details.");
    process.exit(st);
  }

  const smokeJs = join(occamHome, "scripts", "hermes-smoke.mjs");
  const smokeArgs = quiet ? ["--quiet"] : [];
  st = runCaptured(process.execPath, [smokeJs, ...smokeArgs], {
    cwd: occamHome,
    env: {
      ...process.env,
      OCCAM_HOME: occamHome,
      OCCAM_BANNER: "0",
      WT_OCCAM_BANNER: "0",
      OCCAM_INSTALL_QUIET: quiet ? "1" : "0",
    },
    quiet,
  });
  if (st !== 0) {
    console.error("Install failed during self-check (smoke). Re-run with --verbose for details.");
    process.exit(st);
  }
  emit(okLine("Self-check passed"));
  emit("");

  // Operator defaults (no second OCCAM_HOME prompt; no MCP snippet here)
  writeSilentOnboard(occamHome);

  if (opts.skipConnect || !existsSync(join(occamHome, "scripts", "occam-connect.mjs"))) {
    emit("Occam is installed.");
    emit("Connect an AI app later with: occam connect");
    emit("");
    emit("Documentation:");
    emit("https://contextforgeai.github.io/occam/");
    if (quiet) assertQuietTranscript(outLines.join("\n"));
    process.exit(0);
  }

  const connectResult = await runInstallConnectFlow({
    occamHome,
    setupMode: opts.setup,
    verbose,
    // Any real TTY gets multi-host safety prompts; single-host auto still connects without asking.
    interactive,
    forceConnect: false,
  });

  if (connectResult.connectReport) {
    writeConnectLast(connectResult.connectReport);
  }

  console.log(connectResult.transcript);
  if (verbose && connectResult.connectReport) {
    console.log("");
    console.log(renderConnectTranscript(connectResult.connectReport));
  }

  if (quiet) {
    try {
      assertQuietTranscript(`${outLines.join("\n")}\n${connectResult.transcript}`);
    } catch (err) {
      // Soft: do not fail a successful install on a copy regression — warn loudly.
      console.error(String(err instanceof Error ? err.message : err));
    }
  }

  // Install itself succeeded even when connect is skip / action-required.
  process.exit(0);
}

main().catch((err) => {
  console.error(err instanceof Error ? err.message : String(err));
  process.exit(1);
});
