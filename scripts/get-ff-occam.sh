#!/usr/bin/env bash
# FF-Occam MCP — Level B one-liner bootstrap (curl | bash).
# Target: Node 20+ only — NO git, NO .NET SDK on the install machine.
#
#   curl -fsSL "$OCCAM_GET_URL" | bash
#   curl -fsSL "$OCCAM_GET_URL" | OCCAM_HOST=cursor OCCAM_RELEASE_ALLOW_HTTP=1 bash
#
# Flow: product welcome → auto|manual → download → doctor → onboard → connection snippet
set -euo pipefail

ROOT_DIR=""
if [[ -n "${BASH_SOURCE[0]:-}" ]] && [[ -f "${BASH_SOURCE[0]}" ]]; then
  ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fi

# Map the host OS/arch to a .NET RID (same set the npx wrapper + build-release.mjs use).
# OCCAM_RID overrides; unknown platforms fall back to linux-x64 (the historical default).
detect_rid() {
  local os arch
  os="$(uname -s 2>/dev/null || echo Linux)"
  arch="$(uname -m 2>/dev/null || echo x86_64)"
  case "$os" in
    Darwin)
      case "$arch" in
        arm64|aarch64) echo "osx-arm64" ;;
        *)             echo "osx-x64" ;;
      esac
      ;;
    MINGW*|MSYS*|CYGWIN*|Windows_NT) echo "win-x64" ;;
    *) echo "linux-x64" ;;
  esac
}

VERSION="${OCCAM_VERSION:-1.0.0-rc.2}"
RID="${OCCAM_RID:-$(detect_rid)}"
INSTALL_DIR="${OCCAM_INSTALL_DIR:-$HOME/.local/share/ff-occam}"
HOST_TARGET="${OCCAM_HOST:-hermes}"
ALLOW_HTTP="${OCCAM_RELEASE_ALLOW_HTTP:-0}"
SETUP_MODE="${OCCAM_SETUP:-}"

RELEASE_BASE="${OCCAM_RELEASE_BASE:-https://github.com/ContextForgeAI/occam/releases/download/v${VERSION}}"
RELEASE_URL="${OCCAM_RELEASE_URL:-${RELEASE_BASE}/ff-occam-${VERSION}-${RID}.tar.gz}"
MANIFEST_URL="${OCCAM_RELEASE_MANIFEST_URL:-${RELEASE_BASE}/ff-occam-${VERSION}-${RID}-manifest.json}"

MIN_NODE_MAJOR=20
WIDTH=52

use_color() {
  [[ -t 1 ]] && [[ "${OCCAM_NO_COLOR:-0}" != "1" ]]
}

c_gray() { use_color && printf '\033[38;5;244m%s\033[0m' "$1" || printf '%s' "$1"; }
c_white() { use_color && printf '\033[38;5;255m%s\033[0m' "$1" || printf '%s' "$1"; }
c_cyan() { use_color && printf '\033[38;5;45m%s\033[0m' "$1" || printf '%s' "$1"; }
c_green() { use_color && printf '\033[38;5;46m%s\033[0m' "$1" || printf '%s' "$1"; }

print_product_welcome() {
  if [[ -n "$ROOT_DIR" ]] && [[ -f "$ROOT_DIR/scripts/lib/operator/get-install-welcome.mjs" ]]; then
    node "$ROOT_DIR/scripts/lib/operator/get-install-welcome.mjs" print
    return
  fi
  echo ""
  c_cyan "  FF-Occam MCP"
  c_gray "$(printf '─%.0s' $(seq 1 "$WIDTH"))"
  printf "  "; c_gray "ARCHITECTURE"; printf "   "; c_white ".NET 10 Core (Native AOT)"; echo ""
  printf "  "; c_gray "MODE"; printf "           "; c_white "L0 extract-only"; echo ""
  printf "  "; c_gray "WORKERS"; printf "        "; c_white "Node http + browser"; echo ""
  c_gray "$(printf '─%.0s' $(seq 1 "$WIDTH"))"
  printf "  "; c_green "✓"; printf " "; c_gray "Extract"; printf "      "; c_white "Live only"; echo ""
  printf "  "; c_green "✓"; printf " "; c_gray "Tools"; printf "        "; c_white "14 occam_*"; echo ""
  printf "  "; c_green "✓"; printf " "; c_gray "Playbooks"; printf "    "; c_white "seeds + heal/save"; echo ""
  c_gray "$(printf '─%.0s' $(seq 1 "$WIDTH"))"
  c_gray "  One URL → honest Markdown. Typed failures, no file cache."
  echo ""
}

