#!/usr/bin/env bash
# Select Level B platform tarballs under a directory for cosign signing.
# Prints one absolute-or-as-given path per line. Exit 1 when nothing matches.
#
# Intentionally excludes: source zips, manifests, .sig/.bundle, unrelated assets.
set -euo pipefail

select_release_archives() {
  local dir="${1:?usage: select_release_archives DIR}"
  # shellcheck disable=SC2034
  local _occam_nullglob_was_set=0
  shopt -q nullglob && _occam_nullglob_was_set=1
  shopt -s nullglob

  local archives=(
    "$dir"/ff-occam-*-linux-x64.tar.gz
    "$dir"/ff-occam-*-osx-arm64.tar.gz
    "$dir"/ff-occam-*-win-x64.tar.gz
  )

  if ((_occam_nullglob_was_set == 0)); then
    shopt -u nullglob
  fi

  if ((${#archives[@]} == 0)); then
    echo "error: no ff-occam-*-{linux-x64,osx-arm64,win-x64}.tar.gz under ${dir}" >&2
    return 1
  fi

  printf '%s\n' "${archives[@]}"
}

# When executed directly: select_release_archives "$1"
if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
  select_release_archives "${1:?usage: $0 DIR}"
fi
