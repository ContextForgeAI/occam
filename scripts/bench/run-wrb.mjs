#!/usr/bin/env node
/**
 * Run the external WRB benchmark at a pinned revision.
 *
 * The WRB checkout and result JSON stay under artifacts/ (gitignored). The
 * default runner is Occam; --runner=donsetch runs WRB's native comparison arm.
 */
import { execFileSync, spawnSync } from "node:child_process";
import {
  copyFileSync,
  existsSync,
  mkdirSync,
  readFileSync,
  writeFileSync,
} from "node:fs";
import { dirname, isAbsolute, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const WRB_REPO = "https://github.com/dondai44423/wrb.git";
const DEFAULT_WRB_REF = "52025c304f6cdd242eb6d3fef2f0cb3700838fbd";
const scriptDir = dirname(fileURLToPath(import.meta.url));
const root = resolve(process.env.OCCAM_HOME?.trim() || join(scriptDir, "..", ".."));

const custom = {
  prepareOnly: false,
  runner: "occam",
  wrbRef: process.env.OCCAM_WRB_REF?.trim() || DEFAULT_WRB_REF,
};
const forwarded = [];
for (const arg of process.argv.slice(2)) {
  if (arg === "--prepare-only") {
    custom.prepareOnly = true;
  } else if (arg.startsWith("--runner=")) {
    custom.runner = arg.slice("--runner=".length);
  } else if (arg.startsWith("--wrb-ref=")) {
    custom.wrbRef = arg.slice("--wrb-ref=".length);
  } else {
    forwarded.push(arg);
  }
}

if (!["occam", "donsetch"].includes(custom.runner)) {
  console.error("error: --runner must be occam or donsetch");
  process.exit(2);
}
if (!/^[0-9a-f]{40}$/i.test(custom.wrbRef)) {
  console.error("error: --wrb-ref must be a full 40-character commit SHA");
  process.exit(2);
}

const shortRef = custom.wrbRef.slice(0, 12);
const wrbRoot = join(root, "artifacts", "wrb", shortRef, "repo");
const outputDir = join(root, "artifacts", "wrb", shortRef, "results");
const adapterSource = join(root, "scripts", "bench", "wrb", "occam.py");
const adapterTarget = join(wrbRoot, "runners", "occam.py");

function run(command, args, opts = {}) {
  try {
    return execFileSync(command, args, {
      cwd: opts.cwd ?? root,
      encoding: "utf8",
      stdio: opts.stdio ?? ["ignore", "pipe", "inherit"],
      env: { ...process.env, ...(opts.env ?? {}) },
    });
  } catch (error) {
    const status = Number.isInteger(error?.status) ? error.status : 1;
    process.exit(status);
  }
}

mkdirSync(dirname(wrbRoot), { recursive: true });
if (!existsSync(join(wrbRoot, ".git"))) {
  run("git", ["clone", "--filter=blob:none", WRB_REPO, wrbRoot], {
    stdio: "inherit",
  });
}

let hasRevision = true;
try {
  execFileSync("git", ["cat-file", "-e", `${custom.wrbRef}^{commit}`], {
    cwd: wrbRoot,
    stdio: "ignore",
  });
} catch {
  hasRevision = false;
}
if (!hasRevision) {
  run("git", ["fetch", "origin", custom.wrbRef], {
    cwd: wrbRoot,
    stdio: "inherit",
  });
}
run("git", ["checkout", "--detach", custom.wrbRef], {
  cwd: wrbRoot,
  stdio: "ignore",
});

copyFileSync(adapterSource, adapterTarget);
mkdirSync(outputDir, { recursive: true });

const occamRevision = run("git", ["rev-parse", "HEAD"]).trim();
console.error(`WRB revision:   ${custom.wrbRef}`);
console.error(`Occam revision: ${occamRevision}`);
console.error(`Runner:         ${custom.runner}`);
if (custom.runner === "occam") {
  console.error("Crawl mapping:  occam_map URL-discovery proxy (not resumable content crawl)");
  console.error(
    `Search provider: ${process.env.OCCAM_SEARCH_PROVIDER || "unconfigured (expected honest failure)"}`,
  );
}

if (custom.prepareOnly) {
  console.error(`Prepared: ${wrbRoot}`);
  process.exit(0);
}

let requestedOutput = null;
for (let index = 0; index < forwarded.length; index++) {
  const arg = forwarded[index];
  if (arg === "--output" || arg === "-o") {
    requestedOutput = forwarded[index + 1] ?? null;
    break;
  }
  if (arg.startsWith("--output=")) {
    requestedOutput = arg.slice("--output=".length);
    break;
  }
}
const outputPath = requestedOutput
  ? (isAbsolute(requestedOutput) ? requestedOutput : resolve(wrbRoot, requestedOutput))
  : join(outputDir, `${custom.runner}.json`);
const runnerArgs = [
  join(wrbRoot, "lib", "wrb.py"),
  custom.runner,
  "--tool-name",
  custom.runner === "occam" ? "FF-Occam" : "DonSeTch",
  ...forwarded,
];
if (!requestedOutput) {
  runnerArgs.push("--output", outputPath);
}

const python = process.env.PYTHON
  || (process.platform === "win32" ? "python" : "python3");
const completed = spawnSync(python, runnerArgs, {
  cwd: wrbRoot,
  env: {
    ...process.env,
    OCCAM_HOME: root,
  },
  stdio: "inherit",
});
if (completed.error) {
  console.error(`error: failed to start ${python}: ${completed.error.message}`);
  process.exit(1);
}
if (completed.status !== 0) {
  process.exit(completed.status ?? 1);
}

try {
  const result = JSON.parse(readFileSync(outputPath, "utf8"));
  result.provenance = {
    wrbRevision: custom.wrbRef,
    occamRevision,
    runner: custom.runner,
    searchProvider: custom.runner === "occam"
      ? (process.env.OCCAM_SEARCH_PROVIDER || null)
      : null,
    crawlMapping: custom.runner === "occam" ? "occam_map_proxy" : "native",
  };
  writeFileSync(outputPath, `${JSON.stringify(result, null, 2)}\n`, "utf8");
  console.error(`Provenance recorded: ${outputPath}`);
} catch (error) {
  console.error(`error: could not stamp WRB result provenance: ${error.message}`);
  process.exit(1);
}