resolve_setup_mode() {
  if [[ -n "$SETUP_MODE" ]]; then
    case "$SETUP_MODE" in
      auto|1) SETUP_MODE=auto ;;
      manual|2) SETUP_MODE=manual ;;
      *)
        echo "error: OCCAM_SETUP must be auto or manual (got $SETUP_MODE)" >&2
        exit 1
        ;;
    esac
    echo "setup: $SETUP_MODE (from OCCAM_SETUP)"
    return
  fi

  if [[ -n "$ROOT_DIR" ]] && [[ -f "$ROOT_DIR/scripts/lib/operator/get-install-welcome.mjs" ]]; then
    if [[ -t 0 ]] && [[ -t 1 ]]; then
      SETUP_MODE="$(node "$ROOT_DIR/scripts/lib/operator/get-install-welcome.mjs" prompt | tail -n1)"
    else
      SETUP_MODE="$(node "$ROOT_DIR/scripts/lib/operator/get-install-welcome.mjs" resolve | tail -n1)"
      echo "setup: $SETUP_MODE"
    fi
    return
  fi

  if [[ ! -t 0 ]] || [[ ! -t 1 ]]; then
    SETUP_MODE=auto
    echo "setup: auto (non-interactive pipe — set OCCAM_SETUP=manual to override)"
    return
  fi

  echo ""
  c_white "  First-run setup"
  c_gray "$(printf '─%.0s' $(seq 1 "$WIDTH"))"
  echo "  Install the release bundle, then wire your MCP host."
  echo "  [1] Auto   — defaults from OCCAM_HOST (default: hermes)"
  echo "  [2] Manual — guided wizard (occam-onboard)"
  echo ""
  printf "  › Setup [1]: "
  read -r choice
  choice="${choice:-1}"
  case "$choice" in
    2|manual|Manual|MANUAL) SETUP_MODE=manual ;;
    *) SETUP_MODE=auto ;;
  esac
  echo ""
  echo "setup: $SETUP_MODE"
}

need_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "error: required command not found: $1" >&2
    exit 1
  fi
}

check_node() {
  need_cmd node
  local major
  major="$(node -p "process.versions.node.split('.')[0]")"
  if [[ "$major" -lt "$MIN_NODE_MAJOR" ]]; then
    echo "error: Node.js ${MIN_NODE_MAJOR}+ required (found $(node -v))" >&2
    exit 1
  fi
  echo "node: $(node -v)"
}

assert_url_scheme() {
  local url="$1"
  case "$url" in
    https://*) return 0 ;;
    http://*)
      if [[ "$ALLOW_HTTP" == "1" ]]; then
        echo "warning: OCCAM_RELEASE_ALLOW_HTTP=1 — HTTP release URL (LAN/trusted forge only)" >&2
        return 0
      fi
      echo "error: release URL must be HTTPS, or set OCCAM_RELEASE_ALLOW_HTTP=1" >&2
      exit 1
      ;;
    *)
      echo "error: invalid release URL: $url" >&2
      exit 1
      ;;
  esac
}

sha256_file() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | awk '{print $1}'
  elif command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$1" | awk '{print $1}'
  else
    echo "error: sha256sum or shasum required" >&2
    exit 1
  fi
}

json_field() {
  node -e "
    const fs = require('fs');
    const j = JSON.parse(fs.readFileSync(process.argv[1], 'utf8'));
    const v = j[process.argv[2]];
    if (v == null || v === '') process.exit(2);
    process.stdout.write(String(v));
  " "$1" "$2"
}

