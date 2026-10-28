#!/usr/bin/env node
/**
 * Gate: `occam contract` / check-public-mcp-contract.mjs must default OCCAM_PROFILE
 * to full so the fingerprint corpus (18-tool catalog) matches the spawned host.
 * Product default is reader (11); leaving profile unset used to fail published 1.3.0.
 *
 * Marker: PUBLIC_MCP_CONTRACT_PROFILE_OK
 */
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { resolveContractHostProfile } from "./public-mcp-contract.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, "..", "..");
const checkScript = join(root, "scripts", "check-public-mcp-contract.mjs");
const hermesSmoke = join(root, "scripts", "hermes-smoke.mjs");

assert.equal(resolveContractHostProfile(undefined), "full");
assert.equal(resolveContractHostProfile(null), "full");
assert.equal(resolveContractHostProfile(""), "full");
assert.equal(resolveContractHostProfile("   "), "full");
assert.equal(resolveContractHostProfile("full"), "full");
assert.equal(resolveContractHostProfile("reader"), "reader");
assert.equal(resolveContractHostProfile(" reader "), "reader");

const checkSrc = readFileSync(checkScript, "utf8");
assert.match(
  checkSrc,
  /resolveContractHostProfile\(process\.env\.OCCAM_PROFILE\)/,
  "check-public-mcp-contract.mjs must resolve OCCAM_PROFILE via resolveContractHostProfile",
);
assert.match(
  checkSrc,
  /env:\s*contractHostEnv\(\)/,
  "all host spawns in check-public-mcp-contract.mjs must use contractHostEnv()",
);
assert.equal(
  (checkSrc.match(/env:\s*\{\s*\.\.\.process\.env,\s*OCCAM_HOME:/g) || []).length,
  0,
  "check-public-mcp-contract.mjs must not spawn the host without OCCAM_PROFILE via bare env spreads",
);

const hermesSrc = readFileSync(hermesSmoke, "utf8");
assert.match(
  hermesSrc,
  /OCCAM_PROFILE:\s*process\.env\.OCCAM_PROFILE\?\.trim\(\)\s*\|\|\s*"full"/,
  "hermes-smoke must keep OCCAM_PROFILE=full default (parity with occam contract)",
);

console.log("PUBLIC_MCP_CONTRACT_PROFILE_OK");
