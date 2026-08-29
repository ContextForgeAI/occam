#!/usr/bin/env node
/**
 * Friend-grade first-use acceptance scenarios (A–E).
 * No live host mutation — adapter detect + human summary contracts only.
 */
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import {
  createOpenClawAdapter,
  createHermesAdapter,
  describeOpenClawResidue,
  resolveOpenClawInvoker,
  renderHumanConnectSummary,
  selectAutoConnectAdapters,
  evaluateReadyState,
  VERIFICATION_LEVELS,
} from "./connect/index.mjs";
import { FIRST_SUCCESS_URL } from "./install-ux.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(here, "..", "..", "..");

/** Scenario A — stale OpenClaw residue must not be connectable. */
function testScenarioA_falseResidue() {
  const inv = resolveOpenClawInvoker();
  if (!inv) {
    const residue = describeOpenClawResidue();
    const adapter = createOpenClawAdapter({ occamHome: repoRoot });
    const d = adapter.detect();
    assert.equal(d.detected, false);
    assert.equal(d.confidence, "low");
    assert.equal(d.executable, null);
    if (residue.residue) {
      assert.equal(d.residue, true);
      assert.ok(Array.isArray(d.residueSignals));
    }
    const selected = selectAutoConnectAdapters([adapter], { explicit: false });
    assert.equal(selected.length, 0);
  } else {
    assert.doesNotMatch(inv.label, /npx/i);
  }
}

/** Scenario B — Hermes rollback humanization. */
function testScenarioB_hermesRollbackSummary() {
  const report = {
    status: "Action required",
    ready: false,
    occamVerify: { ok: true, toolCount: 8 },
    runtimes: [],
    connections: [
      {
        name: "Hermes Agent",
        apply: { ok: true, applied: true, action: "add" },
        hostVerify: { ok: false, level: 1 },
        readyState: {
          ready: false,
          status: "Not connected",
          rolledBack: true,
          message:
            "Hermes Agent was detected, but Occam could not confirm that Hermes Agent loaded the connection. The attempted change was undone. Occam is not available in Hermes Agent yet.",
        },
        rollback: { ok: true, kind: "remove" },
      },
    ],
  };
  const human = renderHumanConnectSummary(report);
  assert.match(human, /Not connected:/);
  assert.match(human, /• Hermes Agent/);
  assert.match(human, /was undone|not available in Hermes Agent yet/i);
  assert.doesNotMatch(human, /Connected and ready:/);
  assert.doesNotMatch(human, /host discovery not confirmed/);
  assert.doesNotMatch(human, /rolled back \(remove\)/);
  assert.match(human, /What to do now:/);
  assert.match(human, new RegExp(FIRST_SUCCESS_URL.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")));
}

/** Scenario C — Ollama next step when no Ready host. */
function testScenarioC_ollamaNextStep() {
  const report = {
    status: "Action required",
    ready: false,
    occamVerify: { ok: true },
    runtimes: [{ id: "ollama", name: "Ollama", detected: true, confidence: "high" }],
    connections: [
      {
        name: "Hermes Agent",
        apply: { ok: true, applied: true, action: "add" },
        hostVerify: { ok: false },
        readyState: {
          ready: false,
          status: "Not connected",
          rolledBack: true,
          message:
            "Hermes Agent was detected, but Occam could not confirm that Hermes Agent loaded the connection. The attempted change was undone. Occam is not available in Hermes Agent yet.",
        },
        rollback: { ok: true, kind: "remove" },
      },
    ],
  };
  const human = renderHumanConnectSummary(report);
  assert.match(human, /occam chat/);
  assert.match(human, /Ollama is installed/);
  assert.doesNotMatch(human, /Occam itself is working/);
}

/** Scenario D — one Ready MCP host. */
function testScenarioD_oneReadyHost() {
  const report = {
    status: "Ready",
    ready: true,
    occamVerify: { ok: true },
    runtimes: [],
    connections: [
      {
        name: "Cursor",
        apply: { ok: true, applied: true, action: "add" },
        hostVerify: { ok: true, level: 5 },
        readyState: { ready: true, status: "Ready" },
      },
    ],
  };
  const human = renderHumanConnectSummary(report);
  assert.match(human, /Connected and ready:/);
  assert.match(human, /✓ Cursor/);
  assert.match(human, /Open Cursor/);
  assert.match(human, /Use Occam to read https:\/\/example\.com/);
  assert.match(human, new RegExp(FIRST_SUCCESS_URL.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")));
}

/** Scenario E — no ready hosts. */
function testScenarioE_noReadyHosts() {
  const report = {
    status: "Not ready",
    ready: false,
    occamVerify: { ok: true },
    runtimes: [],
    connections: [],
  };
  const human = renderHumanConnectSummary(report);
  assert.match(human, /not connected to an AI/i);
  assert.match(human, /occam connect/);
  assert.match(human, new RegExp(FIRST_SUCCESS_URL.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")));
  assert.doesNotMatch(human, /Connected and ready:/);
}

function testReadyStateMessageHuman() {
  const incomplete = evaluateReadyState({
    occamLevel: VERIFICATION_LEVELS.TOOLS_LIST_OK,
    hostLevel: VERIFICATION_LEVELS.CONFIG_VALID,
    maxHostLevel: VERIFICATION_LEVELS.HOST_DISCOVERS,
    configured: true,
  });
  assert.equal(incomplete.ready, false);
  assert.doesNotMatch(incomplete.message, /host discovery not confirmed/);
  assert.match(incomplete.message, /could not confirm/i);
}

function testOpenClawNpxGuard() {
  const src = readFileSync(join(here, "connect", "adapters", "openclaw.mjs"), "utf8");
  assert.doesNotMatch(src, /prefixArgs:\s*\[["']--yes["'],\s*["']openclaw["']/);
  assert.match(src, /NEVER treat `npx openclaw`/i);
}

function testHermesResidueNotAutoWithoutBinary() {
  const adapter = createHermesAdapter({ occamHome: repoRoot });
  const d = adapter.detect();
  if (!d.executable) {
    assert.equal(d.detected, false);
    assert.equal(d.confidence, "low");
    const selected = selectAutoConnectAdapters([adapter], { explicit: false });
    assert.equal(selected.length, 0);
  }
}

function main() {
  testScenarioA_falseResidue();
  testScenarioB_hermesRollbackSummary();
  testScenarioC_ollamaNextStep();
  testScenarioD_oneReadyHost();
  testScenarioE_noReadyHosts();
  testReadyStateMessageHuman();
  testOpenClawNpxGuard();
  testHermesResidueNotAutoWithoutBinary();
  console.log("friend-first-use.selftest: OK");
}

main();
