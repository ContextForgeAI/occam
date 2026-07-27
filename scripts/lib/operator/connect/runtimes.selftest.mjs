#!/usr/bin/env node
/**
 * Runtime detectors — Ollama.app macOS discovery (not an MCP host adapter).
 */
import assert from "node:assert/strict";
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import {
  detectOllama,
  detectLlamaCpp,
  ollamaAppSignals,
  detectAllRuntimes,
} from "./runtimes.mjs";
import { renderDiscoverySection } from "../install-ux.mjs";
import { AUTO_CONNECT_HOST_IDS, createHostAdapters } from "./registry.mjs";

function testNoOllamaHostAdapter() {
  assert.equal(
    AUTO_CONNECT_HOST_IDS.includes("ollama"),
    false,
    "ollama must not be an auto-connect host id",
  );
  assert.equal(AUTO_CONNECT_HOST_IDS.includes("ollama.cpp"), false);
  assert.equal(AUTO_CONNECT_HOST_IDS.includes("llamacpp"), false);
  const adapters = createHostAdapters({ occamHome: "/tmp/occam-none" });
  assert.equal(adapters.ollama, undefined);
  assert.equal(adapters["ollama.cpp"], undefined);
}

function testOllamaAppBundleDetection() {
  const root = mkdtempSync(join(tmpdir(), "occam-ollama-app-"));
  try {
    const apps = join(root, "Applications");
    mkdirSync(join(apps, "Ollama.app", "Contents", "Resources"), { recursive: true });
    writeFileSync(join(apps, "Ollama.app", "Contents", "Resources", "ollama"), "#!/bin/sh\n");

    const exists = (p) => {
      // Map canonical /Applications paths onto the fixture tree.
      if (p === "/Applications/Ollama.app") return true;
      if (p === "/Applications/Ollama.app/Contents/Resources/ollama") return true;
      return false;
    };

    const signals = ollamaAppSignals({
      existsSync: exists,
      platform: "darwin",
      homedir: () => root,
    });
    assert.ok(signals.some((s) => s.includes("Ollama.app")));

    const det = detectOllama({
      existsSync: exists,
      which: () => null, // PATH may be empty under curl|bash
      platform: "darwin",
      homedir: () => root,
    });
    assert.equal(det.detected, true);
    assert.equal(det.kind, "MODEL_RUNTIME");
    assert.equal(det.name, "Ollama");
    assert.equal(det.confidence, "high");
    assert.ok(det.signals.some((s) => s.startsWith("app:")));

    // Must never inflate the connectable-app count.
    const section = renderDiscoverySection({
      candidates: [{ name: "Cursor" }, { name: "Claude Desktop" }],
      runtimes: [det],
    });
    assert.match(section, /Occam can connect to 2 apps/);
    assert.match(section, /Detected runtimes:/);
    assert.match(section, /Ollama/);
    assert.doesNotMatch(section, /Occam can connect to 3/);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
}

function testOllamaCppPathBinaryIsRuntimeNotHost() {
  const det = detectLlamaCpp({
    which: (name) => (name === "ollama.cpp" ? "/usr/local/bin/ollama.cpp" : null),
  });
  assert.equal(det.detected, true);
  assert.equal(det.kind, "MODEL_RUNTIME");
  assert.equal(det.name, "ollama.cpp");
}

function testDetectAllIncludesAppOnlyOllama() {
  const all = detectAllRuntimes({
    existsSync: (p) => p === "/Applications/Ollama.app",
    which: () => null,
    platform: "darwin",
    homedir: () => "/tmp/empty-home-no-dot-ollama",
  });
  assert.ok(all.some((r) => r.id === "ollama" && r.detected));
  assert.ok(all.every((r) => r.kind === "MODEL_RUNTIME"));
}

function main() {
  testNoOllamaHostAdapter();
  testOllamaAppBundleDetection();
  testOllamaCppPathBinaryIsRuntimeNotHost();
  testDetectAllIncludesAppOnlyOllama();
  console.log("runtimes.selftest: OK");
}

main();
