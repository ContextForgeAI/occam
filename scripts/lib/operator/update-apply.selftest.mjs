/**
 * Selftest: decideUpdateAction + runOccamUpdate idempotency (mocked apply).
 *
 *   node scripts/lib/operator/update-apply.selftest.mjs
 *
 * Marker: UPDATE_APPLY_SELFTEST_OK
 */
import assert from "node:assert/strict";
import { mkdtempSync, rmSync, writeFileSync, mkdirSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { decideUpdateAction } from "./update-check.mjs";
import { runOccamUpdate } from "./update-apply.mjs";

function assertDecision(label, got, want) {
  assert.equal(got.action, want.action, `${label}: action`);
  assert.equal(got.exitCode, want.exitCode, `${label}: exitCode`);
  if (want.messageIncludes) {
    assert.match(got.message, want.messageIncludes, `${label}: message`);
  }
}

// --- Unit: decideUpdateAction ---

assertDecision(
  "equal → noop",
  decideUpdateAction({ installed: "1.2.0", latest: "1.2.0" }),
  { action: "noop", exitCode: 0, messageIncludes: /Already up to date \(v1\.2\.0\)/ },
);

assertDecision(
  "newer available → upgrade",
  decideUpdateAction({ installed: "1.1.0", latest: "1.2.0" }),
  { action: "upgrade", exitCode: 0, messageIncludes: /Updating v1\.1\.0 → v1\.2\.0/ },
);

assertDecision(
  "current > latest → error",
  decideUpdateAction({ installed: "9.9.9", latest: "1.2.0" }),
  {
    action: "error",
    exitCode: 1,
    messageIncludes: /Installed version is newer than latest release/,
  },
);

assertDecision(
  "equal + force → upgrade",
  decideUpdateAction({ installed: "1.2.0", latest: "1.2.0", force: true }),
  { action: "upgrade", exitCode: 0, messageIncludes: /Reinstalling v1\.2\.0 \(--force\)/ },
);

assertDecision(
  "check failed → error",
  decideUpdateAction({ installed: "1.2.0", latest: null, error: "release API HTTP 503" }),
  { action: "error", exitCode: 1, messageIncludes: /Could not check for updates/ },
);

assertDecision(
  "unknown installed → error",
  decideUpdateAction({ installed: "unknown", latest: "1.2.0" }),
  { action: "error", exitCode: 1, messageIncludes: /Installed version unknown/ },
);

// --- Integration: two runs → second no-op (mocked apply) ---

{
  const home = mkdtempSync(join(tmpdir(), "occam-update-idempotent-"));
  mkdirSync(home, { recursive: true });
  writeFileSync(join(home, "VERSION"), "1.2.0\n", "utf8");

  let applyCalls = 0;
  const check = async () => ({
    installed: "1.2.0",
    latest: "1.2.0",
    rid: "win-x64",
    updateAvailable: false,
    upgradeHint: "Already up to date (v1.2.0).",
    error: null,
  });
  const apply = async () => {
    applyCalls += 1;
    return { ok: true, detail: "bootstrap ok (v1.2.0)" };
  };

  const first = await runOccamUpdate(home, { check, apply });
  assert.equal(first.ok, true);
  assert.equal(first.data.applied, false);
  assert.equal(first.data.decision.action, "noop");
  assert.match(first.message, /Already up to date \(v1\.2\.0\)/);
  assert.equal(applyCalls, 0, "noop must not call apply");

  const second = await runOccamUpdate(home, { check, apply });
  assert.equal(second.ok, true);
  assert.equal(second.data.decision.action, "noop");
  assert.equal(applyCalls, 0, "second run must remain no-op");

  const forced = await runOccamUpdate(home, { force: true, check, apply });
  assert.equal(forced.ok, true);
  assert.equal(forced.data.applied, true);
  assert.equal(applyCalls, 1, "force must call apply once");

  rmSync(home, { recursive: true, force: true });
}

{
  const home = mkdtempSync(join(tmpdir(), "occam-update-upgrade-"));
  writeFileSync(join(home, "VERSION"), "1.0.0\n", "utf8");
  let applyCalls = 0;
  const check = async () => ({
    installed: "1.0.0",
    latest: "1.2.0",
    rid: "linux-x64",
    updateAvailable: true,
    upgradeHint: "Newer release…",
    error: null,
  });
  const apply = async ({ version }) => {
    applyCalls += 1;
    assert.equal(version, "1.2.0");
    return { ok: true, detail: `bootstrap ok (v${version})` };
  };

  const upgraded = await runOccamUpdate(home, { check, apply });
  assert.equal(upgraded.ok, true);
  assert.equal(upgraded.data.applied, true);
  assert.equal(applyCalls, 1);

  // After "upgrade", pretend VERSION matches latest → second run no-op.
  const checkAfter = async () => ({
    installed: "1.2.0",
    latest: "1.2.0",
    rid: "linux-x64",
    updateAvailable: false,
    upgradeHint: "Already up to date (v1.2.0).",
    error: null,
  });
  const again = await runOccamUpdate(home, { check: checkAfter, apply });
  assert.equal(again.data.decision.action, "noop");
  assert.equal(applyCalls, 1, "post-upgrade run must not re-apply");

  rmSync(home, { recursive: true, force: true });
}

console.log("UPDATE_APPLY_SELFTEST_OK");
