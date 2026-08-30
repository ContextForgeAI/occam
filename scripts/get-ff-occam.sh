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
  local os="${1:-}" arch="${2:-}"
  [[ -n "$os" ]] || os="$(uname -s 2>/dev/null || echo unknown)"
  [[ -n "$arch" ]] || arch="$(uname -m 2>/dev/null || echo unknown)"
  case "${os}/${arch}" in
    Darwin/arm64|Darwin/aarch64) echo "osx-arm64" ;;
    Linux/x86_64|Linux/amd64) echo "linux-x64" ;;
    MINGW*/x86_64|MINGW*/amd64|MSYS*/x86_64|MSYS*/amd64|CYGWIN*/x86_64|CYGWIN*/amd64|Windows_NT/x86_64|Windows_NT/amd64)
      echo "win-x64"
      ;;
    *)
      echo "error: no public Occam release for ${os}/${arch} (published RIDs: win-x64, linux-x64, osx-arm64)" >&2
      return 1
      ;;
  esac
}

assert_published_rid() {
  case "$1" in
    win-x64|linux-x64|osx-arm64) return 0 ;;
    *)
      echo "error: unsupported OCCAM_RID: $1 (published RIDs: win-x64, linux-x64, osx-arm64)" >&2
      return 1
      ;;
  esac
}

# Public default tracks the published GitHub Release (see PUBLIC_DEFAULT_RELEASE_VERSION).
VERSION="${OCCAM_VERSION:-1.0.0}"
RID="${OCCAM_RID:-$(detect_rid)}"
assert_published_rid "$RID"
INSTALL_DIR="${OCCAM_INSTALL_DIR:-$HOME/.local/share/ff-occam}"
# Set by install_release from manifest runtimeLayout (never from version string).
INSTALL_CONTRACT=""
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
INSTALL_TRANSACTION_TARGET=""
INSTALL_TRANSACTION_BACKUP=""
INSTALL_TRANSACTION_ACTIVE=0
INSTALL_TRANSACTION_COMMITTED=0
BOOTSTRAP_TMP=""

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
  local staged="${2:-}"
  [[ -e "$dir" ]] || return 0

  local helper=""
  local helper_tmp=""
  if [[ -n "$staged" && -f "$staged/scripts/lib/prepare-install-replace.mjs" ]]; then
    # Prefer the helper from the archive whose SHA-256 was just verified.
    helper="$staged/scripts/lib/prepare-install-replace.mjs"
  elif [[ -n "$ROOT_DIR" && -f "$ROOT_DIR/scripts/lib/prepare-install-replace.mjs" ]]; then
    helper="$ROOT_DIR/scripts/lib/prepare-install-replace.mjs"
  elif [[ -f "$dir/scripts/lib/prepare-install-replace.mjs" ]]; then
    helper="$dir/scripts/lib/prepare-install-replace.mjs"
  elif [[ "${INSTALL_CONTRACT:-}" == "legacy" ]]; then
    local base="${OCCAM_OVERLAY_BASE_URL:-https://raw.githubusercontent.com/ContextForgeAI/occam/main}"
    base="${base%/}"
    helper_tmp="$(mktemp -d "${TMPDIR:-/tmp}/occam-prepare.XXXXXX")"
    if ! curl -fsSL "$base/scripts/lib/prepare-install-replace.mjs" -o "$helper_tmp/prepare-install-replace.mjs" \
      || ! curl -fsSL "$base/scripts/lib/install-target-inspect.mjs" -o "$helper_tmp/install-target-inspect.mjs" \
      || ! curl -fsSL "$base/scripts/lib/stop-occam-processes.mjs" -o "$helper_tmp/stop-occam-processes.mjs" \
      || ! curl -fsSL "$base/scripts/lib/resolve-rid.mjs" -o "$helper_tmp/resolve-rid.mjs"; then
      rm -rf "$helper_tmp"
      echo "error: could not download install replacement helpers for legacy Level B install" >&2
      echo "No files were changed." >&2
      return 1
    fi
    helper="$helper_tmp/prepare-install-replace.mjs"
  else
    echo "error: verified release archive is missing the install replacement helper" >&2
    echo "No files were changed." >&2
    return 1
  fi

  local json code
  if json="$(node "$helper" --dir "$dir" --rid "$RID" --json 2>&1)"; then
    [[ -n "$helper_tmp" ]] && rm -rf "$helper_tmp"
    return 0
  else
    code=$?
  fi
  [[ -n "$helper_tmp" ]] && rm -rf "$helper_tmp"
  node -e "try{const j=JSON.parse(process.argv[1]); if(j.message) console.error(j.message)}catch(e){process.exit(1)}" "$json" 2>/dev/null || cat >&2 <<'EOF'
