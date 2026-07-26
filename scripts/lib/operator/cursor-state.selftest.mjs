#!/usr/bin/env node
/**
 * Cursor connect state model — no perpetual restart loop when config is already correct.
 */
import assert from "node:assert/strict";
import { mkdtempSync, rmSync } from "node:fs";
import { tmpdir, homedir } from "node:os";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import {
  createCursorAdapter,
  cursorUserConfigPath,
  evaluateReadyState,
  aggregateConnectionReady,
  VERIFICATION_LEVELS,
  OCCAM_MCP_SERVER_NAME,
  OCCAM_MANAGED_ENV_KEY,
  OCCAM_MANAGED_MARKER,
  buildStableLaunchSpec,
  stdioFromSpec,
  renderHumanConnectSummary,
  mcpEntriesEqual,
} from "./connect/index.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(here, "..", "..", "..");

function desiredEntry(occamHome = repoRoot) {
  const spec = buildStableLaunchSpec(occamHome);
  return stdioFromSpec(spec, { preferWrapper: false, includeCwd: true });
}

function testEvaluateConfiguredVsRestart() {
  const configured = evaluateReadyState({
    occamLevel: VERIFICATION_LEVELS.TOOLS_LIST_OK,
    hostLevel: VERIFICATION_LEVELS.CONFIG_VALID,
    maxHostLevel: VERIFICATION_LEVELS.CONFIG_VALID,
    requiresRestart: false,
    configured: true,
  });
  assert.equal(configured.status, "Configured");
  assert.equal(configured.ready, false);
  assert.equal(configured.requiresRestart, false);

  const restart = evaluateReadyState({
    occamLevel: VERIFICATION_LEVELS.TOOLS_LIST_OK,
    hostLevel: VERIFICATION_LEVELS.CONFIG_VALID,
    maxHostLevel: VERIFICATION_LEVELS.CONFIG_VALID,
    requiresRestart: true,
    configured: true,
  });
  assert.match(restart.status, /restart required/i);
  assert.equal(restart.requiresRestart, true);

  const ready = evaluateReadyState({
    occamLevel: VERIFICATION_LEVELS.TOOLS_LIST_OK,
    hostLevel: VERIFICATION_LEVELS.HOST_DISCOVERS,
    maxHostLevel: VERIFICATION_LEVELS.HOST_DISCOVERS,
    requiresRestart: false,
  });
  assert.equal(ready.ready, true);
  assert.equal(ready.status, "Ready");

  // Config-only must NOT fake Ready when restart flag is absent.
  assert.notEqual(configured.status, "Ready");
}

function testAggregateSecondConnect() {
  const row = {
    name: "Cursor",
    apply: { ok: true, applied: false, action: "noop" },
    hostVerify: { ok: true, configured: true, requiresRestart: false },
    readyState: {
      ready: false,
      status: "Configured",
      configured: true,
      requiresRestart: false,
    },
  };
  const agg = aggregateConnectionReady([row]);
  assert.equal(agg.status, "Configured");
  assert.equal(agg.ready, false);
  assert.doesNotMatch(agg.message || "", /Restart Cursor/i);

  const human = renderHumanConnectSummary({
    status: "Configured",
    message: agg.message,
    connections: [row],
    occamVerify: { ok: true, skipped: true },
  });
  assert.match(human, /Configured:/);
  assert.match(human, /✓ Cursor/);
  assert.match(human, /No further `occam connect` run is required/);
  assert.doesNotMatch(human, /Needs restart:/);
  assert.doesNotMatch(human, /Run `occam connect` again/);
  assert.match(human, /Configured\.\s*$/m);
}

function testAggregateRestartAfterMutation() {
  const row = {
    name: "Cursor",
    apply: { ok: true, applied: true, action: "add", requiresRestart: true },
    hostVerify: { ok: true, configured: true, requiresRestart: false },
    readyState: {
      ready: false,
      status: "Configured — restart required",
      configured: true,
      requiresRestart: true,
    },
  };
  const agg = aggregateConnectionReady([row]);
  assert.equal(agg.status, "Almost ready");
  const human = renderHumanConnectSummary({
    status: "Almost ready",
    message: agg.message,
    connections: [row],
    occamVerify: { ok: true },
  });
  assert.match(human, /Needs restart:/);
  assert.doesNotMatch(human, /Run `occam connect` again/);
}

function testCursorAdapterNoopVsApply(tmpHome) {
  void tmpHome;
  const adapter = createCursorAdapter({ occamHome: repoRoot });
  // Never call apply() here — this selftest must not mutate live Cursor config.
  const plan = adapter.plan();
  const verify = adapter.verifyHost();
  if (plan.action === "noop") {
    assert.equal(plan.requiresRestart, false);
    assert.equal(verify.ok, true);
    assert.equal(verify.requiresRestart, false);
    assert.equal(verify.configured, true);
    const ready = evaluateReadyState({
      occamLevel: VERIFICATION_LEVELS.TOOLS_LIST_OK,
      hostLevel: verify.level,
      maxHostLevel: adapter.maxVerificationLevel,
      requiresRestart: false,
      configured: verify.configured === true,
    });
    assert.equal(ready.status, "Configured");
  }
  if (verify.ok) {
    assert.equal(verify.requiresRestart, false);
  }
}

function testCursorConfigPathContract() {
  assert.equal(cursorUserConfigPath(), join(homedir(), ".cursor", "mcp.json"));
  assert.equal(OCCAM_MCP_SERVER_NAME, "ff-occam");
  const entry = desiredEntry(repoRoot);
  assert.ok(entry.args?.some((a) => String(a).includes("launch-mcp-host.mjs")));
  assert.equal(entry.env?.[OCCAM_MANAGED_ENV_KEY], OCCAM_MANAGED_MARKER);
}

function testMixedSeparatorRegistrationEqual() {
  const base = desiredEntry(repoRoot);
  const mixed = {
    ...base,
    env: {
      ...base.env,
      OCCAM_HOME: String(base.env.OCCAM_HOME).replace(/\\/g, "/"),
    },
    cwd: String(base.cwd).replace(/\\/g, "/"),
  };
  assert.equal(mcpEntriesEqual(base, mixed), true);
  assert.equal(mcpEntriesEqual(mixed, base), true);
}

function main() {
  const tmp = mkdtempSync(join(tmpdir(), "occam-cursor-state-"));
  try {
    testEvaluateConfiguredVsRestart();
    testAggregateSecondConnect();
    testAggregateRestartAfterMutation();
    testCursorConfigPathContract();
    testMixedSeparatorRegistrationEqual();
    testCursorAdapterNoopVsApply(tmp);
    console.log("cursor-state.selftest: OK");
  } finally {
    rmSync(tmp, { recursive: true, force: true });
  }
}

main();
