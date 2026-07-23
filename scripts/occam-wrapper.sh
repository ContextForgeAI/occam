#!/usr/bin/env bash
# FF-Occam MCP — stdio launcher for Hermes / generic MCP hosts.
# Sets OCCAM_HOME, suppresses banner noise, execs launch-mcp-host.mjs.
set -euo pipefail

ROOT="${OCCAM_HOME:-$(cd "$(dirname "$0")/.." && pwd)}"
export OCCAM_HOME="$ROOT"
export Logging__LogLevel__Default=None
export WT_OCCAM_BANNER=0

# occam's Node workers need Node >= 20 (undici references the `File` global, added in Node 20). On a
# host whose default `node` is older (e.g. an EOL Node 18), the worker fetch path fails to load undici
# and every extraction returns a misleading `dns_error`. Prefer a newer node from a known location and
# prepend it to PATH so the AOT-spawned workers inherit it too. No-op when the default node is fine.
is_node20() { "$1" -e 'process.exit(+process.versions.node.split(".")[0] >= 20 ? 0 : 1)' >/dev/null 2>&1; }
if ! command -v node >/dev/null 2>&1 || ! is_node20 "$(command -v node)"; then
  for cand in "$HOME/.local/node20/bin" /opt/node20/bin /usr/local/bin; do
    if [ -x "$cand/node" ] && is_node20 "$cand/node"; then export PATH="$cand:$PATH"; break; fi
  done
fi

exec node "$ROOT/scripts/launch-mcp-host.mjs"
