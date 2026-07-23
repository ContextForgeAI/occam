#!/usr/bin/env bash
# CI — build Level B tarball for one RID + verify manifest/layout.
#
# Usage:
#   ./scripts/ci-release-build.sh linux-x64
#   OCCAM_RELEASE_VERSION=0.8.12 ./scripts/ci-release-build.sh win-x64
#
# Environment:
#   OCCAM_RELEASE_VERSION   override version (default: latest from CHANGELOG.md)
#   OCCAM_RELEASE_OUTPUT_DIR  default artifacts/releases
set -euo pipefail

RID="${1:-}"
if [[ -z "$RID" ]]; then
  echo "usage: ci-release-build.sh <rid>" >&2
  echo "  RIDs: win-x64 linux-x64 osx-arm64 osx-x64" >&2
  exit 1
fi

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

ARGS=(--rid "$RID")
if [[ -n "${OCCAM_RELEASE_VERSION:-}" ]]; then
  ARGS+=(--version "$OCCAM_RELEASE_VERSION")
fi
if [[ -n "${OCCAM_RELEASE_OUTPUT_DIR:-}" ]]; then
  ARGS+=(--output-dir "$OCCAM_RELEASE_OUTPUT_DIR")
fi

echo "[ci-release-build] rid=$RID OCCAM_HOME=$ROOT"

if [[ -f "$ROOT/scripts/lib/stop-occam-processes.mjs" ]]; then
  node "$ROOT/scripts/lib/stop-occam-processes.mjs" 2>/dev/null || true
fi

node "$ROOT/scripts/lib/build-release.mjs" "${ARGS[@]}"

VERIFY_ARGS=(--rid "$RID")
if [[ -n "${OCCAM_RELEASE_VERSION:-}" ]]; then
  VERIFY_ARGS+=(--version "$OCCAM_RELEASE_VERSION")
fi
if [[ -n "${OCCAM_RELEASE_OUTPUT_DIR:-}" ]]; then
  VERIFY_ARGS+=(--output-dir "$OCCAM_RELEASE_OUTPUT_DIR")
fi
node "$ROOT/scripts/lib/verify-release-artifact.mjs" "${VERIFY_ARGS[@]}"
