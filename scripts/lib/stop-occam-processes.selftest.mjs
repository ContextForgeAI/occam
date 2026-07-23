#!/usr/bin/env node
/**
 * Self-test: stop-occam-processes resolves the current AssemblyName (OccamMcp.Core)
 * and retains legacy FFOccamMcp.Core candidates.
 */
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { publishExePath } from "./stop-occam-processes.mjs";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDir, "../..");

function testPublishExePathPrefersCurrentName() {
  const rid = process.platform === "win32" ? "win-x64" : process.platform === "darwin" ? "osx-arm64" : "linux-x64";
  const publishDir = path.join(
    repoRoot,
    "src",
    "FFOccamMcp.Core",
    "bin",
    "Release",
    "net10.0",
    rid,
    "publish",
  );
  const current = path.join(publishDir, process.platform === "win32" ? "OccamMcp.Core.exe" : "OccamMcp.Core");
  const legacy = path.join(publishDir, process.platform === "win32" ? "FFOccamMcp.Core.exe" : "FFOccamMcp.Core");

  if (fs.existsSync(current)) {
    assert.equal(publishExePath(repoRoot), current);
    console.log("ok: publishExePath prefers OccamMcp.Core when present");
    return;
  }

  // Synthetic layout under a temp OCCAM_HOME-shaped tree.
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), "occam-stop-selftest-"));
  try {
    const synthPublish = path.join(tmp, "src", "FFOccamMcp.Core", "bin", "Release", "net10.0", rid, "publish");
    fs.mkdirSync(synthPublish, { recursive: true });
    const synthCurrent = path.join(synthPublish, path.basename(current));
    fs.writeFileSync(synthCurrent, "placeholder");
    // resolveRid still uses process.platform; path under tmp must match.
    // Monkey-patch by running against tmp only works if resolveRid matches rid — it does.
    assert.equal(publishExePath(tmp), synthCurrent);
    console.log("ok: publishExePath resolves synthetic OccamMcp.Core");

    fs.unlinkSync(synthCurrent);
    const synthLegacy = path.join(synthPublish, path.basename(legacy));
    fs.writeFileSync(synthLegacy, "legacy");
    assert.equal(publishExePath(tmp), synthLegacy);
    console.log("ok: publishExePath falls back to legacy FFOccamMcp.Core");
  } finally {
    fs.rmSync(tmp, { recursive: true, force: true });
  }
}

testPublishExePathPrefersCurrentName();
console.log("stop-occam-processes.selftest: OK");
