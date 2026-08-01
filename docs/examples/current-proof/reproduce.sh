#!/usr/bin/env bash
set -euo pipefail

fixture_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$fixture_dir/../../.." && pwd)"

cd "$repo_root"
node "$fixture_dir/reproduce.mjs"
node "$fixture_dir/reproduce-representative.mjs"
