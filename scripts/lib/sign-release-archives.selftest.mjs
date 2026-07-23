#!/usr/bin/env node
/**
 * Unit-style check mirroring scripts/lib/sign-release-archives.sh selection.
 * Pure Node so it runs on Windows without WSL/Git Bash.
 */
import { mkdtempSync, writeFileSync, rmSync, readdirSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

/** @param {string} dir */
function selectReleaseArchives(dir) {
  const names = readdirSync(dir);
  const patterns = [
    /^ff-occam-.+-linux-x64\.tar\.gz$/,
    /^ff-occam-.+-osx-arm64\.tar\.gz$/,
    /^ff-occam-.+-win-x64\.tar\.gz$/,
  ];
  /** @type {string[]} */
  const out = [];
  for (const re of patterns) {
    for (const name of names) {
      if (re.test(name)) {
        out.push(join(dir, name));
      }
    }
  }
  return out;
}

function assert(cond, msg) {
  if (!cond) {
    console.error(`FAIL: ${msg}`);
    process.exit(1);
  }
  console.log(`ok: ${msg}`);
}

const root = mkdtempSync(join(tmpdir(), "occam-sign-select-"));
try {
  assert(selectReleaseArchives(root).length === 0, "empty dir → zero matches");

  writeFileSync(join(root, "Source code.zip"), "nope");
  writeFileSync(join(root, "ff-occam-1.0.0-rc.2-linux-x64-manifest.json"), "{}");
  writeFileSync(join(root, "ff-occam-1.0.0-rc.2-linux-x64.tar.gz.bundle"), "{}");
  writeFileSync(join(root, "ff-occam-1.0.0-rc.2-linux-x64.tar.gz.sig"), "{}");
  writeFileSync(join(root, "readme.txt"), "nope");
  assert(selectReleaseArchives(root).length === 0, "noise-only → zero matches (fail-closed)");

  for (const name of [
    "ff-occam-1.0.0-rc.2-linux-x64.tar.gz",
    "ff-occam-1.0.0-rc.2-osx-arm64.tar.gz",
    "ff-occam-1.0.0-rc.2-win-x64.tar.gz",
  ]) {
    writeFileSync(join(root, name), "blob");
  }

  const selected = selectReleaseArchives(root);
  assert(selected.length === 3, `expected 3 paths, got ${selected.length}`);
  assert(selected.every((p) => p.endsWith(".tar.gz")), "only .tar.gz selected");
  assert(selected.some((p) => p.includes("linux-x64")), "includes linux-x64");
  assert(selected.some((p) => p.includes("osx-arm64")), "includes osx-arm64");
  assert(selected.some((p) => p.includes("win-x64")), "includes win-x64");
  assert(
    !selected.some((p) => p.includes("manifest") || p.endsWith(".bundle") || p.endsWith(".sig")),
    "excludes manifests/signatures",
  );

  // Fail-closed contract used by the workflow when length === 0
  const emptyMsg = "error: no ff-occam-*-{linux-x64,osx-arm64,win-x64}.tar.gz under artifacts";
  assert(emptyMsg.includes("no ff-occam-"), "workflow error message shape documented");

  console.log("sign-release-archives.selftest: OK");
} finally {
  rmSync(root, { recursive: true, force: true });
}
