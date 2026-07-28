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

# Stop install-scoped Occam hosts. Does not delete the install tree.
prepare_install_replace() {
  local dir="$1"
  [[ -e "$dir" ]] || return 0

  local helper=""
  local helper_tmp=""
  if [[ -n "$ROOT_DIR" && -f "$ROOT_DIR/scripts/lib/prepare-install-replace.mjs" ]]; then
    helper="$ROOT_DIR/scripts/lib/prepare-install-replace.mjs"
  elif [[ -f "$dir/scripts/lib/prepare-install-replace.mjs" ]]; then
    helper="$dir/scripts/lib/prepare-install-replace.mjs"
  else
    local base="${OCCAM_OVERLAY_BASE_URL:-https://raw.githubusercontent.com/ContextForgeAI/occam/main}"
    base="${base%/}"
    helper_tmp="$(mktemp -d "${TMPDIR:-/tmp}/occam-prepare.XXXXXX")"
    if ! curl -fsSL "$base/scripts/lib/prepare-install-replace.mjs" -o "$helper_tmp/prepare-install-replace.mjs" \
      || ! curl -fsSL "$base/scripts/lib/stop-occam-processes.mjs" -o "$helper_tmp/stop-occam-processes.mjs" \
      || ! curl -fsSL "$base/scripts/lib/resolve-rid.mjs" -o "$helper_tmp/resolve-rid.mjs"; then
      rm -rf "$helper_tmp"
      cat >&2 <<'EOF'
Occam is currently in use.

Close or restart these AI apps before updating:
• Any app that has Occam connected (Cursor, Claude Desktop, …)

Then run the installer again.

No files were changed.
EOF
      exit 2
    fi
    helper="$helper_tmp/prepare-install-replace.mjs"
  fi

  set +e
  local json
  json="$(node "$helper" --dir "$dir" --json 2>&1)"
  local code=$?
  set -e
  if [[ -n "$helper_tmp" ]]; then rm -rf "$helper_tmp"; fi
  if [[ "$code" -eq 0 ]]; then
    return 0
  fi
  node -e "try{const j=JSON.parse(process.argv[1]); if(j.message) console.error(j.message)}catch(e){process.exit(1)}" "$json" 2>/dev/null || cat >&2 <<'EOF'
Occam is currently in use.

Close or restart these AI apps before updating:
• Any app that has Occam connected (Cursor, Claude Desktop, …)

Then run the installer again.

No files were changed.
EOF
  exit 2
}

