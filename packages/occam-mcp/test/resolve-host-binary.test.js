import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { mkdirSync, writeFileSync, rmSync } from "node:fs";
import { dirname, join } from "node:path";
import { tmpdir } from "node:os";
import {
  listHostBinaryCandidates,
  resolveHostBinary,
  resolveRid,
} from "../lib/resolve-host-binary.mjs";

describe("resolve-host-binary", () => {
  it("finds OccamMcp.Core at repo root", () => {
    const root = join(tmpdir(), `occam-bin-root-${Date.now()}`);
    const binary = join(root, process.platform === "win32" ? "OccamMcp.Core.exe" : "OccamMcp.Core");
    mkdirSync(root, { recursive: true });
    writeFileSync(binary, "");
    try {
      assert.equal(resolveHostBinary(root, "linux-x64"), binary);
    } finally {
      rmSync(root, { recursive: true, force: true });
    }
  });

  it("finds legacy FFOccamMcp.Core at repo root", () => {
    const root = join(tmpdir(), `occam-legacy-root-${Date.now()}`);
    const binary = join(
      root,
      process.platform === "win32" ? "FFOccamMcp.Core.exe" : "FFOccamMcp.Core",
    );
    mkdirSync(root, { recursive: true });
    writeFileSync(binary, "");
    try {
      assert.equal(resolveHostBinary(root, "linux-x64"), binary);
    } finally {
      rmSync(root, { recursive: true, force: true });
    }
  });

  it("lists publish path candidates when rid is set", () => {
    const root = join(tmpdir(), "ff-occam-candidates");
    const candidates = listHostBinaryCandidates(root, "linux-x64");
    assert.ok(candidates.length >= 4);
    assert.ok(candidates.some((p) => p.includes("OccamMcp.Core")));
    assert.ok(
      candidates.some((p) => p.includes("linux-x64") && p.includes("publish")),
    );
  });

  it("resolves all supported local runtime identifiers", () => {
    assert.equal(resolveRid("win32", "x64"), "win-x64");
    assert.equal(resolveRid("win32", "arm64"), "win-arm64");
    assert.equal(resolveRid("linux", "x64"), "linux-x64");
    assert.equal(resolveRid("linux", "arm64"), "linux-arm64");
    assert.equal(resolveRid("darwin", "x64"), "osx-x64");
    assert.equal(resolveRid("darwin", "arm64"), "osx-arm64");
  });
});
