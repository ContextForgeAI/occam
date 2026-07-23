#!/usr/bin/env bash
# RC.2 remote validation helper — Linux x64 / Hermes-neutral MCP (PR-H)
# Does not invent Hermes APIs. Uses stdio MCP + lifecycle CLI only.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
export OCCAM_HOME="${OCCAM_HOME:-$ROOT}"
export OCCAM_BANNER=0

HOST="${OCCAM_RC2_HOST:-$ROOT/artifacts/rc2/linux-x64/OccamMcp.Core}"
OUT_DIR="${RC2_REMOTE_OUT:-$ROOT/artifacts/rc2/remote-linux}"
mkdir -p "$OUT_DIR"

fail() { echo "FAIL: $*" >&2; exit 1; }
ok() { echo "PASS: $*"; }

echo "== RC.2 Linux x64 Hermes/OpenRouter-neutral validation =="
echo "host=$HOST"
echo "occam_home=$OCCAM_HOME"
echo "out=$OUT_DIR"
uname -a | tee "$OUT_DIR/uname.txt"
echo "NOTE: Hermes/OpenRouter integration boundary is external. This script validates Occam MCP + lifecycle only."

[[ -f "$HOST" ]] || fail "host binary missing: $HOST (publish on Linux: dotnet publish src/FFOccamMcp.Core -c Release -r linux-x64 -o artifacts/rc2/linux-x64)"
chmod +x "$HOST" || true

EXPECTED_SHA="${RC2_EXPECTED_SHA256:-}"
ACTUAL_SHA="$(sha256sum "$HOST" | awk '{print $1}')"
echo "$ACTUAL_SHA  $(basename "$HOST")" | tee "$OUT_DIR/host.sha256"
if [[ -n "$EXPECTED_SHA" ]]; then
  [[ "$ACTUAL_SHA" == "$EXPECTED_SHA" ]] || fail "SHA-256 mismatch: expected=$EXPECTED_SHA actual=$ACTUAL_SHA"
  ok "artifact SHA-256 matches"
else
  echo "NOTE: RC2_EXPECTED_SHA256 unset; recorded actual hash only"
fi

"$HOST" lifecycle self | tee "$OUT_DIR/lifecycle-self.json"
grep -q '"ok":true' "$OUT_DIR/lifecycle-self.json" || fail "lifecycle self"
ok "lifecycle self"
"$HOST" lifecycle diagnose | tee "$OUT_DIR/lifecycle-diagnose.json"
grep -q '"ok":true' "$OUT_DIR/lifecycle-diagnose.json" || fail "lifecycle diagnose"
ok "lifecycle diagnose"

export OCCAM_RC2_HOST="$HOST"
dotnet run --project benchmarks/rc2-regression -c Release -- --pr-g | tee "$OUT_DIR/pr-g.txt"
grep -q '"failed":0' "$OUT_DIR/pr-g.txt" || fail "pr-g"
ok "focused pr-g"
dotnet run --project benchmarks/rc2-regression -c Release -- --regression | tee "$OUT_DIR/regression.txt"
grep -q '"failed":0' "$OUT_DIR/regression.txt" || fail "regression"
ok "cumulative regression"

# Exact-child shutdown check using launcher if present
if [[ -f scripts/launch-mcp-host.mjs ]]; then
  node --input-type=module - <<'EOF' | tee "$OUT_DIR/launcher-child.json"
import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';
import path from 'node:path';

const root = process.cwd();
const launcher = path.join(root, 'scripts', 'launch-mcp-host.mjs');
const child = spawn(process.execPath, [launcher], {
  env: {
    ...process.env,
    OCCAM_HOME: root,
    OCCAM_BANNER: '0',
    OCCAM_FORCE_DOTNET_RUN: '0',
  },
  stdio: ['pipe', 'pipe', 'pipe'],
});
const rl = createInterface({ input: child.stdout });
let id = 0;
const pending = new Map();
rl.on('line', (l) => {
  try {
    const m = JSON.parse(l);
    if (m.id != null && pending.has(m.id)) pending.get(m.id)(m);
  } catch {}
});
const send = (method, params) =>
  new Promise((resolve, reject) => {
    const reqId = ++id;
    const t = setTimeout(() => reject(new Error('timeout ' + method)), 60000);
    pending.set(reqId, (m) => {
      clearTimeout(t);
      resolve(m);
    });
    child.stdin.write(JSON.stringify({ jsonrpc: '2.0', id: reqId, method, params }) + '\n');
  });

await send('initialize', {
  protocolVersion: '2024-11-05',
  capabilities: {},
  clientInfo: { name: 'rc2-linux-remote', version: '1' },
});
child.stdin.write(JSON.stringify({ jsonrpc: '2.0', method: 'notifications/initialized' }) + '\n');
const listed = await send('tools/list', {});
const names = (listed.result?.tools || []).map((t) => t.name);
const digest = await send('tools/call', {
  name: 'occam_digest',
  arguments: { urls: ['https://example.com/'], backend_policy: 'http', max_tokens: 300 },
});
const text = digest.result?.content?.[0]?.text || '';
let parsed = null;
try {
  parsed = JSON.parse(text);
} catch {}
console.log(
  JSON.stringify(
    {
      launcherPid: child.pid,
      toolCount: names.length,
      digestOk: parsed?.ok ?? null,
      failureCode: parsed?.failureCode ?? parsed?.failure?.code ?? null,
      semantic: {
        focus: parsed?.focus ?? null,
        completeness: parsed?.completeness ?? null,
        transportOk: parsed?.transportOk ?? null,
        usable: parsed?.usable ?? null,
      },
    },
    null,
    2,
  ),
);
child.kill('SIGTERM');
await new Promise((r) => setTimeout(r, 1500));
process.exit(0);
EOF
  ok "launcher MCP smoke"
else
  echo "NOTE: launch-mcp-host.mjs missing; skipped launcher path"
fi

# Process-tree snapshot (informational)
{
  echo "=== pstree self ==="
  pstree -ap "$$" 2>/dev/null || ps -ef | head -n 5
  echo "=== OccamMcp processes ==="
  pgrep -af OccamMcp || true
} | tee "$OUT_DIR/process-tree.txt"

echo "ALL_REMOTE_LINUX_STEPS_DONE"
echo "Collect $OUT_DIR for maintainer validation notes (private). Do not commit machine-local paths."
echo "Hermes/OpenRouter: configure the external client to launch this host over stdio MCP; do not invent Occam-side Hermes APIs."