Occam is currently in use.

Close or restart these AI apps before updating:
• Any app that has Occam connected (Cursor, Claude Desktop, …)

Then run the installer again.

No files were changed.
EOF
  return 2
}

assert_safe_tree_path() {
  local value="$1" label="$2"
  if [[ -z "$value" ]]; then
    echo "error: ${label} path is empty (internal installer error)" >&2
    return 1
  fi
  node -e '
    const path = require("path");
    const value = path.resolve(process.argv[1]);
    const root = path.parse(value).root;
    const home = process.env.HOME ? path.resolve(process.env.HOME) : "";
    if (value === root || (home && value === home)) {
      console.error(`error: ${process.argv[2]} path is too broad: ${value}`);
      process.exit(1);
    }
    process.stdout.write(value);
  ' "$value" "$label"
}

assert_transaction_backup_path() {
  local target="$1" backup="$2"
  [[ -n "$backup" ]] || return 0
  if [[ "$(dirname "$backup")" != "$(dirname "$target")" || "$backup" != "${target}.pre-replace-"* ]]; then
    echo "error: backup path escaped the install transaction boundary: $backup" >&2
    return 1
  fi
}

replace_install_tree() {
  local target staged
  target="$(assert_safe_tree_path "$1" install)"
  staged="$(assert_safe_tree_path "$2" staging)"
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
  INSTALL_TRANSACTION_TARGET="$target"
  INSTALL_TRANSACTION_BACKUP="$backup"
  INSTALL_TRANSACTION_ACTIVE=1
  INSTALL_TRANSACTION_COMMITTED=0
}

rollback_install_transaction() {
  [[ "$INSTALL_TRANSACTION_ACTIVE" -eq 1 ]] || return 0
  local target="$INSTALL_TRANSACTION_TARGET" backup="$INSTALL_TRANSACTION_BACKUP"
  target="$(assert_safe_tree_path "$target" install)" || return 1
  assert_transaction_backup_path "$target" "$backup" || return 1

  echo "Install validation failed after the release tree was replaced." >&2
  echo "Stopping processes started from the new install before rollback..." >&2
  local stop_code=0
  if prepare_install_replace "$target" "$target"; then
    stop_code=0
  else
    stop_code=$?
  fi
  if [[ "$stop_code" -ne 0 ]]; then
    echo "error: rollback could not stop every process using the new install." >&2
    echo "The failed install was preserved at: $target" >&2
    if [[ -n "$backup" ]]; then
      echo "The previous install backup was preserved at: $backup" >&2
    fi
    echo "Close AI apps using Occam, then move the backup back into place." >&2
    return 1
  fi

  cd "$(dirname "$target")"
  rm -rf -- "$target"
  if [[ -n "$backup" && -e "$backup" ]]; then
    mv "$backup" "$target"
    echo "The previous Occam install was restored: $target" >&2
  else
    echo "The failed fresh Occam install was removed: $target" >&2
  fi
  INSTALL_TRANSACTION_ACTIVE=0
  INSTALL_TRANSACTION_TARGET=""
  INSTALL_TRANSACTION_BACKUP=""
}

