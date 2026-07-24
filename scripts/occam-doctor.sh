#!/usr/bin/env bash
# FF-Occam MCP doctor — macOS / Linux (same checks as occam-doctor.ps1)
set -euo pipefail

SKIP_BUILD=0
for arg in "$@"; do
  case "$arg" in
    --skip-build) SKIP_BUILD=1 ;;
    -h | --help)
      echo "Usage: ./scripts/occam-doctor.sh [--skip-build]"
      exit 0
      ;;
  esac
done

ROOT="${OCCAM_HOME:-$(cd "$(dirname "$0")/.." && pwd)}"
export OCCAM_HOME="$ROOT"
CACHE_SCRIPT="$ROOT/scripts/lib/playwright-cache.mjs"

echo "FF-Occam MCP doctor (L0 skeleton)"
echo "OCCAM_HOME=$ROOT"

node "$ROOT/scripts/lib/assert-net10-csproj.mjs" "$ROOT"

if ! command -v node >/dev/null 2>&1; then
  echo "error: node not found on PATH" >&2
  exit 1
fi
echo "node: $(command -v node)"

if [[ ! -f "$ROOT/workers/package.json" ]]; then
  echo "error: missing workers/package.json" >&2
  exit 1
fi

if [[ ! -d "$ROOT/workers/node_modules" ]]; then
  echo "npm install (workspace root) ..."
  (cd "$ROOT/workers" && npm install --no-fund --no-audit)
fi

SKIP_PLAYWRIGHT_BUNDLED=0
CHANNEL="${OCCAM_BROWSER_CHANNEL:-}"
CHANNEL_LC="$(printf '%s' "$CHANNEL" | tr '[:upper:]' '[:lower:]')"
case "$CHANNEL_LC" in
  chrome | msedge | chrome-beta | msedge-beta)
    SKIP_PLAYWRIGHT_BUNDLED=1
    echo "playwright chromium: skip (OCCAM_BROWSER_CHANNEL=$CHANNEL_LC)"
    ;;
esac
if [[ -n "${OCCAM_BROWSER_EXECUTABLE_PATH:-}" || -n "${OCCAM_CHROME_PATH:-}" ]]; then
  SKIP_PLAYWRIGHT_BUNDLED=1
  echo "playwright chromium: skip (system executable path set)"
fi

if [[ -d "$ROOT/workers/browser-extract" && "$SKIP_PLAYWRIGHT_BUNDLED" -eq 0 ]]; then
  if node "$CACHE_SCRIPT" has-chromium; then
    echo "playwright chromium: already installed (skip)"
  else
    echo "playwright install chromium ..."
    (cd "$ROOT/workers/browser-extract" && npx playwright install chromium)
  fi
  # Chromium needs system shared libs (libnspr4/libnss3/libatk/…). The browser binary download
  # does NOT install them, and a cached browser skips the install above — so ensure OS deps
  # separately. Only on Linux as root (CI containers); idempotent. Dev machines (Win/macOS, or
  # non-root Linux) are untouched: install-deps needs root+apt and isn't needed there.
  if [[ "$(uname -s)" == "Linux" && "$(id -u)" == "0" ]]; then
    echo "playwright install-deps chromium (Linux root) ..."
    (cd "$ROOT/workers/browser-extract" && npx playwright install-deps chromium) \
      || echo "WARN: playwright install-deps failed (continuing; browser launch may fail without libnspr4 etc.)"
  fi
  if [[ -n "$(node "$CACHE_SCRIPT" path 2>/dev/null || true)" ]]; then
    echo "playwright cache: $(node "$CACHE_SCRIPT" path)"
  fi
fi

EGRESS_SELFTEST="$ROOT/workers/shared/lib/egress-proxy.selftest.mjs"
if [[ -n "${OCCAM_HTTP_PROXY:-}" || -n "${OCCAM_HTTPS_PROXY:-}" ]]; then
  echo "egress proxy env detected (OCCAM_HTTP_PROXY / OCCAM_HTTPS_PROXY)"
  if [[ -f "$EGRESS_SELFTEST" ]]; then
    echo "egress proxy module selftest ..."
    if ! node "$EGRESS_SELFTEST"; then
      echo "warning: egress-proxy selftest failed - verify proxy URL and OCCAM_NO_PROXY bypass list"
    fi
  fi
  echo "If transcode fails behind proxy, run full gate (L2_EGRESS_OK) or check corporate PAC/NTLM (v2 sidecar)."