replace_install_tree() {
  local target="$1"
  local staged="$2"
  local backup=""
  if [[ -e "$target" ]]; then
    backup="${target}.pre-replace-$$"
    if ! mv "$target" "$backup"; then
      cat >&2 <<'EOF'
Occam is currently in use.

The existing install could not be moved aside (file lock).
Close AI apps using Occam, then run the installer again.

No files were changed.
EOF
      exit 2
    fi
  fi
  mkdir -p "$target"
  if ! mv "$staged"/* "$target"/; then
    if [[ -n "$backup" && -e "$backup" ]]; then
      rm -rf "$target"
      mv "$backup" "$target" || true
    fi
    echo "Install failed while replacing files." >&2
    echo "The previous Occam install was restored when possible." >&2
    exit 1
  fi
  if [[ -n "$backup" ]]; then
    rm -rf "$backup" || true
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

  local extract_tmp="$tmp/extract"
  mkdir -p "$extract_tmp"
  tar -xzf "$tarball_path" -C "$extract_tmp"
  # Prefer single top-level directory contents as the staged tree.
  local staged="$extract_tmp"
  local entries=("$extract_tmp"/*)
  if [[ ${#entries[@]} -eq 1 && -d "${entries[0]}" ]]; then
    staged="${entries[0]}"
  fi

  prepare_install_replace "$INSTALL_DIR"
  replace_install_tree "$INSTALL_DIR" "$staged"
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

# Install ~/.local/bin/occam and ensure current-shell PATH (prepend). Overlay connect
# CLI from public main when the release tarball lacks occam-connect.
install_occam_user_command() {
  local home="$1"
  local helper_tmp=""
  # Always stage helper from public main so OPERATOR_OVERLAY_FILES stays current
  # even when Level B already shipped an older install-user-cli.mjs.
  # Manifest: scripts/lib/operator/install-user-cli-temp-manifest.mjs
  helper_tmp="$(mktemp -d "${TMPDIR:-/tmp}/occam-install-user-cli.XXXXXX")"
  local base="${OCCAM_OVERLAY_BASE_URL:-https://raw.githubusercontent.com/ContextForgeAI/occam/main}"
  base="${base%/}"
  mkdir -p "$helper_tmp/scripts/lib/operator"
  if ! curl -fsSL "$base/scripts/lib/operator/install-user-cli.mjs" -o "$helper_tmp/scripts/lib/operator/install-user-cli.mjs" \
    || ! curl -fsSL "$base/scripts/lib/resolve-node-runtime.mjs" -o "$helper_tmp/scripts/lib/resolve-node-runtime.mjs"; then
    echo "✗ Could not install the occam command (download failed)." >&2
    echo "Re-run with OCCAM_VERBOSE=1 for details." >&2
    rm -rf "$helper_tmp"
    exit 1
  fi
  if [[ ! -f "$helper_tmp/scripts/lib/operator/install-user-cli.mjs" \
    || ! -f "$helper_tmp/scripts/lib/resolve-node-runtime.mjs" ]]; then
    echo "✗ Could not install the occam command (incomplete helper staging)." >&2
    echo "Re-run with OCCAM_VERBOSE=1 for details." >&2
    rm -rf "$helper_tmp"
    exit 1
  fi
  local helper="$helper_tmp/scripts/lib/operator/install-user-cli.mjs"

  local json
  set +e
  # Always refresh operator CLI overlay from the same base.
  # Level B tarballs lag git; gap-only overlay leaves stale doctor/update/contract UX.
  json="$(node "$helper" --home "$home" --base-url "$base" --json 2>&1)"
  local code=$?
  set -e
  rm -rf "$helper_tmp"
  if [[ "$code" -ne 0 ]]; then
    echo "✗ Could not install the occam command." >&2
    echo "Re-run with OCCAM_VERBOSE=1 for details." >&2
    printf '%s\n' "$json" | tail -n 30 >&2
    exit "$code"
  fi
  if [[ "$VERBOSE" -eq 1 ]]; then
    printf '%s\n' "$json"
  fi

  local bin_dir
  bin_dir="$(node -e "const j=JSON.parse(process.argv[1]); process.stdout.write(j.pathForCurrentProcess||j.binDir||'')" "$json")"
  if [[ -z "$bin_dir" ]]; then
    bin_dir="$HOME/.local/bin"
  fi
  case ":$PATH:" in
    *":$bin_dir:"*)
      # Already present — move to front for this shell (parity with Windows).
      PATH_WITHOUT=""
      OLD_IFS="$IFS"
      IFS=':'
      for p in $PATH; do
        if [[ "$p" != "$bin_dir" ]]; then
          if [[ -z "$PATH_WITHOUT" ]]; then PATH_WITHOUT="$p"; else PATH_WITHOUT="$PATH_WITHOUT:$p"; fi
        fi
      done
      IFS="$OLD_IFS"
      export PATH="$bin_dir${PATH_WITHOUT:+:$PATH_WITHOUT}"
      ;;
    *) export PATH="$bin_dir:$PATH" ;;
  esac

  hash -r 2>/dev/null || true
  if ! command -v occam >/dev/null 2>&1; then
    echo "✗ occam command is not available on PATH after install." >&2
    echo "Open a new shell, or run: export PATH=\"$bin_dir:\$PATH\"" >&2
    exit 1
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
    install_occam_user_command "$INSTALL_DIR"
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

  echo "  Installing runtime…"
  run_legacy_step "Runtime setup (doctor)" bash "$INSTALL_DIR/scripts/occam-doctor.sh" --skip-build
  echo "✓ Runtime installed"
  echo "✓ Browser ready"
  echo "  Running self-check…"
  run_legacy_step "Host verify" node "$INSTALL_DIR/scripts/lib/verify-install.mjs" --skip-build --version "$VERSION"
  run_legacy_step "Self-check" node "$INSTALL_DIR/scripts/hermes-smoke.mjs"
  echo "✓ Self-check passed"

  install_occam_user_command "$INSTALL_DIR"

  local connect_js="$INSTALL_DIR/scripts/occam-connect.mjs"
  if [[ -f "$connect_js" ]]; then
    # Avoid "${arr[@]}" with empty array under `set -u` (macOS bash 3.2 / bash 5).
    if [[ "$VERBOSE" -eq 1 ]]; then
      node "$connect_js" --verbose
    else
      node "$connect_js"
    fi
  else
    echo ""
    echo "Occam is installed."
    echo ""
    echo "Connect an AI app later with:"
    echo "  occam connect"
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
