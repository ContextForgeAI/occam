#!/usr/bin/env node
/**
 * ff-occam npm package smoke + clean-install from packed tarballs (no registry).
 *
 * Usage:
 *   node scripts/lib/ff-occam-package.selftest.mjs
 */
import { spawnSync } from "node:child_process";
import {
  existsSync,
  mkdtempSync,
  mkdirSync,
  rmSync,
  readFileSync,
  readdirSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = process.env.OCCAM_HOME || join(dirname(fileURLToPath(import.meta.url)), "..", "..");
const pkgDir = join(repoRoot, "packages", "ff-occam");
const mcpDir = join(repoRoot, "packages", "occam-mcp");

function run(cmd, args, opts = {}) {
  const r = spawnSync(cmd, args, {
    encoding: "utf8",
    shell: true,
    ...opts,
  });
  if (r.status !== 0) {
    console.error(r.stdout);
    console.error(r.stderr);
    process.exit(r.status ?? 1);
  }
  return r;
}

if (!existsSync(join(pkgDir, "package.json"))) {
  console.error("ff-occam package missing");
  process.exit(1);
}
if (!existsSync(join(mcpDir, "package.json"))) {
  console.error("@ff-occam/mcp package missing");
  process.exit(1);
}

// Workspace install + unit tests (file: dependency).
run("npm", ["install", "--no-audit", "--no-fund"], { cwd: pkgDir });
run("npm", ["test"], { cwd: pkgDir });

// Clean install from packed tarballs (mimics published consumers).
const stage = mkdtempSync(join(tmpdir(), "ff-occam-clean-"));
try {
  run("npm", ["pack", "--pack-destination", stage], { cwd: mcpDir });
  run("npm", ["pack", "--pack-destination", stage], { cwd: pkgDir });

  const tarballs = readdirSync(stage).filter((f) => f.endsWith(".tgz"));
  // npm pack names: @ff-occam/mcp → ff-occam-mcp-*.tgz ; ff-occam → ff-occam-*.tgz
  const mcpPack = tarballs.find((f) => /^ff-occam-mcp-.*\.tgz$/.test(f));
  const ffPack = tarballs.find((f) => /^ff-occam-(?!mcp-).*\.tgz$/.test(f));
  if (!mcpPack || !ffPack) {
    console.error(`missing packed tarballs in ${stage}: ${tarballs.join(", ")}`);
    process.exit(1);
  }

  const cleanRoot = join(stage, "consumer");
  mkdirSync(cleanRoot, { recursive: true });
  const toFileUrl = (p) => `file:${p.replace(/\\/g, "/")}`;
  writeFileSync(
    join(cleanRoot, "package.json"),
    JSON.stringify(
      {
        name: "ff-occam-clean-consumer",
        private: true,
        type: "module",
        dependencies: {
          "@ff-occam/mcp": toFileUrl(join(stage, mcpPack)),
          "ff-occam": toFileUrl(join(stage, ffPack)),
        },
      },
      null,
      2,
    ),
  );
  run("npm", ["install", "--no-audit", "--no-fund"], { cwd: cleanRoot });

  const binJs = join(cleanRoot, "node_modules", "ff-occam", "bin", "ff-occam.js");
  if (!existsSync(binJs)) {
    console.error(`clean-install missing bin: ${binJs}`);
    process.exit(1);
  }
  const mcpPkg = JSON.parse(
    readFileSync(join(cleanRoot, "node_modules", "@ff-occam", "mcp", "package.json"), "utf8"),
  );
  if (mcpPkg.name !== "@ff-occam/mcp") {
    console.error(`clean-install wrong mcp package: ${mcpPkg.name}`);
    process.exit(1);
  }

  const help = spawnSync(process.execPath, [binJs, "--help"], {
    cwd: cleanRoot,
    encoding: "utf8",
    env: { ...process.env, OCCAM_BANNER: "0" },
  });
  const out = `${help.stdout ?? ""}${help.stderr ?? ""}`;
  if (!out || out.length < 8) {
    console.error("clean-install: ff-occam bin produced no output");
    console.error(help.stdout);
    console.error(help.stderr);
    process.exit(1);
  }
  console.log(`ok: clean-install from ${ffPack} + ${mcpPack}`);
} finally {
  try {
    rmSync(stage, { recursive: true, force: true });
  } catch {
    /* best-effort */
  }
}

console.log("FF_OCCAM_PACKAGE_SELFTEST_OK");
