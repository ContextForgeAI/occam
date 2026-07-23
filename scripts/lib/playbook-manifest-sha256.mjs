#!/usr/bin/env node
/**
 * Compute SHA-256 for community playbook files and emit manifest.json row snippets.
 * Optional Ed25519 metadata stub (signature: null) — verify at load deferred to v1.1.
 */
import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";
import { basename, dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));

function printHelp() {
  process.stdout.write(`Usage: playbook-manifest-sha256 --file <community.json> [--id <id>] [--hosts h1,h2]

  --file <path>     Community playbook JSON under profiles/playbooks/community/
  --id <id>         Override manifest id (default: playbook.id or filename stem)
  --hosts <list>    Comma-separated hosts (default: playbook.hosts)
  --no-signature-stub   Omit signed_at / signer / signature stub fields

Outputs JSON manifest row to stdout.
`);
}

function parseArgs(argv) {
  let file = null;
  let id = null;
  let hosts = null;
  let signatureStub = true;
  let help = false;

  for (let i = 0; i < argv.length; i += 1) {
    const arg = argv[i];
    if (arg === "--help" || arg === "-h") {
      help = true;
      continue;
    }
    if (arg === "--file") {
      file = argv[++i] ?? null;
      continue;
    }
    if (arg.startsWith("--file=")) {
      file = arg.slice("--file=".length);
      continue;
    }
    if (arg === "--id") {
      id = argv[++i] ?? null;
      continue;
    }
    if (arg.startsWith("--id=")) {
      id = arg.slice("--id=".length);
      continue;
    }
    if (arg === "--hosts") {
      hosts = argv[++i] ?? null;
      continue;
    }
    if (arg.startsWith("--hosts=")) {
      hosts = arg.slice("--hosts=".length);
      continue;
    }
    if (arg === "--no-signature-stub") {
      signatureStub = false;
      continue;
    }
    throw new Error(`Unknown option: ${arg}`);
  }

  return { file, id, hosts, signatureStub, help };
}

function sha256Hex(content) {
  return createHash("sha256").update(content, "utf8").digest("hex");
}

/** LF-normalized — manifest rows match git eol=lf bytes (see .gitattributes). */
function sha256FileHex(filePath) {
  const raw = readFileSync(filePath, "utf8");
  return sha256Hex(raw.replace(/\r\n/g, "\n"));
}

function main() {
  const parsed = parseArgs(process.argv.slice(2));
  if (parsed.help) {
    printHelp();
    process.exit(0);
  }
  if (!parsed.file) {
    process.stderr.write("error: --file is required\n");
    printHelp();
    process.exit(2);
  }

  const filePath = resolve(parsed.file);
  const raw = readFileSync(filePath, "utf8");
  const doc = JSON.parse(raw);
  const fileName = basename(filePath);
  const playbookId = parsed.id ?? doc.id ?? fileName.replace(/\.json$/i, "");
  const hostList = parsed.hosts
    ? parsed.hosts.split(",").map((h) => h.trim()).filter(Boolean)
    : doc.hosts ?? [playbookId];

  const row = {
    id: playbookId,
    hosts: hostList,
    schema_version: doc.schema_version ?? "1.0",
    file: fileName,
    sha256: sha256FileHex(filePath),
  };

  if (parsed.signatureStub) {
    row.signed_at = new Date().toISOString().slice(0, 10);
    row.signer = "occam-maintainers-ed25519";
    row.signature = null;
  }

  process.stdout.write(`${JSON.stringify(row, null, 2)}\n`);
}

main();
