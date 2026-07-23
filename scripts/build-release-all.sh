#!/usr/bin/env bash
# Build Level B tarballs for all supported RIDs (local maintainer).
# Cross-RID builds may require the matching OS (osx-* on macOS).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RIDS=(linux-x64 win-x64 osx-arm64 osx-x64)

while [[ $# -gt 0 ]]; do
  case "$1" in
    --version)
      export OCCAM_RELEASE_VERSION="$2"
      shift 2
      ;;
    --output-dir)
      export OCCAM_RELEASE_OUTPUT_DIR="$2"
      shift 2
      ;;
    -h | --help)
      echo "usage: build-release-all.sh [--version VER] [--output-dir DIR]"
      exit 0
      ;;
    *)
      echo "error: unknown argument: $1" >&2
      exit 1
      ;;
  esac
done

for rid in "${RIDS[@]}"; do
  echo ""
  echo "=== build-release-all: $rid ==="
  bash "$ROOT/scripts/ci-release-build.sh" "$rid"
done

echo ""
echo "build-release-all: OK (${#RIDS[@]} tarballs)"
