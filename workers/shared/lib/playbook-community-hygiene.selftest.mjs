import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { readFileSync, readdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");
const communityDir = join(root, "profiles", "playbooks", "community");

function sha256Hex(content) {
  return createHash("sha256").update(content, "utf8").digest("hex");
}

function sha256FileHex(filePath) {
  const raw = readFileSync(filePath, "utf8");
  return sha256Hex(raw.replace(/\r\n/g, "\n"));
}

const FORBIDDEN_KEYS = new Set([
  "cookie",
  "cookies",
  "authorization",
  "set-cookie",
  "set_cookie",
  "bearer",
  "bearer_token",
  "api_key",
  "apikey",
  "password",
  "secret_key",
  "session_token",
  "access_token",
  "refresh_token",
]);

function walk(value) {
  if (value === null || typeof value !== "object") {
    return false;
  }
  if (Array.isArray(value)) {
    return value.some(walk);
  }
  for (const [key, child] of Object.entries(value)) {
    if (FORBIDDEN_KEYS.has(key.toLowerCase())) {
      return true;
    }
    if (walk(child)) {
      return true;
    }
  }
  return false;
}

function containsForbiddenKeys(json) {
  try {
    return walk(JSON.parse(json));
  } catch {
    return true;
  }
}

const manifest = JSON.parse(readFileSync(join(communityDir, "manifest.json"), "utf8"));
assert.equal(manifest.schema_version, "1.0");
assert.ok(Array.isArray(manifest.playbooks) && manifest.playbooks.length >= 2);

for (const entry of manifest.playbooks) {
  const filePath = join(communityDir, entry.file);
  const json = readFileSync(filePath, "utf8");
  assert.ok(!containsForbiddenKeys(json), `community playbook must not contain forbidden keys: ${entry.file}`);
  const doc = JSON.parse(json);
  assert.equal(doc.id, entry.id);
  assert.deepEqual(doc.hosts, entry.hosts);
  assert.ok(entry.sha256 && entry.sha256.length === 64, `manifest row must include sha256: ${entry.id}`);
  assert.equal(sha256FileHex(filePath), entry.sha256.toLowerCase(), `sha256 mismatch: ${entry.file}`);
}

const playbookFiles = readdirSync(communityDir).filter(
  (name) => name.endsWith(".json") && name !== "manifest.json",
);
assert.equal(playbookFiles.length, manifest.playbooks.length, "manifest lists every community playbook");

const badDraft = JSON.stringify({
  schema_version: "1.0",
  id: "evil.example",
  hosts: ["evil.example"],
  request: { headers: { Cookie: "session=abc" } },
});
assert.ok(containsForbiddenKeys(badDraft), "hygiene rejects cookie header keys");

const goodDraft = JSON.stringify({
  schema_version: "1.0",
  id: "good.example",
  hosts: ["good.example"],
  agent_notes: "No secrets here.",
});
assert.ok(!containsForbiddenKeys(goodDraft), "hygiene accepts sanitized playbook");

console.log("playbook-community-hygiene.selftest: OK");