commit_install_transaction() {
  [[ "$INSTALL_TRANSACTION_ACTIVE" -eq 1 ]] || return 0
  local target="$INSTALL_TRANSACTION_TARGET" backup="$INSTALL_TRANSACTION_BACKUP"
  target="$(assert_safe_tree_path "$target" install)"
  assert_transaction_backup_path "$target" "$backup"
  if [[ -n "$backup" && -e "$backup" ]]; then
    if ! rm -rf -- "$backup" || [[ -e "$backup" ]]; then
      echo "warning: install succeeded, but the previous-tree backup could not be removed: $backup" >&2
      echo "Review that exact path and remove it manually after confirming Occam works." >&2
    fi
  fi
  INSTALL_TRANSACTION_COMMITTED=1
  INSTALL_TRANSACTION_ACTIVE=0
  INSTALL_TRANSACTION_TARGET=""
  INSTALL_TRANSACTION_BACKUP=""
}

bootstrap_on_exit() {
  local code=$?
  trap - EXIT
  if [[ "$INSTALL_TRANSACTION_ACTIVE" -eq 1 && "$INSTALL_TRANSACTION_COMMITTED" -ne 1 ]]; then
    local rollback_code=0
    if rollback_install_transaction; then
      rollback_code=0
    else
      rollback_code=$?
    fi
    if [[ "$rollback_code" -ne 0 ]]; then
      code=1
    fi
  fi
  if [[ -n "$BOOTSTRAP_TMP" && -d "$BOOTSTRAP_TMP" ]]; then
    rm -rf -- "$BOOTSTRAP_TMP"
  fi
  exit "$code"
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
    if (v == null || v === '') {
      console.error('error: release manifest missing ' + process.argv[2]);
      process.exit(2);
    }
    process.stdout.write(String(v));
  " "$1" "$2"
}

json_field_optional() {
  node -e "
    const fs = require('fs');
    const j = JSON.parse(fs.readFileSync(process.argv[1], 'utf8'));
    const v = j[process.argv[2]];
    if (v == null || v === '') process.exit(0);
    process.stdout.write(String(v));
  " "$1" "$2"
}

# Resolve archive-preflight.mjs: checkout → OCCAM_ARCHIVE_PREFLIGHT_PATH →
# pinned raw URL for the release tag being installed → fail (listing fallback).
resolve_archive_preflight_module() {
  if [[ -n "${OCCAM_ARCHIVE_PREFLIGHT_PATH:-}" && -f "$OCCAM_ARCHIVE_PREFLIGHT_PATH" ]]; then
    printf '%s\n' "$OCCAM_ARCHIVE_PREFLIGHT_PATH"
    return 0
  fi
  if [[ -n "$ROOT_DIR" && -f "$ROOT_DIR/scripts/lib/archive-preflight.mjs" ]]; then
    printf '%s\n' "$ROOT_DIR/scripts/lib/archive-preflight.mjs"
    return 0
  fi
  if [[ -z "${BOOTSTRAP_TMP:-}" ]]; then
    return 1
  fi
  local dest="$BOOTSTRAP_TMP/archive-preflight.mjs"
  if [[ -f "$dest" ]]; then
    printf '%s\n' "$dest"
    return 0
  fi
  local url="${OCCAM_ARCHIVE_PREFLIGHT_URL:-https://raw.githubusercontent.com/ContextForgeAI/occam/v${VERSION}/scripts/lib/archive-preflight.mjs}"
  if curl -fsSL "$url" -o "$dest"; then
    printf '%s\n' "$dest"
    return 0
  fi
  rm -f "$dest"
  return 1
}

