#!/usr/bin/env bash
# FF-Occam MCP — stdio launcher for Hermes / generic MCP hosts.
# Sets OCCAM_HOME, suppresses banner noise, execs launch-mcp-host.mjs with a
# GUI-PATH-safe absolute Node (runtime/node-bin or OCCAM_NODE_BIN when set).
set -euo pipefail

ROOT="${OCCAM_HOME:-$(cd "$(dirname "$0")/.." && pwd)}"
export OCCAM_HOME="$ROOT"
export Logging__LogLevel__Default=None
export WT_OCCAM_BANNER=0

is_node20() { "$1" -e 'process.exit(+process.versions.node.split(".")[0] >= 20 ? 0 : 1)' >/dev/null 2>&1; }

resolve_node() {
  if [ -n "${OCCAM_NODE_BIN:-}" ] && [ -x "$OCCAM_NODE_BIN" ]; then
    printf '%s\n' "$OCCAM_NODE_BIN"
    return 0
  fi
  if [ -f "$ROOT/runtime/node-bin" ]; then
    local recorded
    recorded="$(grep -v '^[[:space:]]*#' "$ROOT/runtime/node-bin" | head -n1 | tr -d '\r' | sed 's/[[:space:]]*$//')"
    if [ -n "$recorded" ] && [ -x "$recorded" ]; then
      printf '%s\n' "$recorded"
      return 0
    fi
  fi
  if command -v node >/dev/null 2>&1 && is_node20 "$(command -v node)"; then
    command -v node
    return 0
  fi
  local cand
  for cand in "$HOME/.local/node20/bin/node" /opt/homebrew/bin/node /usr/local/bin/node /opt/node20/bin/node /usr/bin/node; do
    if [ -x "$cand" ] && is_node20 "$cand"; then
      printf '%s\n' "$cand"
      return 0
    fi
  done
  return 1
}

NODE_BIN="$(resolve_node || true)"
if [ -z "${NODE_BIN:-}" ]; then
  echo "Occam's Node runtime is no longer available." >&2
  echo "Reinstall Occam or set OCCAM_NODE_BIN to a working Node 20+ executable." >&2
  if [ -f "$ROOT/runtime/node-bin" ]; then
    echo "Recorded path was: $(head -n1 "$ROOT/runtime/node-bin" | tr -d '\r')" >&2
  fi
  exit 1
fi

export OCCAM_NODE_BIN="$NODE_BIN"
export PATH="$(dirname "$NODE_BIN"):${PATH:-/usr/bin:/bin}"

exec "$NODE_BIN" "$ROOT/scripts/launch-mcp-host.mjs"
