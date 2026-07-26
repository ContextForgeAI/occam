#!/usr/bin/env bash
# Occam — one-liner bootstrap (curl | bash).
# Target: Node 20+ only — NO git, NO .NET SDK on the install machine.
#
#   curl -fsSL "$OCCAM_GET_URL" | bash
#
# Quiet by default. OCCAM_VERBOSE=1 shows doctor/smoke internals.
# Flow: download → extract → post-install-ux (verify → connect → Ready)
set -euo pipefail

ROOT_DIR=""
if [[ -n "${BASH_SOURCE[0]:-}" ]] && [[ -f "${BASH_SOURCE[0]}" ]]; then
  ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fi

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
# Legacy snippet hint only — never printed as a selected host before the user chooses.
HOST_TARGET="${OCCAM_HOST:-}"
ALLOW_HTTP="${OCCAM_RELEASE_ALLOW_HTTP:-0}"
SETUP_MODE="${OCCAM_SETUP:-}"
VERBOSE=0
if [[ "${OCCAM_VERBOSE:-}" == "1" || "${OCCAM_VERBOSE:-}" == "true" ||
      "${OCCAM_DEBUG:-}" == "1" || "${OCCAM_DEBUG:-}" == "true" ]]; then
  VERBOSE=1
fi

RELEASE_BASE="${OCCAM_RELEASE_BASE:-https://github.com/ContextForgeAI/occam/releases/download/v${VERSION}}"
RELEASE_URL="${OCCAM_RELEASE_URL:-${RELEASE_BASE}/ff-occam-${VERSION}-${RID}.tar.gz}"
MANIFEST_URL="${OCCAM_RELEASE_MANIFEST_URL:-${RELEASE_BASE}/ff-occam-${VERSION}-${RID}-manifest.json}"

MIN_NODE_MAJOR=20

v_echo() {
  if [[ "$VERBOSE" -eq 1 ]]; then
    echo "$@"
  fi
}

resolve_setup_mode() {
  local raw="${SETUP_MODE}"
  local normalized
  normalized="$(printf '%s' "$raw" | tr '[:upper:]' '[:lower:]')"
  case "$normalized" in
    ""|auto|1)
      SETUP_MODE=auto
      v_echo "setup: auto"
      return
      ;;
    manual|2)
      SETUP_MODE=manual
      v_echo "setup: manual"
      return
      ;;
    ask)
      ;;
    *)
      echo "error: OCCAM_SETUP must be auto|manual|ask (got $raw)" >&2
      exit 1
      ;;
  esac

  if [[ ! -t 0 ]] || [[ ! -t 1 ]]; then
    SETUP_MODE=auto
    v_echo "setup: auto (ask ignored: non-interactive)"
    return
  fi

  echo ""
  echo "  First-run setup"
  echo "  [1] Auto   — detect and connect supported AI apps"
  echo "  [2] Manual — choose which AI app to connect"
  echo ""
  printf "  Setup [1]: "
  read -r choice
  choice="${choice:-1}"
  case "$choice" in
    2|manual|Manual|MANUAL) SETUP_MODE=manual ;;
    *) SETUP_MODE=auto ;;
  esac
  v_echo "setup: $SETUP_MODE"
}

need_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "error: required command not found: $1" >&2
    exit 1
  fi
}

ensure_node_on_path() {
  if command -v node >/dev/null 2>&1; then
    return 0
  fi
  local d
  for d in /opt/homebrew/bin /usr/local/bin "${HOME}/.local/bin"; do
    if [[ -x "${d}/node" ]]; then
      export PATH="${d}:${PATH}"
      return 0
    fi
  done
}

check_node() {
  ensure_node_on_path
  need_cmd node
  local major
  major="$(node -p "process.versions.node.split('.')[0]")"
  if [[ "$major" -lt "$MIN_NODE_MAJOR" ]]; then
    echo "error: Node.js ${MIN_NODE_MAJOR}+ required (found $(node -v))" >&2
    exit 1
  fi
  v_echo "node: $(node -v)"
}