# Fail closed before any extract. Prefer Node gzipped-ustar preflight
# (archive-preflight.mjs). Listing-text fallback is last resort only.
preflight_release_archive() {
  local archive="$1"
  local expected_root="$2"
  local module=""
  if module="$(resolve_archive_preflight_module)"; then
    node "$module" --archive "$archive" --expected-root "$expected_root"
    return $?
  fi

  echo "warn: archive-preflight.mjs unavailable; using tar listing fallback" >&2
  local listing="$BOOTSTRAP_TMP/tar-list.txt"
  if ! tar -tvzf "$archive" >"$listing"; then
    echo "error: unable to list archive members before extract" >&2
    exit 1
  fi

  if [[ -n "$ROOT_DIR" && -f "$ROOT_DIR/scripts/lib/archive-preflight-listing.mjs" ]]; then
    node "$ROOT_DIR/scripts/lib/archive-preflight-listing.mjs" "$listing" "$expected_root"
    return $?
  fi

  node - "$listing" "$expected_root" <<'NODE'
const fs = require("fs");
const [listingPath, expectedRoot] = process.argv.slice(2);
const lines = fs.readFileSync(listingPath, "utf8").split(/\r?\n/).filter(Boolean);

function nameStartIndex(parts) {
  if (parts.length >= 6 && String(parts[1] || "").includes("/")) return 5;
  if (parts.length >= 9 && /^\d+$/.test(String(parts[1] || ""))) return 8;
  if (parts.length >= 6) return 5;
  return -1;
}

function parseLine(line) {
  const type = line[0] || "";
  const arrow = line.indexOf(" -> ");
  if (arrow !== -1 && (type === "l" || type === "h")) {
    const left = line.slice(0, arrow).trim().split(/\s+/);
    const start = nameStartIndex(left);
    return { type, name: start >= 0 ? left.slice(start).join(" ") : "" };
  }
  const parts = line.trim().split(/\s+/);
  const start = nameStartIndex(parts);
  if (start < 0) return null;
  return { type, name: parts.slice(start).join(" ") };
}

function unsafePath(p) {
  const n = String(p || "").replace(/\\/g, "/");
  if (!n) return "empty archive member path";
  if (n.startsWith("/") || n.startsWith("~")) return `absolute archive member path: ${p}`;
  if (/^[A-Za-z]:(\/|$)/.test(n)) return `windows drive archive member path: ${p}`;
  if (n.startsWith("//")) return `unc archive member path: ${p}`;
  if (n.split("/").includes("..")) return `path traversal in archive member: ${p}`;
  return null;
}

const names = [];
for (const line of lines) {
  const parsed = parseLine(line);
  if (!parsed || !parsed.name) {
    console.error(`error: unable to parse archive member listing line: ${line}`);
    process.exit(1);
  }
  if (parsed.type === "l" || parsed.type === "h") {
    console.error(`error: ${parsed.type === "l" ? "symlink" : "hardlink"} archive members are not allowed: ${parsed.name}`);
    process.exit(1);
  }
  names.push(parsed.name.replace(/\\/g, "/"));
}
const roots = new Set();
for (const name of names) {
  const reason = unsafePath(name);
  if (reason) {
    console.error(`error: ${reason}`);
    process.exit(1);
  }
  const root = name.split("/").filter(Boolean)[0];
  if (root) roots.add(root);
}
if (!roots.has(expectedRoot)) {
  console.error(`error: missing expected archive root directory: ${expectedRoot}`);
  process.exit(1);
}
for (const root of roots) {
  if (root !== expectedRoot) {
    console.error(`error: unexpected archive root entries: ${root}`);
    process.exit(1);
  }
}
NODE
}

