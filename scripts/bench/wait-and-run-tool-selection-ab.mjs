/**
 * Wait for .secrets/openrouter.env (gitignored), then run tool-selection A/B.
 * Never prints the key. Writes aggregate results to artifacts/.
 */
import fs from "fs";
import path from "path";
import { spawn } from "child_process";
import { fileURLToPath } from "url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..");
const secretsPath = path.join(root, ".secrets", "openrouter.env");
const outDir = path.join(root, "artifacts");
const abScript = path.join(root, "scripts", "bench", "tool-selection-ab.mjs");

function loadKey() {
  if (!fs.existsSync(secretsPath)) return null;
  const text = fs.readFileSync(secretsPath, "utf8");
  for (const line of text.split(/\r?\n/)) {
    const t = line.trim();
    if (!t || t.startsWith("#")) continue;
    const m = t.match(/^OPENROUTER_API_KEY\s*=\s*(.+)$/);
    if (!m) continue;
    let v = m[1].trim().replace(/^['"]|['"]$/g, "");
    if (v.startsWith("sk-") && v.length > 10) return v;
  }
  return null;
}

const maxWaitMs = Number(process.env.AB_WAIT_MS || 25 * 60 * 1000);
const pollMs = 5000;
const started = Date.now();

console.error(`Waiting for ${secretsPath} (OPENROUTER_API_KEY=sk-...)`);
console.error("Do not put the key in chat. Write the file, then this runner continues.");

while (Date.now() - started < maxWaitMs) {
  const key = loadKey();
  if (key) {
    console.error("KEY_READY — starting A/B (AB_SAMPLES=3)");
    fs.mkdirSync(outDir, { recursive: true });
    const stamp = new Date().toISOString().replace(/[:.]/g, "-").slice(0, 19);
    const outFile = path.join(outDir, `tool-selection-ab-${stamp}.txt`);
    const child = spawn(process.execPath, [abScript], {
      cwd: root,
      env: {
        ...process.env,
        OPENROUTER_API_KEY: key,
        AB_SAMPLES: process.env.AB_SAMPLES || "3",
      },
      stdio: ["ignore", "pipe", "pipe"],
    });
    let buf = "";
    child.stdout.on("data", (d) => {
      const s = d.toString();
      buf += s;
      process.stdout.write(s);
    });
    child.stderr.on("data", (d) => {
      const s = d.toString();
      buf += s;
      process.stderr.write(s);
    });
    child.on("exit", (code) => {
      fs.writeFileSync(outFile, buf, "utf8");
      console.error(`RESULTS_FILE=${outFile}`);
      console.error(`AB_EXIT=${code ?? 1}`);
      process.exit(code ?? 1);
    });
    break;
  }
  await new Promise((r) => setTimeout(r, pollMs));
}

if (!loadKey()) {
  console.error("KEY_TIMEOUT — create .secrets/openrouter.env and re-run");
  process.exit(2);
}