assert_url_scheme() {
  local url="$1"
  case "$url" in
    https://*) return 0 ;;
    http://*)
      if [[ "$ALLOW_HTTP" == "1" ]]; then
        echo "warning: OCCAM_RELEASE_ALLOW_HTTP=1 — HTTP release URL" >&2
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
  v_echo "download: $url"
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
  # shellcheck disable=SC2064
  trap "rm -rf $(printf '%q' "$tmp")" EXIT

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
  v_echo "sha256: OK"
  v_echo "release: version=$manifest_version rid=$rid"

  local parent
  parent="$(dirname "$INSTALL_DIR")"
  mkdir -p "$parent"
  if [[ -e "$INSTALL_DIR" ]]; then
    rm -rf "$INSTALL_DIR"
  fi
  mkdir -p "$INSTALL_DIR"
  tar -xzf "$tarball_path" -C "$INSTALL_DIR" --strip-components=1
  v_echo "extracted: $INSTALL_DIR"

  rm -rf "$tmp"
  trap - EXIT
}

# Run a legacy-tarball child: quiet = capture I/O (old packs ignore --quiet flags).
# Checks still execute; diagnostics surface only on failure or OCCAM_VERBOSE=1.
run_legacy_step() {
  local label="$1"
  shift
  if [[ "$VERBOSE" -eq 1 ]]; then
    "$@"
    local code=$?
    if [[ "$code" -ne 0 ]]; then
      echo "✗ ${label} failed" >&2
      echo "Re-run with OCCAM_VERBOSE=1 for details." >&2
      exit "$code"
    fi
    return 0
  fi

  local out code
  set +e
  out="$("$@" 2>&1)"
  code=$?
  set -e
  if [[ "$code" -ne 0 ]]; then
    echo "✗ ${label} failed" >&2
    echo "Re-run with OCCAM_VERBOSE=1 for full diagnostics." >&2
    if [[ -n "$out" ]]; then
      echo "" >&2
      printf '%s\n' "$out" | tail -n 40 >&2
    fi
    exit "$code"
  fi
}

run_post_install() {
  export OCCAM_HOME="$INSTALL_DIR"
  cd "$INSTALL_DIR"
  if [[ -n "$HOST_TARGET" ]]; then
    export OCCAM_HOST="$HOST_TARGET"
  fi

  local post_ux="$INSTALL_DIR/scripts/lib/operator/post-install-ux.mjs"
  if [[ -f "$post_ux" ]]; then
    local args=(--setup "$SETUP_MODE" --version "$VERSION" --download-ok)
    if [[ "$VERBOSE" -eq 1 ]]; then
      args+=(--verbose)
    fi
    node "$post_ux" "${args[@]}"
    return
  fi

  # Legacy release tarball (no post-install-ux.mjs): quiet by capturing child I/O.
  echo ""
  echo "Occam $VERSION"
  echo ""
  echo "Installing Occam"
  echo "✓ Download verified"
  export OCCAM_INSTALL_QUIET=1
  if [[ "$VERBOSE" -eq 1 ]]; then
    export OCCAM_INSTALL_QUIET=0
  fi
  export OCCAM_BANNER=0
  export WT_OCCAM_BANNER=0

  run_legacy_step "Runtime setup (doctor)" bash "$INSTALL_DIR/scripts/occam-doctor.sh" --skip-build
  echo "✓ Runtime installed"
  echo "✓ Browser ready"
  run_legacy_step "Host verify" node "$INSTALL_DIR/scripts/lib/verify-install.mjs" --skip-build --version "$VERSION"
  run_legacy_step "Self-check" node "$INSTALL_DIR/scripts/hermes-smoke.mjs"
  echo "✓ Self-check passed"
  echo ""
  echo "Occam is installed."
  if [[ -f "$INSTALL_DIR/scripts/occam-connect.mjs" ]]; then
    echo "Connecting to your AI app"
    node "$INSTALL_DIR/scripts/occam-connect.mjs" || true
  else
    echo "Connect an AI app later with: occam connect"
  fi
}

main() {
  resolve_setup_mode
  need_cmd curl
  need_cmd tar
  check_node

  if [[ "$VERBOSE" -eq 1 ]]; then
    echo ""
    echo "install_dir: $INSTALL_DIR"
    if [[ -n "$HOST_TARGET" ]]; then
      echo "host_hint: $HOST_TARGET"
    fi
    echo "release_url: $RELEASE_URL"
    echo ""
  fi

  install_release
  run_post_install
}

main "$@"