download() {
  local url="$1" dest="$2"
  assert_url_scheme "$url"
  echo "download: $url"
  if ! curl -fsSL "$url" -o "$dest"; then
    echo "" >&2
    echo "error: download failed — is the release tarball published?" >&2
    echo "  url: $url" >&2
    echo "  maintainer: tag v${VERSION} and ensure GitHub Release assets exist" >&2
    echo "  see: INSTALL.md" >&2
    exit 1
  fi
}

install_release() {
  local tmp
  tmp="$(mktemp -d "${TMPDIR:-/tmp}/ff-occam-get.XXXXXX")"
  trap 'rm -rf "$tmp"' EXIT

  local manifest_path="$tmp/manifest.json"
  local tarball_path="$tmp/release.tar.gz"

  download "$MANIFEST_URL" "$manifest_path"
  local expected_sha rid manifest_version
  expected_sha="$(json_field "$manifest_path" sha256 | tr '[:upper:]' '[:lower:]')"
  rid="$(json_field "$manifest_path" rid)"
  manifest_version="$(json_field "$manifest_path" version)"

  download "$RELEASE_URL" "$tarball_path"
  local actual_sha
  actual_sha="$(sha256_file "$tarball_path" | tr '[:upper:]' '[:lower:]')"
  if [[ "$actual_sha" != "$expected_sha" ]]; then
    echo "error: sha256 mismatch" >&2
    echo "  expected: $expected_sha" >&2
    echo "  actual:   $actual_sha" >&2
    exit 1
  fi
  echo "sha256: OK"
  echo "release: version=$manifest_version rid=$rid"

  local parent
  parent="$(dirname "$INSTALL_DIR")"
  mkdir -p "$parent"
  if [[ -e "$INSTALL_DIR" ]]; then
    rm -rf "$INSTALL_DIR"
  fi
  mkdir -p "$INSTALL_DIR"
  tar -xzf "$tarball_path" -C "$INSTALL_DIR" --strip-components=1
  echo "extracted: $INSTALL_DIR"
}

post_install() {
  export OCCAM_HOME="$INSTALL_DIR"
  cd "$INSTALL_DIR"

  echo ""
  echo "doctor (npm + Playwright, skip dotnet publish) ..."
  bash "$INSTALL_DIR/scripts/occam-doctor.sh" --skip-build

  echo ""
  echo "verify-install ..."
  node "$INSTALL_DIR/scripts/lib/verify-install.mjs" --skip-build

  echo ""
  echo "smoke (core tools) ..."
  node "$INSTALL_DIR/scripts/hermes-smoke.mjs"
}

run_onboard() {
  export OCCAM_HOME="$INSTALL_DIR"
  export OCCAM_HOST="${HOST_TARGET}"

  echo ""
  if [[ "$SETUP_MODE" == "manual" ]]; then
    echo "Starting manual onboard wizard ..."
    node "$INSTALL_DIR/scripts/occam-onboard.mjs" --skip-doctor --skip-welcome
  else
    local profile="hermes-headless"
    if [[ "$HOST_TARGET" == "cursor" ]]; then
      profile="default"
    fi
    echo "Applying auto setup (profile=$profile host=$HOST_TARGET) ..."
    node "$INSTALL_DIR/scripts/occam-onboard.mjs" \
      --non-interactive \
      --profile "$profile" \
      --host-target "$HOST_TARGET" \
      --skip-doctor \
      --plain
  fi
}

print_connection_snippet() {
  echo ""
  echo "=== Connection snippet (host=$HOST_TARGET) ==="
  node "$INSTALL_DIR/scripts/lib/print-connection-snippet.mjs" "$INSTALL_DIR" "$HOST_TARGET"
  echo ""
  echo "Docs: docs/01-operator-journey.md#verify"
  echo "PATH: export PATH=\"$INSTALL_DIR/scripts:\$PATH\""
  echo "Next: occam   # operator menu"
  echo "       occam smoke   # after MCP reload"
}

main() {
  print_product_welcome
  resolve_setup_mode

  need_cmd curl
  need_cmd tar
  check_node

  echo ""
  echo "install_dir: $INSTALL_DIR"
  echo "host_target: $HOST_TARGET"
  echo "release_url: $RELEASE_URL"
  echo ""

  install_release
  post_install
  run_onboard
  print_connection_snippet
}

main "$@"
