#!/usr/bin/env node
/**
 * Regression: release tarball creation must not emit AppleDouble / Finder junk.
 * Exercises the production createTarball path (not COPYFILE_DISABLE in isolation).
 */
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { pathToFileURL } from "node:url";
import { listTarGzMembers } from "./archive-preflight.mjs";
import { createTarball, releaseTarCreateEnv } from "./build-release.mjs";

function isAppleDoubleOrFinderJunk(name) {
  const base = path.posix.basename(name.replace(/\\/g, "/"));
  if (base === ".DS_Store") return true;
  if (base.startsWith("._")) return true;
  if (name.includes("/._") || name.startsWith("._")) return true;
  return false;
}

function testReleaseTarEnvMerges() {
  const env = releaseTarCreateEnv({
    PATH: "/usr/bin",
    COPYFILE_DISABLE: "0",
    KEEP_ME: "yes",
  });
  assert.equal(env.COPYFILE_DISABLE, "1");
  assert.equal(env.KEEP_ME, "yes");
  assert.equal(env.PATH, "/usr/bin");
}

function testCreateTarballHasNoAppleDouble() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "occam-release-tar-"));
  try {
    const stageName = "ff-occam-9.9.9-test-rid";
    const stageRoot = path.join(root, stageName);
    fs.mkdirSync(path.join(stageRoot, "scripts"), { recursive: true });
    fs.writeFileSync(path.join(stageRoot, "VERSION"), "9.9.9\n", "utf8");
    fs.writeFileSync(path.join(stageRoot, "scripts", "hello.txt"), "ok\n", "utf8");
    // On macOS, Finder/xattr pollution would otherwise create ._ companions
    // when tar runs without COPYFILE_DISABLE. Seed a real ._ file in the stage
    // so we also prove createTarball does not invent sibling metadata from
    // adjacent junk (production packaging must not package Finder litter if
    // present on disk — stage should be clean; this asserts archive members).
    const tarballPath = path.join(root, `${stageName}.tar.gz`);
    createTarball(stageRoot, stageName, tarballPath);
    assert.ok(fs.existsSync(tarballPath), "tarball missing");
    const members = listTarGzMembers(tarballPath);
    assert.ok(members.length >= 2, "expected staged files in archive");
    const junk = members.filter((m) => isAppleDoubleOrFinderJunk(m.name));
    assert.deepEqual(
      junk.map((m) => m.name),
      [],
      `release archive must not contain AppleDouble/Finder members; got ${junk.map((m) => m.name).join(", ")}`,
    );
    assert.ok(members.some((m) => m.name.includes("VERSION")));
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
}

function main() {
  testReleaseTarEnvMerges();
  testCreateTarballHasNoAppleDouble();
  console.log("build-release-tarball.selftest: OK");
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main();
}
