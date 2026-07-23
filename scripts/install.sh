#!/usr/bin/env bash
# FF-Occam MCP installer — production-oriented (pinned ref, verify, no silent git failures)
#
# Level A (clone + doctor + SDK):
#   git clone "$OCCAM_REPO_URL" /opt/ff-occam && cd /opt/ff-occam
#   ./scripts/install.sh --repo-url "$OCCAM_REPO_URL" --ref v0.7.7-install
#
# Level B (pre-built tarball, no .NET SDK on target):
#   ./scripts/install.sh --from-url "https://releases.example/ff-occam-0.7.7-install-linux-x64.tar.gz"
#
# Pipe install is supported but warned against for production supply-chain hygiene.
set -euo pipefail

INSTALL_DIR="${OCCAM_INSTALL_DIR:-$HOME/.local/share/ff-occam}"
REPO_URL="${OCCAM_REPO_URL:-}"
REF="${OCCAM_REF:-${OCCAM_BRANCH:-main}}"
RELEASE_URL="${OCCAM_RELEASE_URL:-}"
MANIFEST_URL="${OCCAM_RELEASE_MANIFEST_URL:-}"
SKIP_BUILD=0
FORCE_PLAYWRIGHT=0
SKIP_VERIFY=0

usage() {
  cat <<'EOF'
Usage: install.sh [options]

Level A (clone + SDK): clone the repo first, then run this script (avoid curl|bash in prod).

Level B (release tarball): pass --from-url or set OCCAM_RELEASE_URL — Node 20+ only, no .NET SDK.

Options:
  --install-dir PATH       Default: ~/.local/share/ff-occam
  --repo-url URL           Level A: required if OCCAM_REPO_URL unset
  --ref NAME               Level A: git tag, branch, or commit (default: main)
  --from-url URL           Level B: HTTPS tarball URL (sha256 manifest verified before extract)
  --from-release URL       Alias for --from-url
  --manifest-url URL       Level B: manifest JSON (default: tarball URL with -manifest.json)
  --skip-build             Pass --skip-build to occam-doctor
  --skip-verify            Skip post-install browser + binary checks
  --force-playwright       Always run playwright install chromium (prod default: bundled)
  --browser-channel CH     Dev-only: chrome|msedge (skips bundled Chromium)
  -h, --help

Environment:
  OCCAM_REPO_URL, OCCAM_INSTALL_DIR, OCCAM_REF (or OCCAM_BRANCH),
  OCCAM_RELEASE_URL, OCCAM_RELEASE_MANIFEST_URL,
  OCCAM_BROWSER_CHANNEL, OCCAM_BROWSER_EXECUTABLE_PATH, OCCAM_CHROME_PATH
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --install-dir) INSTALL_DIR="$2"; shift 2 ;;
    --repo-url) REPO_URL="$2"; shift 2 ;;
    --ref) REF="$2"; shift 2 ;;
    --from-url | --from-release) RELEASE_URL="$2"; shift 2 ;;
    --manifest-url) MANIFEST_URL="$2"; shift 2 ;;
    --skip-build) SKIP_BUILD=1; shift ;;
    --skip-verify) SKIP_VERIFY=1; shift ;;
    --force-playwright) FORCE_PLAYWRIGHT=1; shift ;;
    --browser-channel) export OCCAM_BROWSER_CHANNEL="$2"; shift 2 ;;
    -h | --help) usage; exit 0 ;;
    *)
      echo "error: unknown argument: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

if [[ ! -t 0 ]]; then
  echo "warning: stdin is not a TTY (pipe install). For production, clone the repo and run ./scripts/install.sh directly." >&2
fi

SCRIPT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"

if [[ -n "$RELEASE_URL" ]]; then
  if [[ -n "${OCCAM_BROWSER_CHANNEL:-}" ]]; then
    echo "warning: OCCAM_BROWSER_CHANNEL is set — bundled Playwright Chromium is the production default for reproducible extracts." >&2
  fi

  node "$SCRIPT_ROOT/scripts/lib/install-preflight.mjs" release

  RELEASE_ARGS=(--url "$RELEASE_URL" --install-dir "$INSTALL_DIR")
  if [[ -n "$MANIFEST_URL" ]]; then
    RELEASE_ARGS+=(--manifest-url "$MANIFEST_URL")
  fi
  node "$SCRIPT_ROOT/scripts/lib/release-install.mjs" "${RELEASE_ARGS[@]}"

  export OCCAM_HOME="$INSTALL_DIR"
  cd "$INSTALL_DIR"

  VERSION="$(cat VERSION 2>/dev/null || echo unknown)"
  echo "install: level=B version=$VERSION"

  if [[ "$FORCE_PLAYWRIGHT" -eq 1 ]]; then
    echo "playwright install chromium (--force-playwright) ..."
    (cd workers/browser-extract && npx playwright install chromium)
  fi

  DOCTOR_ARGS=(--skip-build)
  bash "$SCRIPT_ROOT/scripts/occam-doctor.sh" "${DOCTOR_ARGS[@]}"

  if [[ "$SKIP_VERIFY" -eq 0 ]]; then
    node "$INSTALL_DIR/scripts/lib/verify-install.mjs" --skip-build
  fi

  echo ""
  echo "=== FF-Occam install complete (Level B) ==="
  echo "OCCAM_HOME=$INSTALL_DIR"
  echo "version=$VERSION"
  echo ""
  echo "Wire any MCP host:"
  echo "  export PATH=\"\$INSTALL_DIR/scripts:\$PATH\" && occam onboard"
  echo "  # or paste JSON:"
  node "$INSTALL_DIR/scripts/lib/print-connection-snippet.mjs" "$INSTALL_DIR" generic-stdio
  echo ""
  echo "Reload MCP servers in your host after saving."
  exit 0
