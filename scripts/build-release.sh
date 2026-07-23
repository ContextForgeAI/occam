#!/usr/bin/env bash
# Build per-RID release tarball + sha256 manifest (P2-5b Level B).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RID=""
VERSION=""
OUTPUT_DIR=""

usage() {
  cat <<'EOF'
Usage: build-release.sh --rid <rid> [--version VER] [--output-dir DIR]

Supported RIDs: win-x64, linux-x64, osx-arm64, osx-x64
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --rid) RID="$2"; shift 2 ;;
    --version) VERSION="$2"; shift 2 ;;
    --output-dir) OUTPUT_DIR="$2"; shift 2 ;;
    -h | --help) usage; exit 0 ;;
    *) echo "error: unknown argument: $1" >&2; usage >&2; exit 1 ;;
  esac
done

if [[ -z "$RID" ]]; then
  echo "error: --rid is required" >&2
  usage >&2
  exit 1
fi

ARGS=(--rid "$RID")
[[ -n "$VERSION" ]] && ARGS+=(--version "$VERSION")
[[ -n "$OUTPUT_DIR" ]] && ARGS+=(--output-dir "$OUTPUT_DIR")

node "$ROOT/scripts/lib/build-release.mjs" "${ARGS[@]}"
