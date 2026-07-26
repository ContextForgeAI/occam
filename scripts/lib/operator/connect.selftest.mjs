/**
 * Self-tests for Occam connect platform (Wave 1 core — no live host mutation).
 */
import assert from "node:assert/strict";
import { mkdtempSync, writeFileSync, rmSync, readFileSync, existsSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import {
  buildStableLaunchSpec,
  stdioFromSpec,
  evaluateReadyState,
  aggregateConnectionReady,
  VERIFICATION_LEVELS,
  resolveConnectMode,
  parseHermesMcpServer,
  OCCAM_MCP_SERVER_NAME,
  OCCAM_MANAGED_ENV_KEY,
  OCCAM_MANAGED_MARKER,
  looksLikeOccamManagedEntry,
  decidePostVerifyCleanup,
  loadMcpConfig,
  planMcpMerge,
  commitMcpRegistration,
  rollbackMcpRegistration,
  parseClaudeGetConnected,
  parseCodexGetJson,
  codexJsonToEntry,
  parseGeminiListEntry,
  geminiVerificationFromInspection,
  WAVE2_HOST_IDS,
  WAVE3_HOST_IDS,
  AUTO_CONNECT_HOST_IDS,
  OPENCODE_ENTRY_CODEC,
  createOpencodeAdapter,
  createGooseAdapter,
  createJunieAdapter,
  createClaudeDesktopAdapter,
  createVscodeAdapter,
  resolveClaudeDesktopConfigTarget,
  vscodeUserConfigPath,
  clineConfigPath,
  rooConfigPath,
  windsurfConfigPath,
  zedSettingsPath,
  createZedAdapter,
  createConfigFileAdapter,
  createHostAdapters,
  selectAutoConnectAdapters,
  VSCODE_ENTRY_CODEC,
  looksLikeJsonc,
  dirHasEntries,
  describeSupportedHosts,
  partitionSupportedHosts,
} from "./connect/index.mjs";
import { stableLaunchCommand } from "./mcp-snippet.mjs";
import {
  argsEqual,
  envEqual,
  normalizePathish,
  windowsCmdQuote,
} from "./connect/process.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(here, "..", "..", "..");

function testLaunchSpec() {
  const spec = buildStableLaunchSpec(repoRoot);
  assert.equal(normalizePathish(spec.cwd), normalizePathish(repoRoot));
  assert.ok(spec.launcherPath.includes("launch-mcp-host.mjs"));
  assert.equal(spec.env.OCCAM_HOME, repoRoot.replace(/[\\/]+$/, "") || repoRoot);
  assert.equal(spec.env.OCCAM_BANNER, "0");
  assert.equal(spec.env[OCCAM_MANAGED_ENV_KEY], OCCAM_MANAGED_MARKER);
  assert.equal(typeof spec.wrapperUsable, "boolean");
  assert.equal("preferredForHermes" in spec, false);

  const wrapped = stdioFromSpec(spec, { preferWrapper: true });
  assert.ok(wrapped.command);
  assert.equal(typeof wrapped.env.OCCAM_HOME, "string");
  assert.equal(wrapped.env[OCCAM_MANAGED_ENV_KEY], OCCAM_MANAGED_MARKER);

  const withCwd = stdioFromSpec(spec, { includeCwd: true });
  assert.equal(withCwd.command, "node");
  assert.ok(withCwd.args[0].includes("launch-mcp-host.mjs"));
  assert.equal(normalizePathish(withCwd.cwd), normalizePathish(repoRoot));
}

function testVerificationReady() {
  const ready = evaluateReadyState({
    occamLevel: VERIFICATION_LEVELS.TOOLS_LIST_OK,
    hostLevel: VERIFICATION_LEVELS.HOST_DISCOVERS,
    maxHostLevel: VERIFICATION_LEVELS.HOST_DISCOVERS,
    requiresRestart: false,
  });
  assert.equal(ready.ready, true);

  const restart = evaluateReadyState({
    occamLevel: VERIFICATION_LEVELS.TOOLS_LIST_OK,
    hostLevel: VERIFICATION_LEVELS.HOST_DISCOVERS,
    maxHostLevel: VERIFICATION_LEVELS.HOST_DISCOVERS,
    requiresRestart: true,
  });
  assert.equal(restart.ready, false);
  assert.match(restart.status, /restart/i);

  const configOnly = evaluateReadyState({
    occamLevel: VERIFICATION_LEVELS.TOOLS_LIST_OK,
    hostLevel: VERIFICATION_LEVELS.CONFIG_VALID,
    maxHostLevel: VERIFICATION_LEVELS.HOST_DISCOVERS,
  });
  assert.equal(configOnly.ready, false);

  const hostBlocked = evaluateReadyState({
    occamLevel: VERIFICATION_LEVELS.TOOLS_LIST_OK,
    hostLevel: VERIFICATION_LEVELS.CONFIG_VALID,
    maxHostLevel: VERIFICATION_LEVELS.HOST_DISCOVERS,
    configured: true,
    requiresUserAction: true,
    hostBlocked: true,
    actionMessage: "Trust the folder",
  });
  assert.deepEqual(
    {
      configured: hostBlocked.configured,
      requiresUserAction: hostBlocked.requiresUserAction,
      hostBlocked: hostBlocked.hostBlocked,
      ready: hostBlocked.ready,
    },
    {
      configured: true,
      requiresUserAction: true,
      hostBlocked: true,
      ready: false,
    },
  );
  assert.equal(hostBlocked.status, "Configured — action required");
}

function testAggregateReady() {
  const allOk = aggregateConnectionReady([
    { name: "A", readyState: { ready: true, status: "Ready" } },
    { name: "B", readyState: { ready: true, status: "Ready" } },
  ]);
  assert.equal(allOk.ready, true);
  assert.equal(allOk.status, "Ready");

  const partial = aggregateConnectionReady([
    { name: "A", readyState: { ready: true, status: "Ready" } },
    {
      name: "B",
      readyState: { ready: false, status: "Configured — host discovery incomplete" },
    },
  ]);
  assert.equal(partial.ready, false);
  assert.equal(partial.status, "Action required");
  assert.match(partial.message, /B:/);

  const applyFailDoesNotHide = aggregateConnectionReady([
    { name: "A", readyState: { ready: true, status: "Ready" } },
    { name: "B", readyState: { ready: false, status: "Apply failed" } },
  ]);
  assert.equal(applyFailDoesNotHide.ready, false);
  assert.equal(applyFailDoesNotHide.status, "Action required");

  // Mixed: Hermes Ready + OpenClaw verify-fail → not Ready; A success does not mask B.
  const mixed = aggregateConnectionReady([
    { name: "Hermes Agent", readyState: { ready: true, status: "Ready" } },
    {
      name: "OpenClaw",
      readyState: {
        ready: false,
        status: "Configured — host discovery incomplete",
        message: "verify failed; rolled back (remove)",
      },
    },
  ]);
  assert.equal(mixed.ready, false);
  assert.equal(mixed.status, "Action required");
  assert.match(mixed.message, /OpenClaw:/);
  assert.match(mixed.message, /Hermes Agent:/);

  // Cursor restart-required with peers Ready → Almost ready (not false Ready).
  const almost = aggregateConnectionReady([
    { name: "Hermes Agent", readyState: { ready: true, status: "Ready" }, hostVerify: { ok: true } },
    {
      name: "Cursor",
      readyState: {
        ready: false,
        status: "Configured — restart required",
        requiresRestart: true,
      },
      hostVerify: { ok: true },
    },
  ]);
  assert.equal(almost.ready, false);
  assert.equal(almost.status, "Almost ready");
  assert.match(almost.message, /Cursor/);

  const actionRequired = aggregateConnectionReady([
    { name: "Hermes Agent", readyState: { ready: true, status: "Ready" }, hostVerify: { ok: true } },
    {
      name: "Gemini CLI",
      readyState: {
        configured: true,
        requiresUserAction: true,
        hostBlocked: true,
        ready: false,
        status: "Configured — action required",
      },
      hostVerify: { ok: false },
    },
    {
      name: "Cursor",
      readyState: {
        configured: true,
        ready: false,
        status: "Configured — restart required",
        requiresRestart: true,
      },
      hostVerify: { ok: true },
    },
  ]);
  assert.equal(actionRequired.ready, false);
  assert.equal(actionRequired.status, "Action required");
  assert.match(actionRequired.message, /Gemini CLI/);
  assert.match(actionRequired.message, /Cursor/);
}

function testPolicyCi() {
  const ci = resolveConnectMode({ CI: "1" });
  assert.equal(ci.mutateHosts, false);
  assert.equal(ci.mode, "ci");

  const autoInCi = resolveConnectMode({ CI: "1", OCCAM_CONNECT: "auto" });
  assert.equal(autoInCi.mutateHosts, false);
  assert.equal(autoInCi.mode, "ci");
  assert.match(autoInCi.reason, /FORCE/);

  const onInCi = resolveConnectMode({ CI: "1", OCCAM_CONNECT: "on" });
  assert.equal(onInCi.mutateHosts, false);

  const forced = resolveConnectMode({
    CI: "1",
    OCCAM_CONNECT: "auto",
    OCCAM_CONNECT_FORCE: "1",
  });
  assert.equal(forced.mutateHosts, true);
  assert.equal(forced.mode, "auto");

  const off = resolveConnectMode({ OCCAM_CONNECT: "off" });
  assert.equal(off.mutateHosts, false);

  const on = resolveConnectMode({ OCCAM_CONNECT: "auto" });
  assert.equal(on.mutateHosts, true);
}

function testOwnershipAndCleanup() {
  assert.equal(
    looksLikeOccamManagedEntry({
      command: "npx",
      args: ["something-else"],
      env: {},
    }),
    false,
  );
  assert.equal(
    looksLikeOccamManagedEntry({
      command: "node",
      args: ["/tmp/occam/scripts/launch-mcp-host.mjs"],
      env: {},
    }),
    true,
  );
  assert.equal(
    looksLikeOccamManagedEntry({
      command: "custom",
      args: [],
      env: { [OCCAM_MANAGED_ENV_KEY]: OCCAM_MANAGED_MARKER },
    }),
    true,
  );

  assert.equal(
    decidePostVerifyCleanup({ applied: true, action: "add", verifyOk: false }),
    "remove",
  );
  assert.equal(
    decidePostVerifyCleanup({ applied: true, action: "update", verifyOk: false }),
    "restore",
  );
  assert.equal(
    decidePostVerifyCleanup({ applied: true, action: "add", verifyOk: true }),
    "none",
  );
  assert.equal(
    decidePostVerifyCleanup({ applied: false, action: "add", verifyOk: false }),
    "none",
  );
  assert.equal(
    decidePostVerifyCleanup({ applied: true, action: "noop", verifyOk: false }),
    "none",
  );
  assert.equal(
    decidePostVerifyCleanup({
      applied: true,
      action: "add",
      verifyOk: false,
      configured: true,
      requiresUserAction: true,
      hostBlocked: true,
    }),
    "none",
  );
}

function testHermesYamlParse() {
  const yaml = `
mcp_servers:
  ${OCCAM_MCP_SERVER_NAME}:
    command: "node"
    args:
      - "C:/occam/scripts/launch-mcp-host.mjs"
    env:
      OCCAM_HOME: "C:/occam"
      OCCAM_BANNER: "0"
    enabled: true
  other:
    command: "npx"
`;
  const entry = parseHermesMcpServer(yaml, OCCAM_MCP_SERVER_NAME);
  assert.ok(entry);
  assert.equal(entry.command, "node");
  assert.deepEqual(entry.args, ["C:/occam/scripts/launch-mcp-host.mjs"]);
  assert.equal(entry.env.OCCAM_HOME, "C:/occam");
  assert.equal(entry.env.OCCAM_BANNER, "0");

  const missing = parseHermesMcpServer(yaml, "nope");
  assert.equal(missing, null);
}

function testIdempotencyComparators() {
  assert.equal(envEqual({ A: "1", B: "2" }, { B: "2", A: "1" }), true);
  assert.equal(envEqual({ A: "1" }, { A: "2" }), false);
  assert.equal(argsEqual(["C:\\a\\b", "x"], ["C:/a/b", "x"]), true);
}

function testMalformedOpenClawGuardShape() {
  const dir = mkdtempSync(join(tmpdir(), "occam-connect-"));
  const path = join(dir, "openclaw.json");
  writeFileSync(path, "{ not json", "utf8");
  rmSync(dir, { recursive: true, force: true });
  assert.ok(true);
}

function testNoHermesLeakInCoreModules() {
  assert.equal(/hermes/i.test(buildStableLaunchSpec.toString()), false);
  const ownershipSrc = readFileSync(join(here, "connect", "ownership.mjs"), "utf8");
  assert.equal(/hermes|openclaw/i.test(ownershipSrc), false);
  const orchSrc = readFileSync(join(here, "connect", "orchestrator.mjs"), "utf8");
  assert.equal(/adapter\.id\s*===\s*["']hermes["']/i.test(orchSrc), false);
  assert.equal(/preferredForHermes|hermesStdioFromSpec/i.test(orchSrc), false);
  const processSrc = readFileSync(join(here, "connect", "process.mjs"), "utf8");
  assert.equal(/resolveHermesInvoker|resolveOpenClawInvoker/i.test(processSrc), false);

  // Shared core must not name products in code: a hand-written host list is a
  // second registry that silently goes stale when a host is added or retiered.
  const productNames =
    /\b(hermes|openclaw|claude code|claude desktop|codex|gemini|cursor|vs code|copilot|cline|roo|windsurf|zed|opencode|goose|junie)\b/i;
  for (const file of [
    "orchestrator.mjs",
    "render.mjs",
    "verification.mjs",
    "policy.mjs",
    "config-engine.mjs",
    "config-file-adapter.mjs",
  ]) {
    const code = readFileSync(join(here, "connect", file), "utf8")
      .replace(/\/\*[\s\S]*?\*\//g, "")
      .replace(/(^|[^:])\/\/.*$/gm, "$1");
    assert.equal(productNames.test(code), false, `${file} hardcodes a host name`);
  }

  // The supported-host copy is derived, so it covers every registered adapter.
  const allAdapters = Object.values(createHostAdapters({ occamHome: repoRoot }));
  const rows = allAdapters.map((a) => ({
    name: a.name,
    connectionMethod: a.connectionMethod,
  }));
  const described = describeSupportedHosts(rows);
  for (const adapter of allAdapters) {
    assert.ok(described.includes(adapter.name), `${adapter.id} missing from supported hosts`);
  }
  const split = partitionSupportedHosts(rows);
  assert.deepEqual(split.assisted, ["Goose", "Junie"]);
  assert.equal(split.automatic.length, allAdapters.length - 2);
}

function testConfigEngine() {
  const dir = mkdtempSync(join(tmpdir(), "occam-mcp-cfg-"));
  const path = join(dir, "mcp.json");
  const desired = {
    command: "node",
    args: [join(repoRoot, "scripts", "launch-mcp-host.mjs")],
    cwd: repoRoot,
    env: {
      OCCAM_HOME: repoRoot,
      OCCAM_BANNER: "0",
      [OCCAM_MANAGED_ENV_KEY]: OCCAM_MANAGED_MARKER,
    },
  };

  // Fresh add
  const added = commitMcpRegistration({
    configPath: path,
    serverName: OCCAM_MCP_SERVER_NAME,
    desired,
    occamHome: repoRoot,
  });
  assert.equal(added.ok, true);
  assert.equal(added.applied, true);
  assert.equal(added.action, "add");

  // Unrelated entry preserved on update path
  writeFileSync(
    path,
    JSON.stringify(
      {
        mcpServers: {
          other: { command: "npx", args: ["x"] },
          [OCCAM_MCP_SERVER_NAME]: desired,
        },
      },
      null,
      2,
    ),
    "utf8",
  );
  const loaded = loadMcpConfig(path);
  assert.equal(loaded.ok, true);
  assert.ok(loaded.doc.mcpServers.other);

  // Unmanaged skip
  writeFileSync(
    path,
    JSON.stringify(
      {
        mcpServers: {
          [OCCAM_MCP_SERVER_NAME]: { command: "custom-bin", args: ["a"], env: {} },
        },
      },
      null,
      2,
    ),
    "utf8",
  );
  const skip = planMcpMerge({
    loaded: loadMcpConfig(path),
    serverName: OCCAM_MCP_SERVER_NAME,
    desired,
    occamHome: repoRoot,
  });
  assert.equal(skip.action, "skip-unmanaged");

  // Malformed refuse
  writeFileSync(path, "{ not json", "utf8");
  const bad = loadMcpConfig(path);
  assert.equal(bad.parseError, true);
  const refuse = planMcpMerge({
    loaded: bad,
    serverName: OCCAM_MCP_SERVER_NAME,
    desired,
  });
  assert.equal(refuse.action, "refuse");

  // Rollback restore
  writeFileSync(path, JSON.stringify({ mcpServers: { keep: { command: "x" } } }, null, 2), "utf8");
  const before = readFileSync(path, "utf8");
  const mut = commitMcpRegistration({
    configPath: path,
    serverName: OCCAM_MCP_SERVER_NAME,
    desired,
    occamHome: repoRoot,
  });
  assert.equal(mut.applied, true);
  const rb = rollbackMcpRegistration(path, {
    backupPath: mut.backupPath,
    previousRaw: mut.previousRaw,
    previousMissing: mut.previousMissing,
  });
  assert.equal(rb.ok, true);
  assert.equal(readFileSync(path, "utf8"), before);

  // previousMissing restore must honor rootKey for Wave 3 shapes
  for (const rootKey of ["mcpServers", "servers", "context_servers", "mcp"]) {
    const p = join(dir, `fresh-${rootKey}.json`);
    const mut = commitMcpRegistration({
      configPath: p,
      rootKey,
      serverName: OCCAM_MCP_SERVER_NAME,
      desired,
      occamHome: repoRoot,
    });
    assert.equal(mut.applied, true);
    assert.equal(mut.rootKey, rootKey);
    assert.equal(existsSync(p), true);
    const rb = rollbackMcpRegistration(p, {
      backupPath: mut.backupPath,
      previousRaw: mut.previousRaw,
      previousMissing: mut.previousMissing,
      rootKey: mut.rootKey,
    });
    assert.equal(rb.ok, true);
    assert.equal(existsSync(p), false, `previousMissing should unlink trivial ${rootKey} doc`);
  }

  rmSync(dir, { recursive: true, force: true });
}

function testWave2ParsersAndRegistry() {
  assert.equal(WAVE2_HOST_IDS.includes("claude-code"), true);
  assert.equal(WAVE2_HOST_IDS.includes("cursor"), true);
  assert.equal(AUTO_CONNECT_HOST_IDS.length >= 6, true);

  assert.equal(
    parseClaudeGetConnected("ff-occam:\n  Status: √ Connected\n"),
    true,
  );
  assert.equal(parseClaudeGetConnected("Status: Failed"), false);

  const codex = parseCodexGetJson(`noise\n{"name":"ff-occam","enabled":true,"transport":{"type":"stdio","command":"node","args":["a"],"env":{"OCCAM_HOME":"x"}}}`);
  assert.ok(codex);
  const entry = codexJsonToEntry(codex);
  assert.equal(entry.command, "node");
  assert.deepEqual(entry.args, ["a"]);

  const gem = parseGeminiListEntry(
    "○ ff-occam: node C:/occam/scripts/launch-mcp-host.mjs (stdio) - Disabled\n",
    "ff-occam",
  );
  assert.ok(gem);
  assert.equal(gem.disabled, true);
  assert.equal(gem.command, "node");

  const blocked = geminiVerificationFromInspection({
    registered: true,
    disabled: true,
    listOut: "ff-occam - Disabled",
  });
  assert.equal(blocked.configured, true);
  assert.equal(blocked.requiresUserAction, true);
  assert.equal(blocked.hostBlocked, true);
  assert.equal(blocked.ok, false);

  let rollbackCalls = 0;
  const cleanup = decidePostVerifyCleanup({
    applied: true,
    action: "add",
    verifyOk: blocked.ok,
    configured: blocked.configured,
    requiresUserAction: blocked.requiresUserAction,
    hostBlocked: blocked.hostBlocked,
  });
  if (cleanup !== "none") rollbackCalls += 1;
  assert.equal(cleanup, "none");
  assert.equal(rollbackCalls, 0);
}

function testWave3ProfilesAndCodecs() {
  assert.equal(WAVE3_HOST_IDS.includes("claude-desktop"), true);
  assert.equal(WAVE3_HOST_IDS.includes("vscode"), true);
  assert.equal(WAVE3_HOST_IDS.includes("opencode"), true);
  assert.equal(WAVE3_HOST_IDS.includes("goose"), true);
  assert.equal(WAVE3_HOST_IDS.includes("junie"), true);
  assert.ok(AUTO_CONNECT_HOST_IDS.length >= WAVE2_HOST_IDS.length + WAVE3_HOST_IDS.length);

  const encoded = OPENCODE_ENTRY_CODEC.encode({
    command: "node",
    args: ["/tmp/occam/scripts/launch-mcp-host.mjs"],
    env: { OCCAM_HOME: "/tmp/occam", [OCCAM_MANAGED_ENV_KEY]: OCCAM_MANAGED_MARKER },
  });
  assert.equal(encoded.type, "local");
  assert.deepEqual(encoded.command, ["node", "/tmp/occam/scripts/launch-mcp-host.mjs"]);
  assert.equal(encoded.environment.OCCAM_HOME, "/tmp/occam");
  const decoded = OPENCODE_ENTRY_CODEC.decode(encoded);
  assert.equal(decoded.command, "node");
  assert.deepEqual(decoded.args, ["/tmp/occam/scripts/launch-mcp-host.mjs"]);

  const dir = mkdtempSync(join(tmpdir(), "occam-opencode-"));
  const path = join(dir, "opencode.json");
  const desired = {
    command: "node",
    args: [join(repoRoot, "scripts", "launch-mcp-host.mjs")],
    cwd: repoRoot,
    env: {
      OCCAM_HOME: repoRoot,
      OCCAM_BANNER: "0",
      [OCCAM_MANAGED_ENV_KEY]: OCCAM_MANAGED_MARKER,
    },
  };
  const added = commitMcpRegistration({
    configPath: path,
    rootKey: "mcp",
    serverName: OCCAM_MCP_SERVER_NAME,
    desired,
    occamHome: repoRoot,
    codec: OPENCODE_ENTRY_CODEC,
  });
  assert.equal(added.ok, true);
  const raw = JSON.parse(readFileSync(path, "utf8"));
  assert.equal(raw.mcp[OCCAM_MCP_SERVER_NAME].type, "local");
  assert.ok(Array.isArray(raw.mcp[OCCAM_MCP_SERVER_NAME].command));
  // preserve siblings
  raw.keep = true;
  writeFileSync(path, JSON.stringify(raw, null, 2), "utf8");
  const again = commitMcpRegistration({
    configPath: path,
    rootKey: "mcp",
    serverName: OCCAM_MCP_SERVER_NAME,
    desired,
    occamHome: repoRoot,
    codec: OPENCODE_ENTRY_CODEC,
  });
  assert.equal(again.action, "noop");
  assert.equal(JSON.parse(readFileSync(path, "utf8")).keep, true);
  rmSync(dir, { recursive: true, force: true });

  const goose = createGooseAdapter({ occamHome: repoRoot });
  assert.equal(goose.connectionMethod, "ASSISTED");
  assert.equal(goose.plan().action, "assisted");
  const junie = createJunieAdapter({ occamHome: repoRoot });
  assert.equal(junie.connectionMethod, "ASSISTED");

  assert.ok(vscodeUserConfigPath().includes("mcp.json"));
  assert.ok(clineConfigPath().includes("cline_mcp_settings.json"));
  assert.ok(rooConfigPath().includes("mcp_settings.json"));
  assert.ok(windsurfConfigPath().includes("mcp_config.json"));
  assert.ok(zedSettingsPath().length > 0);
  assert.ok(resolveClaudeDesktopConfigTarget().candidates.length >= 1);

  const cd = createClaudeDesktopAdapter({ occamHome: repoRoot });
  assert.equal(cd.connectionMethod, "CONFIG_FILE");
  const vs = createVscodeAdapter({ occamHome: repoRoot });
  assert.equal(vs.profile.rootKey, "servers");
  const oc = createOpencodeAdapter({ occamHome: repoRoot });
  assert.equal(oc.profile.rootKey, "mcp");

  assert.match(stableLaunchCommand(repoRoot), /launch-mcp-host\.mjs/);
}

/**
 * Regressions for defects found during live desktop validation (2026-07-25).
 */
function testLiveValidationFindings() {
  // 1. Empty leftover app directories must not count as an installed host.
  const emptyDir = mkdtempSync(join(tmpdir(), "occam-empty-"));
  assert.equal(dirHasEntries(emptyDir), false);
  writeFileSync(join(emptyDir, "x"), "1", "utf8");
  assert.equal(dirHasEntries(emptyDir), true);
  rmSync(emptyDir, { recursive: true, force: true });

  // 2. Claude Desktop strips `cwd` on launch — writing it caused endless updates.
  const cd = createClaudeDesktopAdapter({ occamHome: repoRoot });
  assert.equal(cd.profile.includeCwd, false);
  assert.equal(cd.supportTier, "A");

  // 3. VS Code entries need the documented `type: "stdio"`.
  const vsEncoded = VSCODE_ENTRY_CODEC.encode({ command: "node", args: ["a.mjs"], env: {} });
  assert.equal(vsEncoded.type, "stdio");
  assert.equal(VSCODE_ENTRY_CODEC.decode(vsEncoded).command, "node");
  assert.equal(VSCODE_ENTRY_CODEC.decode(vsEncoded).type, undefined);

  // 4. JSONC configs are refused with an honest reason, never rewritten.
  assert.equal(looksLikeJsonc('{"a": "http://x"}'), false);
  assert.equal(looksLikeJsonc('{\n  // comment\n  "a": 1\n}'), true);
  const jsoncDir = mkdtempSync(join(tmpdir(), "occam-jsonc-"));
  const jsoncPath = join(jsoncDir, "settings.json");
  const jsoncRaw = '{\n  // user comment\n  "context_servers": {}\n}\n';
  writeFileSync(jsoncPath, jsoncRaw, "utf8");
  const zedLike = createConfigFileAdapter(
    {
      ...createZedAdapter({ occamHome: repoRoot }).profile,
      resolveConfigTarget: () => ({ path: jsoncPath, ambiguous: false, candidates: [jsoncPath] }),
    },
    { occamHome: repoRoot },
  );
  assert.equal(zedLike.plan().action, "jsonc");
  const jsoncApply = zedLike.apply();
  assert.equal(jsoncApply.applied, false);
  assert.equal(jsoncApply.requiresUserAction, true);
  assert.equal(readFileSync(jsoncPath, "utf8"), jsoncRaw);
  assert.match(zedLike.verifyHost().message, /comments/i);
  rmSync(jsoncDir, { recursive: true, force: true });

  // 5. Rollback must undo only our entry — hosts rewrite their config while running.
  const rbDir = mkdtempSync(join(tmpdir(), "occam-rollback-"));
  const rbPath = join(rbDir, "config.json");
  writeFileSync(rbPath, JSON.stringify({ mcpServers: {}, hostState: "before" }, null, 2), "utf8");
  const cdLike = createConfigFileAdapter(
    { ...cd.profile, resolveConfigTarget: () => ({ path: rbPath, ambiguous: false, candidates: [rbPath] }) },
    { occamHome: repoRoot },
  );
  assert.equal(cdLike.apply().applied, true);
  const mutated = JSON.parse(readFileSync(rbPath, "utf8"));
  mutated.hostState = "changed-by-host-after-apply";
  writeFileSync(rbPath, JSON.stringify(mutated, null, 2), "utf8");
  const rb = cdLike.rollback();
  assert.equal(rb.ok, true);
  const afterRb = JSON.parse(readFileSync(rbPath, "utf8"));
  assert.equal(afterRb.hostState, "changed-by-host-after-apply");
  assert.equal(afterRb.mcpServers[OCCAM_MCP_SERVER_NAME], undefined);
  assert.ok(Object.hasOwn(afterRb, "mcpServers"), "pre-existing root key must survive");

  // ...and a root key we created ourselves must not be left behind as an empty shell.
  const bareePath = join(rbDir, "bare.json");
  writeFileSync(bareePath, JSON.stringify({ hostState: "before" }, null, 2), "utf8");
  const bareAdapter = createConfigFileAdapter(
    {
      ...cd.profile,
      resolveConfigTarget: () => ({ path: bareePath, ambiguous: false, candidates: [bareePath] }),
    },
    { occamHome: repoRoot },
  );
  assert.equal(bareAdapter.apply().applied, true);
  const bareMutated = JSON.parse(readFileSync(bareePath, "utf8"));
  bareMutated.hostState = "changed";
  writeFileSync(bareePath, JSON.stringify(bareMutated, null, 2), "utf8");
  bareAdapter.rollback();
  const bareAfter = JSON.parse(readFileSync(bareePath, "utf8"));
  assert.equal(Object.hasOwn(bareAfter, "mcpServers"), false);
  assert.equal(bareAfter.hostState, "changed");
  rmSync(rbDir, { recursive: true, force: true });

  // 6. Tier B hosts only auto-connect when the user names them explicitly.
  const fakeA = { id: "a", supportTier: "A", connectionMethod: "CONFIG_FILE", detect: () => ({ detected: true, confidence: "high" }) };
  const fakeB = { id: "b", supportTier: "B", connectionMethod: "CONFIG_FILE", detect: () => ({ detected: true, confidence: "high" }) };
  assert.deepEqual(selectAutoConnectAdapters([fakeA, fakeB]).map((a) => a.id), ["a"]);
  assert.deepEqual(
    selectAutoConnectAdapters([fakeA, fakeB], { explicit: true }).map((a) => a.id),
    ["a", "b"],
  );
  for (const id of ["vscode", "zed", "cline", "roo", "windsurf", "opencode"]) {
    assert.equal(createHostAdapters({ occamHome: repoRoot })[id].supportTier, "B", id);
  }

  // 7. cmd.exe quoting: backslash escapes broke JSON args at the first `&`.
  assert.equal(windowsCmdQuote("plain"), "plain");
  assert.equal(windowsCmdQuote("with space"), '"with space"');
  assert.equal(windowsCmdQuote('{"a":"b & c"}'), '"{""a"":""b & c""}"');
  assert.equal(windowsCmdQuote('{"a":1}').includes("\\"), false);
  assert.equal(windowsCmdQuote(""), '""');
}

function main() {
  testLaunchSpec();
  testVerificationReady();
  testAggregateReady();
  testPolicyCi();
  testOwnershipAndCleanup();
  testHermesYamlParse();
  testIdempotencyComparators();
  testMalformedOpenClawGuardShape();
  testNoHermesLeakInCoreModules();
  testConfigEngine();
  testWave2ParsersAndRegistry();
  testWave3ProfilesAndCodecs();
  testLiveValidationFindings();
  console.log("connect.selftest.mjs OK");
}

main();
