#!/bin/sh
# Re-derives the committed canary vectors on this host and compares the emitted FILE bytes, not just
# the sentinel values. Writing through --out avoids shell newline rewriting, so a mismatch here is a
# real portability defect rather than a pipeline artefact.
set -u

PLATFORM="${1:?usage: verify-vectors.sh <platform-label>}"
ROOT="$HOME/occam-test"
SRC="$ROOT/occam"
OUT="$ROOT/results/$PLATFORM"
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
DOTNET="$DOTNET_ROOT/dotnet"
CORE_DLL="$SRC/src/FFOccamMcp.Core/bin/Release/net10.0/OccamMcp.Core.dll"

mkdir -p "$OUT"
cd "$SRC" || exit 90

sha256() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | awk '{print $1}'
  else
    shasum -a 256 "$1" | awk '{print $1}'
  fi
}

{
  echo "### rebuild with the LF-pinned JSON writer"
  "$DOTNET" build src/FFOccamMcp.Core/FFOccamMcp.Core.csproj -c Release --nologo 2>&1 | tail -4
  echo "build_exit=$?"
  echo

  echo "### semantic check: re-derive every committed vector"
  "$DOTNET" "$CORE_DLL" canary vectors --verify docs/testing/canary-vectors.json
  echo "vectors_exit=$?"
  echo

  echo "### byte check: emit to a file and compare SHA-256 with the committed baseline"
  "$DOTNET" "$CORE_DLL" canary vectors --emit --out "$OUT/canary-vectors-local.json"
  BASE_HASH=$(sha256 docs/testing/canary-vectors.json)
  LOCAL_HASH=$(sha256 "$OUT/canary-vectors-local.json")
  echo "baseline_sha256=$BASE_HASH"
  echo "local_sha256=$LOCAL_HASH"
  echo "baseline_bytes=$(wc -c < docs/testing/canary-vectors.json | tr -d ' ')"
  echo "local_bytes=$(wc -c < "$OUT/canary-vectors-local.json" | tr -d ' ')"
  if [ "$BASE_HASH" = "$LOCAL_HASH" ]; then
    echo "vectors_byte_identical=yes"
  else
    echo "vectors_byte_identical=no"
    diff -u docs/testing/canary-vectors.json "$OUT/canary-vectors-local.json" || true
  fi
} > "$OUT/canary-vectors.log" 2>&1

cat "$OUT/canary-vectors.log" | grep -E 'vectors_exit|sha256|bytes=|identical|CANARY'