fi

if [[ -z "$REPO_URL" ]]; then
  echo "error: set OCCAM_REPO_URL or pass --repo-url (Level A), or --from-url / OCCAM_RELEASE_URL (Level B)" >&2
  exit 1
fi

if [[ -n "${OCCAM_BROWSER_CHANNEL:-}" ]]; then
  echo "warning: OCCAM_BROWSER_CHANNEL is set — bundled Playwright Chromium is the production default for reproducible extracts." >&2
fi

node "$SCRIPT_ROOT/scripts/lib/install-preflight.mjs" all

parent_dir="$(dirname "$INSTALL_DIR")"
mkdir -p "$parent_dir"

if [[ -e "$INSTALL_DIR" && ! -d "$INSTALL_DIR" ]]; then
  echo "error: install path exists and is not a directory: $INSTALL_DIR" >&2
  exit 1
fi

if [[ -d "$INSTALL_DIR/.git" ]]; then
  echo "Updating $INSTALL_DIR (ref=$REF) ..."
  git -C "$INSTALL_DIR" fetch origin --tags
  if ! git -C "$INSTALL_DIR" checkout "$REF" 2>/dev/null; then
    if ! git -C "$INSTALL_DIR" checkout "origin/$REF" 2>/dev/null; then
      echo "error: cannot checkout ref: $REF" >&2
      exit 1
    fi
  fi
  if git -C "$INSTALL_DIR" show-ref --verify --quiet "refs/heads/$REF" 2>/dev/null; then
    git -C "$INSTALL_DIR" pull --ff-only origin "$REF" || {
      echo "error: git pull --ff-only failed for branch $REF — resolve manually or re-clone" >&2
      exit 1
    }
  fi
elif [[ -d "$INSTALL_DIR" ]]; then
  echo "error: $INSTALL_DIR exists but is not a git repo — remove it or pick another --install-dir" >&2
  exit 1
else
  echo "Cloning $REPO_URL (ref=$REF) into $INSTALL_DIR ..."
  git clone --depth 1 --branch "$REF" "$REPO_URL" "$INSTALL_DIR" || {
    echo "error: git clone failed for ref=$REF — check tag/branch name and repo access" >&2
    exit 1
  }
fi

export OCCAM_HOME="$INSTALL_DIR"
cd "$INSTALL_DIR"

COMMIT="$(git rev-parse --short HEAD 2>/dev/null || echo unknown)"
echo "install: level=A ref=$REF commit=$COMMIT"

if [[ "$FORCE_PLAYWRIGHT" -eq 1 ]]; then
  echo "playwright install chromium (--force-playwright) ..."
  (cd workers/browser-extract && npx playwright install chromium)
fi

DOCTOR_ARGS=()
if [[ "$SKIP_BUILD" -eq 1 ]]; then
  DOCTOR_ARGS+=(--skip-build)
fi
bash "$SCRIPT_ROOT/scripts/occam-doctor.sh" "${DOCTOR_ARGS[@]}"

if [[ "$SKIP_VERIFY" -eq 0 ]]; then
  VERIFY_ARGS=()
  if [[ "$SKIP_BUILD" -eq 1 ]]; then
    VERIFY_ARGS+=(--skip-build)
  fi
  node "$INSTALL_DIR/scripts/lib/verify-install.mjs" "${VERIFY_ARGS[@]}"
fi

echo ""
echo "=== FF-Occam install complete (Level A) ==="
echo "OCCAM_HOME=$INSTALL_DIR"
echo "ref=$REF commit=$COMMIT"
echo ""
echo "Next: export PATH=\"\$INSTALL_DIR/scripts:\$PATH\" && occam"
echo "      occam doctor   # if not already run"
echo "      occam onboard  # MCP snippet for your host"
echo "Docs: docs/operator_journey.md"
echo ""
echo "Paste-ready MCP config (generic stdio — Cursor, VS Code, bridges):"
node "$INSTALL_DIR/scripts/lib/print-connection-snippet.mjs" "$INSTALL_DIR" generic-stdio
echo ""
echo "Reload MCP servers in your host after saving."
