#!/usr/bin/env bash
# RC.2 remote validation helper — macOS ARM64 (PR-H)
# Run from the Occam repository root after placing/publishing the osx-arm64 host.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
export OCCAM_HOME="${OCCAM_HOME:-$ROOT}"
export OCCAM_BANNER=0

HOST="${OCCAM_RC2_HOST:-$ROOT/artifacts/rc2/osx-arm64/OccamMcp.Core}"
OUT_DIR="${RC2_REMOTE_OUT:-$ROOT/artifacts/rc2/remote-macos}"
mkdir -p "$OUT_DIR"

fail() { echo "FAIL: $*" >&2; exit 1; }
ok() { echo "PASS: $*"; }

echo "== RC.2 macOS ARM64 validation =="
echo "host=$HOST"
echo "occam_home=$OCCAM_HOME"
echo "out=$OUT_DIR"
uname -a | tee "$OUT_DIR/uname.txt"

[[ -f "$HOST" ]] || fail "host binary missing: $HOST (publish on macOS: dotnet publish src/FFOccamMcp.Core -c Release -r osx-arm64 -o artifacts/rc2/osx-arm64)"
chmod +x "$HOST" || true

EXPECTED_SHA="${RC2_EXPECTED_SHA256:-}"
ACTUAL_SHA="$(shasum -a 256 "$HOST" | awk '{print $1}')"
echo "$ACTUAL_SHA  $(basename "$HOST")" | tee "$OUT_DIR/host.sha256"
if [[ -n "$EXPECTED_SHA" ]]; then
  [[ "$ACTUAL_SHA" == "$EXPECTED_SHA" ]] || fail "SHA-256 mismatch: expected=$EXPECTED_SHA actual=$ACTUAL_SHA"
  ok "artifact SHA-256 matches"
else
  echo "NOTE: RC2_EXPECTED_SHA256 unset; recorded actual hash only"
fi

# Lifecycle
"$HOST" lifecycle self | tee "$OUT_DIR/lifecycle-self.json"
grep -q '"ok":true' "$OUT_DIR/lifecycle-self.json" || fail "lifecycle self"
ok "lifecycle self"
"$HOST" lifecycle diagnose | tee "$OUT_DIR/lifecycle-diagnose.json"
grep -q '"ok":true' "$OUT_DIR/lifecycle-diagnose.json" || fail "lifecycle diagnose"
ok "lifecycle diagnose"

# Offline gates against this host
export OCCAM_RC2_HOST="$HOST"
dotnet run --project benchmarks/rc2-regression -c Release -- --pr-g | tee "$OUT_DIR/pr-g.txt"
grep -q '"failed":0' "$OUT_DIR/pr-g.txt" || fail "pr-g"
ok "focused pr-g"
dotnet run --project benchmarks/rc2-regression -c Release -- --regression | tee "$OUT_DIR/regression.txt"
grep -q '"failed":0' "$OUT_DIR/regression.txt" || fail "regression"
ok "cumulative regression"

# MCP stdio smoke via node helper embedded below
node --input-type=module - <<'EOF' | tee "$OUT_DIR/mcp-smoke.json"
import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';

const host = process.env.OCCAM_RC2_HOST;
const home = process.env.OCCAM_HOME;
const child = spawn(host, [], {
  env: { ...process.env, OCCAM_HOME: home, OCCAM_BANNER: '0' },
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
    const t = setTimeout(() => reject(new Error('timeout ' + method)), 120000);
    pending.set(reqId, (m) => {
      clearTimeout(t);
      resolve(m);
    });
    child.stdin.write(JSON.stringify({ jsonrpc: '2.0', id: reqId, method, params }) + '\n');
  });

await send('initialize', {
  protocolVersion: '2024-11-05',
  capabilities: {},
  clientInfo: { name: 'rc2-macos-remote', version: '1' },
});
child.stdin.write(JSON.stringify({ jsonrpc: '2.0', method: 'notifications/initialized' }) + '\n');
const listed = await send('tools/list', {});
const names = (listed.result?.tools || []).map((t) => t.name);

const call = async (name, args) => {
  const r = await send('tools/call', { name, arguments: args });
  const text = r.result?.content?.[0]?.text || '';
  try {
    return JSON.parse(text);
  } catch {
    return { parseError: true, text };
  }
};

const digest = await call('occam_digest', {
  urls: ['https://example.com/'],
  backend_policy: 'http',
  max_tokens: 400,
});
const probe = await call('occam_probe', { url: 'https://httpwg.org/specs/rfc9110.html' });
const focus = await call('occam_transcode', {
  url: 'https://example.com/',
  backend_policy: 'http',
  focus_query: 'Example Domain',
  max_tokens: 300,
});

const summary = {
  toolCount: names.length,
  hasDigest: names.includes('occam_digest'),
  digestOk: digest.ok === true || digest.failureCode === undefined,
  digestNativeArrayAccepted: digest.failureCode !== 'invalid_arguments',
  probeLikelyLogin: probe.likelyLoginRequired ?? probe.classification?.likelyLoginRequired ?? null,
  probeAccess: probe.access ?? null,
  focus: focus.focus ?? null,
  completeness: focus.completeness ?? null,
  transportOk: focus.transportOk ?? null,
  usable: focus.usable ?? null,
  ok: focus.ok ?? null,
};
console.log(JSON.stringify(summary, null, 2));
try {
  child.kill('SIGTERM');
} catch {}
setTimeout(() => process.exit(0), 500);
EOF

ok "mcp smoke completed — inspect $OUT_DIR/mcp-smoke.json"

BEFORE=$(pgrep -lf OccamMcp || true)
sleep 2
AFTER=$(pgrep -lf OccamMcp || true)
echo "processes_before<<EOF" | tee "$OUT_DIR/processes.txt"
echo "$BEFORE" | tee -a "$OUT_DIR/processes.txt"
echo "EOF" | tee -a "$OUT_DIR/processes.txt"
echo "processes_after<<EOF" | tee -a "$OUT_DIR/processes.txt"
echo "$AFTER" | tee -a "$OUT_DIR/processes.txt"
echo "EOF" | tee -a "$OUT_DIR/processes.txt"

echo "ALL_REMOTE_MACOS_STEPS_DONE"
echo "Collect $OUT_DIR for maintainer validation notes (private). Do not commit machine-local paths."