# Cosign is required when the release manifest declares signaturePolicy=required-cosign-v1
# (published v1.0.0-rc.3+). Prefer an operator-installed CLI; do not silently skip.
ensure_cosign_cli() {
  if command -v cosign >/dev/null 2>&1; then
    return 0
  fi
  local d
  for d in /opt/homebrew/bin /usr/local/bin "${HOME}/.local/bin"; do
    if [[ -x "${d}/cosign" ]]; then
      export PATH="${d}:${PATH}"
      return 0
    fi
  done
  cat >&2 <<'EOF'
error: signaturePolicy=required-cosign-v1 requires the cosign CLI on PATH

Install Cosign, then re-run the Occam bootstrap:
  https://docs.sigstore.dev/cosign/system_config/installation/

Authenticity checks prove release signer identity — not page-content truth.
See INSTALL.md (Cosign / signaturePolicy).
EOF
  exit 1
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
  BOOTSTRAP_TMP="$tmp"

  local manifest_path="$tmp/manifest.json"
  local tarball_path="$tmp/release.tar.gz"

  download "$MANIFEST_URL" "$manifest_path"
  local expected_sha rid manifest_version manifest_tarball runtime_layout expected_tarball
  expected_sha="$(json_field "$manifest_path" sha256 | tr '[:upper:]' '[:lower:]')"
  rid="$(json_field "$manifest_path" rid)"
  manifest_version="$(json_field "$manifest_path" version)"
  manifest_tarball="$(json_field "$manifest_path" tarball)"
  runtime_layout="$(json_field_optional "$manifest_path" runtimeLayout)"
  expected_tarball="ff-occam-${VERSION}-${RID}.tar.gz"
  if [[ "$manifest_version" != "$VERSION" ]]; then
    echo "error: release manifest version mismatch (expected $VERSION, got $manifest_version)" >&2
    exit 1
  fi
  if [[ "$rid" != "$RID" ]]; then
    echo "error: release manifest RID mismatch (expected $RID, got $rid)" >&2
    exit 1
  fi
  if [[ -n "$manifest_tarball" && "$manifest_tarball" != "$expected_tarball" ]]; then
    echo "error: release manifest tarball mismatch (expected $expected_tarball, got $manifest_tarball)" >&2
    exit 1
  fi
  # Contract from manifest runtimeLayout — never from version string.
  if [[ -z "$runtime_layout" ]]; then
    INSTALL_CONTRACT=legacy
  elif [[ "$runtime_layout" == "self-contained-v1" ]]; then
    INSTALL_CONTRACT=self-contained-v1
  else
    echo "error: unsupported release runtimeLayout: $runtime_layout" >&2
    exit 1
  fi
  if [[ ! "$expected_sha" =~ ^[0-9a-f]{64}$ ]]; then
    echo "error: release manifest sha256 must be 64 hexadecimal characters" >&2
    exit 1
  fi

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
  v_echo "release: version=$manifest_version rid=$rid contract=$INSTALL_CONTRACT"

  local signature_policy
  signature_policy="$(json_field_optional "$manifest_path" signaturePolicy)"
  if [[ -z "$signature_policy" ]]; then
    signature_policy="sha256-only"
  fi
  case "$signature_policy" in
    sha256-only) ;;
    required-cosign-v1)
      local bundle_path="$tmp/release.tar.gz.bundle"
      local bundle_url="${RELEASE_URL}.bundle"
      if [[ -n "${OCCAM_RELEASE_BUNDLE_URL:-}" ]]; then
        bundle_url="$OCCAM_RELEASE_BUNDLE_URL"
      fi
      download "$bundle_url" "$bundle_path"
      local verify_mod=""
      if [[ -n "${OCCAM_VERIFY_RELEASE_SIGNATURE_PATH:-}" && -f "$OCCAM_VERIFY_RELEASE_SIGNATURE_PATH" ]]; then
        verify_mod="$OCCAM_VERIFY_RELEASE_SIGNATURE_PATH"
      elif [[ -n "$ROOT_DIR" && -f "$ROOT_DIR/scripts/lib/verify-release-signature.mjs" ]]; then
        verify_mod="$ROOT_DIR/scripts/lib/verify-release-signature.mjs"
      elif [[ -n "${BOOTSTRAP_TMP:-}" ]]; then
        verify_mod="$BOOTSTRAP_TMP/verify-release-signature.mjs"
        if [[ ! -f "$verify_mod" ]]; then
          local verify_url="${OCCAM_VERIFY_RELEASE_SIGNATURE_URL:-https://raw.githubusercontent.com/ContextForgeAI/occam/v${VERSION}/scripts/lib/verify-release-signature.mjs}"
          if ! curl -fsSL "$verify_url" -o "$verify_mod"; then
            rm -f "$verify_mod"
            verify_mod=""
          fi
        fi
      fi
      if [[ -n "$verify_mod" && -f "$verify_mod" ]]; then
        # verify-release-signature.mjs still needs cosign on PATH.
        ensure_cosign_cli
        node "$verify_mod" \
          --manifest "$manifest_path" \
          --archive "$tarball_path" \
          --bundle "$bundle_path" \
          --version "$VERSION"
      else
        ensure_cosign_cli
        local expected_identity
        expected_identity="https://github.com/ContextForgeAI/occam/.github/workflows/occam-release.yml@refs/tags/v${VERSION}"
        cosign verify-blob "$tarball_path" \
          --bundle "$bundle_path" \
          --certificate-identity "$expected_identity" \
          --certificate-oidc-issuer "https://token.actions.githubusercontent.com"
      fi
      ;;
    *)
      echo "error: unsupported release signaturePolicy: $signature_policy" >&2
      exit 1
      ;;
  esac

  if [[ "$INSTALL_CONTRACT" == "self-contained-v1" ]]; then
    local expected_root="ff-occam-${VERSION}-${RID}"
    preflight_release_archive "$tarball_path" "$expected_root"
  fi

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

  if [[ "$INSTALL_CONTRACT" == "self-contained-v1" ]]; then
    local runtime_checker="$staged/scripts/lib/operator/install-user-cli.mjs"
    if [[ ! -f "$runtime_checker" ]] \
      || ! node "$runtime_checker" --check-release-root "$staged" \
        --version "$VERSION" --rid "$RID" >/dev/null; then
      echo "error: verified release archive is incomplete" >&2
      echo "No files were changed." >&2
      echo "Self-contained install does not fall back to legacy overlay mode." >&2
      exit 1
    fi
  fi

  prepare_install_replace "$INSTALL_DIR" "$staged"
  replace_install_tree "$INSTALL_DIR" "$staged"
  v_echo "extracted: $INSTALL_DIR"

  rm -rf "$tmp"
  BOOTSTRAP_TMP=""
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

