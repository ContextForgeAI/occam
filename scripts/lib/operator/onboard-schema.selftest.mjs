import assert from "node:assert/strict";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import {
  SUPPORTED_ONBOARD_SCHEMA,
  buildOnboardFilePayload,
  loadOnboardConfig,
  parseSchemaVersion,
} from "./onboard-schema.mjs";
import { buildOnboardResult, buildMcpSnippetConfig } from "./onboard-flow.mjs";
import { writeOnboardConfig } from "./onboard-config.mjs";

function testParseSchemaVersion() {
  assert.deepEqual(parseSchemaVersion("1.0"), { major: 1, minor: 0 });
  assert.deepEqual(parseSchemaVersion("2.1"), { major: 2, minor: 1 });
  assert.deepEqual(parseSchemaVersion(1), { major: 1, minor: 0 });
  assert.equal(parseSchemaVersion("bad"), null);
}

function testRoundTrip() {
  const dir = mkdtempSync(join(tmpdir(), "occam-onboard-"));
  const path = join(dir, "onboard.json");

  const result = buildOnboardResult({
    occamHome: "/tmp/ff-occam",
    hostTarget: "hermes",
    browser: "bundled",
    useProxy: false,
    profile: "hermes-headless",
  });

  writeOnboardConfig({ ...result, configPath: path });
  const loaded = loadOnboardConfig(path);

  assert.equal(loaded.ignored, false);
  assert.equal(loaded.warnings.length, 0);
  assert.equal(loaded.env.OCCAM_HOME, "/tmp/ff-occam");
  assert.equal(loaded.env.OCCAM_BANNER, "0");

  const written = JSON.parse(readFileSync(path, "utf8"));
  assert.equal(written.schema_version, SUPPORTED_ONBOARD_SCHEMA);
  assert.equal(typeof written.generator, "string");
  assert.equal(typeof written.written_at, "string");
  assert.equal(written.version, undefined);

  rmSync(dir, { recursive: true, force: true });
}

function testMajorMismatchIgnored() {
  const dir = mkdtempSync(join(tmpdir(), "occam-onboard-"));
  const path = join(dir, "onboard.json");
  writeFileSync(
    path,
    JSON.stringify({
      schema_version: "2.0",
      env: { OCCAM_HOME: "/should-not-load" },
    }),
    "utf8",
  );

  const loaded = loadOnboardConfig(path);
  assert.equal(loaded.ignored, true);
  assert.ok(loaded.warnings.includes("onboard_schema_unsupported"));
  assert.deepEqual(loaded.env, {});

  rmSync(dir, { recursive: true, force: true });
}

function testNewerMinorWarnsAndApplies() {
  const dir = mkdtempSync(join(tmpdir(), "occam-onboard-"));
  const path = join(dir, "onboard.json");
  writeFileSync(
    path,
    JSON.stringify({
      schema_version: "1.9",
      env: { OCCAM_HOME: "/known-field", FUTURE_FIELD: "x" },
    }),
    "utf8",
  );

  const loaded = loadOnboardConfig(path);
  assert.equal(loaded.ignored, false);
  assert.ok(loaded.warnings.includes("onboard_schema_newer"));
  assert.equal(loaded.env.OCCAM_HOME, "/known-field");
  assert.equal(loaded.env.FUTURE_FIELD, "x");

  rmSync(dir, { recursive: true, force: true });
}

function testLegacyIntegerVersion() {
  const dir = mkdtempSync(join(tmpdir(), "occam-onboard-"));
  const path = join(dir, "onboard.json");
  writeFileSync(
    path,
    JSON.stringify({
      version: 1,
      env: { OCCAM_HOME: "/legacy" },
    }),
    "utf8",
  );

  const loaded = loadOnboardConfig(path);
  assert.equal(loaded.ignored, false);
  assert.equal(loaded.env.OCCAM_HOME, "/legacy");

  rmSync(dir, { recursive: true, force: true });
}

function testBuildPayloadShape() {
  const result = buildOnboardResult({
    occamHome: "C:/occam",
    hostTarget: "cursor",
    browser: "bundled",
    useProxy: false,
    profile: "default",
  });
  const payload = buildOnboardFilePayload(result);
  assert.equal(payload.schema_version, "1.0");
  assert.equal(payload.profile, "default");
  assert.ok(payload.generator);
  assert.ok(payload.written_at);
}

function testHermesSnippetUsesWrapper() {
  const result = buildOnboardResult({
    occamHome: "/opt/ff-occam",
    hostTarget: "hermes",
    browser: "bundled",
    useProxy: false,
    profile: "hermes-headless",
  });
  const cfg = buildMcpSnippetConfig(result);
  const entry = cfg.mcpServers["ff-occam"];
  assert.ok(entry.command.includes("occam-wrapper.sh"));
  assert.equal(entry.env.OCCAM_HOME, "/opt/ff-occam");
}

function main() {
  testParseSchemaVersion();
  testRoundTrip();
  testMajorMismatchIgnored();
  testNewerMinorWarnsAndApplies();
  testLegacyIntegerVersion();
  testBuildPayloadShape();
  testHermesSnippetUsesWrapper();
  console.log("onboard-schema.selftest.mjs OK");
}