fi

PDF_SELFTEST="$ROOT/workers/shared/lib/pdf-extract.selftest.mjs"
if [[ -f "$PDF_SELFTEST" ]]; then
  echo "pdf-extract module selftest ..."
  if ! (cd "$ROOT/workers/http-extract" && node "$PDF_SELFTEST"); then
    echo "warning: pdf-extract selftest failed - PDF transcode may be unavailable (is 'unpdf' installed?)"
  fi
fi

SSRF_SELFTEST="$ROOT/workers/shared/lib/private-ip.selftest.mjs"
if [[ -f "$SSRF_SELFTEST" ]]; then
  echo "private-ip (SSRF guard) module selftest ..."
  if ! (cd "$ROOT/workers/http-extract" && node "$SSRF_SELFTEST"); then
    echo "warning: private-ip selftest failed - SSRF/private-URL protection may be degraded"
  fi
fi

if [[ -d "$ROOT/workers/browser-extract" ]]; then
  echo "browser launch smoke ..."
  (cd "$ROOT/workers/browser-extract" && node lib/verify-browser-launch.mjs) || {
    echo "error: browser launch smoke failed" >&2
    exit 1
  }
fi

VERIFY_MANIFEST="$ROOT/scripts/lib/verify-community-manifest.mjs"
if [[ -f "$VERIFY_MANIFEST" ]]; then
  echo "community manifest sha256 verify ..."
  node "$VERIFY_MANIFEST" || {
    echo "error: verify-community-manifest failed" >&2
    exit 1
  }
fi

if [[ "$SKIP_BUILD" -eq 0 ]]; then
  RID="$(node "$ROOT/scripts/lib/resolve-rid.mjs")"
  echo "dotnet publish (RID=$RID) ..."
  dotnet publish "$ROOT/src/FFOccamMcp.Core/FFOccamMcp.Core.csproj" -c Release -r "$RID"
  PUBLISH_BIN="$ROOT/src/FFOccamMcp.Core/bin/Release/net10.0/$RID/publish/OccamMcp.Core"
  if [[ ! -f "$PUBLISH_BIN" ]]; then
    echo "error: dotnet publish did not produce $PUBLISH_BIN" >&2
    node "$ROOT/scripts/lib/assert-host-binary.mjs" "$ROOT"
    exit 1
  fi
  echo "mcp host: $PUBLISH_BIN"
  cp -f "$PUBLISH_BIN" "$ROOT/OccamMcp.Core"
  chmod +x "$ROOT/OccamMcp.Core"
  echo "mcp host (OCCAM_HOME root): $ROOT/OccamMcp.Core"
fi

if [[ "$SKIP_BUILD" -eq 1 ]]; then
  node "$ROOT/scripts/lib/assert-host-binary.mjs" "$ROOT" --skip-build
else
  node "$ROOT/scripts/lib/assert-host-binary.mjs" "$ROOT"
fi

echo "doctor: OK"
SESSIONS_ROOT="${OCCAM_SESSIONS_ROOT:-$HOME/.occam/sessions}"
echo "sessions: $SESSIONS_ROOT (optional: node scripts/occam-session.mjs init)"
echo ""
echo "MCP host ready. Wire any MCP client (Cursor, Claude Desktop, Hermes, …):"
echo "  occam onboard"
echo "  # or: node scripts/lib/print-connection-snippet.mjs \"$ROOT\" generic-stdio"
echo ""
echo "Canonical launcher: node scripts/launch-mcp-host.mjs with OCCAM_HOME=$ROOT"
echo "Avoid on git clone: packages/occam-mcp/bin/occam-mcp.js without OCCAM_HOME (npx/release path)."
echo "Reload MCP servers in your host after saving config."
