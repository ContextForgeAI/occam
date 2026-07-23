#!/usr/bin/env bash
# PB4c maintainer publish CLI — sanitized export + PULL_REQUEST.md (no auto-upload)
set -euo pipefail

ROOT="${OCCAM_HOME:-$(cd "$(dirname "$0")/.." && pwd)}"
export OCCAM_HOME="$ROOT"
CLI="$ROOT/scripts/lib/playbook-publish.mjs"

if [[ ! -f "$CLI" ]]; then
  echo "error: missing publish CLI module: $CLI" >&2
  exit 2
fi

exec node "$CLI" "$@"
