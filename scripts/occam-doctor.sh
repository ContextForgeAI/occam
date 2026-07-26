#!/usr/bin/env bash
# Occam doctor — macOS / Linux (same checks as occam-doctor.ps1)
set -euo pipefail

SKIP_BUILD=0
QUIET=0
if [[ "${OCCAM_INSTALL_QUIET:-}" == "1" || "${OCCAM_INSTALL_QUIET:-}" == "true" ]]; then
  QUIET=1
fi
for arg in "$@"; do
  case "$arg" in
    --skip-build) SKIP_BUILD=1 ;;
    --quiet) QUIET=1 ;;
    -h | --help)
      echo "Usage: ./scripts/occam-doctor.sh [--skip-build] [--quiet]"
      exit 0
      ;;
  esac
done

ROOT="${OCCAM_HOME:-$(cd "$(dirname "$0")/.." && pwd)}"
export OCCAM_HOME="$ROOT"
CACHE_SCRIPT="$ROOT/scripts/lib/playwright-cache.mjs"

doctor_echo() {
  if [[ "$QUIET" -eq 0 ]]; then
    echo "$@"
  fi
}

run_quiet_ok() {
  # Run command; on failure re-run with output visible.
  if [[ "$QUIET" -eq 1 ]]; then
    if ! "$@" >/dev/null 2>&1; then
      "$@" || return $?
    fi
  else
    "$@"
  fi
}

doctor_echo "Occam doctor"
doctor_echo "OCCAM_HOME=$ROOT"

node "$ROOT/scripts/lib/assert-net10-csproj.mjs" "$ROOT"

if ! command -v node >/dev/null 2>&1; then
  echo "error: node not found on PATH" >&2
  exit 1
fi
doctor_echo "node: $(command -v node)"

if [[ ! -f "$ROOT/workers/package.json" ]]; then
  echo "error: missing workers/package.json" >&2
  exit 1
fi

if [[ ! -d "$ROOT/workers/node_modules" ]]; then
  doctor_echo "npm install (workspace root) ..."
  if [[ "$QUIET" -eq 1 ]]; then
    (cd "$ROOT/workers" && npm install --no-fund --no-audit --silent)
  else
    (cd "$ROOT/workers" && npm install --no-fund --no-audit)
  fi
fi

SKIP_PLAYWRIGHT_BUNDLED=0
CHANNEL="${OCCAM_BROWSER_CHANNEL:-}"
CHANNEL_LC="$(printf '%s' "$CHANNEL" | tr '[:upper:]' '[:lower:]')"
case "$CHANNEL_LC" in
  chrome | msedge | chrome-beta | msedge-beta)
    SKIP_PLAYWRIGHT_BUNDLED=1
    doctor_echo "playwright chromium: skip (OCCAM_BROWSER_CHANNEL=$CHANNEL_LC)"
    ;;
esac
if [[ -n "${OCCAM_BROWSER_EXECUTABLE_PATH:-}" || -n "${OCCAM_CHROME_PATH:-}" ]]; then
  SKIP_PLAYWRIGHT_BUNDLED=1
  doctor_echo "playwright chromium: skip (system executable path set)"
fi

if [[ -d "$ROOT/workers/browser-extract" && "$SKIP_PLAYWRIGHT_BUNDLED" -eq 0 ]]; then
  if [[ "$(uname -s)" == "Linux" && "$(id -u)" == "0" ]]; then
    doctor_echo "playwright install-deps chromium (Linux root) ..."
    (cd "$ROOT/workers/browser-extract" && npx playwright install-deps chromium) \
      || echo "WARN: playwright install-deps failed (continuing; browser launch may fail without libnspr4 etc.)"
  fi
  if [[ -n "$(node "$CACHE_SCRIPT" path 2>/dev/null || true)" ]]; then
    doctor_echo "playwright cache: $(node "$CACHE_SCRIPT" path)"
  fi
fi

