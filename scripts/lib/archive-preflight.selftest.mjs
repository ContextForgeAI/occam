#!/usr/bin/env node
/**
 * Adversarial selftests for release archive member preflight.
 */
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { pathToFileURL } from "node:url";
import {
  classifyUnsafeArchivePath,
  classifyUnsafeSymlinkTarget,
  listTarGzMembers,
  preflightTarGzArchive,
  validateArchiveMembers,
} from "./archive-preflight.mjs";
import { writeTarGzArchive } from "./archive-preflight-fixtures.mjs";

function withTempArchive(entries, fn) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "occam-archive-preflight-"));
  try {
    const archivePath = path.join(root, "fixture.tar.gz");
    writeTarGzArchive(entries, archivePath);
    return fn(archivePath);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
}

function testPathClassifiers() {
  assert.equal(classifyUnsafeArchivePath("../etc/passwd"), "path traversal in archive member: ../etc/passwd");
  assert.equal(
    classifyUnsafeArchivePath("ff-occam/../escape"),
    "path traversal in archive member: ff-occam/../escape",
  );
  assert.match(classifyUnsafeArchivePath("/etc/passwd") || "", /absolute/);
  assert.match(classifyUnsafeArchivePath("C:/Windows/system32") || "", /windows drive/);
  assert.match(classifyUnsafeArchivePath("C:\\Windows\\system32") || "", /windows drive/);
  assert.match(classifyUnsafeArchivePath("//server/share/file") || "", /unc|absolute/);
  assert.equal(classifyUnsafeArchivePath("ff-occam-1.0.0-rc.3-win-x64/VERSION"), null);

  assert.match(
    classifyUnsafeSymlinkTarget("ff-root/link", "../outside") || "",
    /escapes archive root|unsafe/,
  );
  assert.match(
    classifyUnsafeSymlinkTarget("ff-root/link", "/tmp/x") || "",
    /unsafe|absolute/,
  );
  assert.equal(classifyUnsafeSymlinkTarget("ff-root/bin/link", "../VERSION"), null);
}

function testSafeArchive() {
  withTempArchive(
    [
      { name: "ff-root/", type: "directory" },
      { name: "ff-root/VERSION", content: "1.0.0-rc.3\n" },
      { name: "ff-root/nested/file.txt", content: "ok\n" },
    ],
    (archivePath) => {
      const result = preflightTarGzArchive({ archivePath, expectedRoot: "ff-root" });
      assert.equal(result.problems.length, 0);
      assert.ok(result.members.length >= 2);
    },
  );
}

function assertRejects(entries, expectedRoot, pattern) {
  withTempArchive(entries, (archivePath) => {
    const result = preflightTarGzArchive({ archivePath, expectedRoot });
    assert.ok(result.problems.length > 0, `expected rejection matching ${pattern}`);
    assert.match(result.problems.join("\n"), pattern);
  });
}

function testAdversarialMembers() {
  assertRejects([{ name: "../escape.txt", content: "x" }], "ff-root", /path traversal/);
  assertRejects(
    [{ name: "ff-root/../../etc/passwd", content: "x" }],
    "ff-root",
    /path traversal/,
  );
  assertRejects([{ name: "/etc/passwd", content: "x" }], "ff-root", /absolute/);
  assertRejects(
    [{ name: "C:/Windows/notepad.exe", content: "x" }],
    "ff-root",
    /windows drive/,
  );
  assertRejects(
    [{ name: "//server/share/x", content: "x" }],
    "ff-root",
    /unc|absolute/,
  );
  assertRejects(
    [
      { name: "ff-root/", type: "directory" },
      { name: "ff-root/link", type: "symlink", linkname: "../../outside" },
    ],
    "ff-root",
    /symlink|escapes|unsafe/,
  );
  assertRejects(
    [
      { name: "ff-root/", type: "directory" },
      { name: "ff-root/link", type: "symlink", linkname: "../sibling-escape" },
      { name: "ff-root/child-via-link/file.txt", content: "x" },
    ],
    "ff-root",
    /symlink|escapes|unsafe/,
  );
  assertRejects(
    [
      { name: "ff-root/VERSION", content: "a\n" },
      { name: "ff-root/VERSION", content: "b\n" },
    ],
    "ff-root",
    /duplicate|conflict/,
  );
  assertRejects(
    [{ name: "other-root/VERSION", content: "x\n" }],
    "ff-root",
    /missing expected archive root|unexpected archive root/,
  );
  assertRejects(
    [
      { name: "ff-root/VERSION", content: "x\n" },
      { name: "extra-root/file", content: "y\n" },
    ],
    "ff-root",
    /unexpected archive root/,
  );
}

function testValidateMembersApi() {
  const problems = validateArchiveMembers(
    [{ name: "ff-root/a", type: "file", linkname: "", size: 1, mode: 0o644 }],
    { expectedRoot: "ff-root" },
  );
  assert.equal(problems.length, 0);
}

function testListRoundTrip() {
  withTempArchive(
    [
      { name: "ff-root/VERSION", content: "1\n" },
      { name: "ff-root/readme.txt", content: "x\n" },
    ],
    (archivePath) => {
      const members = listTarGzMembers(archivePath);
      assert.ok(members.some((m) => m.name === "ff-root/VERSION" && m.type === "file"));
      assert.ok(members.some((m) => m.name === "ff-root/readme.txt" && m.type === "file"));
    },
  );
}

function main() {
  testPathClassifiers();
  testSafeArchive();
  testAdversarialMembers();
  testValidateMembersApi();
  testListRoundTrip();
  console.log("archive-preflight.selftest: OK");
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main();
}