# Install ~/.local/bin/occam from the verified release and prepend it to this shell's PATH.
install_occam_user_command() {
  local home="$1"
  local helper=""
  local helper_tmp=""
  local overlay_args=()

  if [[ "${INSTALL_CONTRACT:-}" == "legacy" ]]; then
    # Legacy Level B: refresh operator CLI from mutable main overlay.
    helper_tmp="$(mktemp -d "${TMPDIR:-/tmp}/occam-install-user-cli.XXXXXX")"
    local base="${OCCAM_OVERLAY_BASE_URL:-https://raw.githubusercontent.com/ContextForgeAI/occam/main}"
    base="${base%/}"
    mkdir -p "$helper_tmp/scripts/lib/operator"
    if ! curl -fsSL "$base/scripts/lib/operator/install-user-cli.mjs" -o "$helper_tmp/scripts/lib/operator/install-user-cli.mjs" \
      || ! curl -fsSL "$base/scripts/lib/resolve-node-runtime.mjs" -o "$helper_tmp/scripts/lib/resolve-node-runtime.mjs"; then
      echo "✗ Could not install the occam command (download failed)." >&2
      rm -rf "$helper_tmp"
      exit 1
    fi
    helper="$helper_tmp/scripts/lib/operator/install-user-cli.mjs"
    overlay_args=(--base-url "$base")
  else
    # Self-contained: helpers must come from the verified archive — never overlay.
    helper="$home/scripts/lib/operator/install-user-cli.mjs"
    if [[ ! -f "$helper" || ! -f "$home/scripts/lib/resolve-node-runtime.mjs" ]]; then
      echo "✗ Verified release archive is missing the occam command installer." >&2
      exit 1
    fi
    overlay_args=(--no-overlay)
  fi

  local json
  set +e
  json="$(node "$helper" --home "$home" "${overlay_args[@]}" --json 2>&1)"
  local code=$?
  set -e
  [[ -n "$helper_tmp" ]] && rm -rf "$helper_tmp"
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
    local args=(--setup "$SETUP_MODE" --version "$VERSION" --download-ok)
    if [[ "$VERBOSE" -eq 1 ]]; then
      args+=(--verbose)
    fi
    node "$post_ux" "${args[@]}"
    install_occam_user_command "$INSTALL_DIR"
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

  install_occam_user_command "$INSTALL_DIR"
}

main() {
  trap bootstrap_on_exit EXIT
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
  commit_install_transaction
}

main "$@"