EGRESS_SELFTEST="$ROOT/workers/shared/lib/egress-proxy.selftest.mjs"
if [[ -n "${OCCAM_HTTP_PROXY:-}" || -n "${OCCAM_HTTPS_PROXY:-}" ]]; then
  doctor_echo "egress proxy env detected (OCCAM_HTTP_PROXY / OCCAM_HTTPS_PROXY)"
  if [[ -f "$EGRESS_SELFTEST" ]]; then
    doctor_echo "egress proxy module selftest ..."
    if ! run_quiet_ok node "$EGRESS_SELFTEST"; then
      echo "warning: egress-proxy selftest failed - verify proxy URL and OCCAM_NO_PROXY bypass list"
    fi
  fi
  doctor_echo "If transcode fails behind proxy, run full gate (L2_EGRESS_OK) or check corporate PAC/NTLM (v2 sidecar)."
fi

PDF_SELFTEST="$ROOT/workers/shared/lib/pdf-extract.selftest.mjs"
if [[ -f "$PDF_SELFTEST" ]]; then
  doctor_echo "pdf-extract module selftest ..."
  if ! (cd "$ROOT/workers/http-extract" && run_quiet_ok node "$PDF_SELFTEST"); then
    echo "warning: pdf-extract selftest failed - PDF transcode may be unavailable (is 'unpdf' installed?)"
  fi
fi

SSRF_SELFTEST="$ROOT/workers/shared/lib/private-ip.selftest.mjs"
if [[ -f "$SSRF_SELFTEST" ]]; then
  doctor_echo "private-ip (SSRF guard) module selftest ..."
  if ! (cd "$ROOT/workers/http-extract" && run_quiet_ok node "$SSRF_SELFTEST"); then
    echo "warning: private-ip selftest failed - SSRF/private-URL protection may be degraded"
  fi
fi

if [[ -d "$ROOT/workers/browser-extract" ]]; then
  # Single source path — quiet vs verbose only changes output, not probe count.
  CHROMIUM_LAUNCH_PROBE="$ROOT/workers/browser-extract/lib/ensure-chromium-usable.mjs"
  doctor_echo "browser runtime check (launch probe) ..."
  if [[ "$QUIET" -eq 1 ]]; then
    if ! (cd "$ROOT/workers/browser-extract" && node "$CHROMIUM_LAUNCH_PROBE" >/dev/null 2>&1); then
      echo "error: browser runtime unavailable" >&2
      (cd "$ROOT/workers/browser-extract" && node "$CHROMIUM_LAUNCH_PROBE") || exit 1
    fi
  else
    (cd "$ROOT/workers/browser-extract" && node "$CHROMIUM_LAUNCH_PROBE") || {
      echo "error: browser runtime unavailable" >&2
      exit 1
    }
  fi
fi

VERIFY_MANIFEST="$ROOT/scripts/lib/verify-community-manifest.mjs"
if [[ -f "$VERIFY_MANIFEST" ]]; then
  doctor_echo "community manifest sha256 verify ..."
  if [[ "$QUIET" -eq 1 ]]; then
    node "$VERIFY_MANIFEST" >/dev/null 2>&1 || {
      echo "error: verify-community-manifest failed" >&2
      node "$VERIFY_MANIFEST" || exit 1
    }
  else
    node "$VERIFY_MANIFEST" || {
      echo "error: verify-community-manifest failed" >&2
      exit 1
    }
  fi
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
  doctor_echo "mcp host: $PUBLISH_BIN"
  cp -f "$PUBLISH_BIN" "$ROOT/OccamMcp.Core"
  chmod +x "$ROOT/OccamMcp.Core"
  doctor_echo "mcp host (OCCAM_HOME root): $ROOT/OccamMcp.Core"
fi

if [[ "$SKIP_BUILD" -eq 1 ]]; then
  node "$ROOT/scripts/lib/assert-host-binary.mjs" "$ROOT" --skip-build
else
  node "$ROOT/scripts/lib/assert-host-binary.mjs" "$ROOT"
fi

doctor_echo "doctor: OK"
SESSIONS_ROOT="${OCCAM_SESSIONS_ROOT:-$HOME/.occam/sessions}"
doctor_echo "sessions: $SESSIONS_ROOT (optional: node scripts/occam-session.mjs init)"
if [[ "$QUIET" -eq 0 ]]; then
  echo ""
  echo "Occam runtime is installed (self-check via doctor passed)."
  echo "Connect an AI app:  occam connect"
  echo "Manual snippet:     node scripts/lib/print-connection-snippet.mjs \"$ROOT\" generic-stdio"
  echo ""
  echo "Canonical launcher: node scripts/launch-mcp-host.mjs with OCCAM_HOME=$ROOT"
fi
