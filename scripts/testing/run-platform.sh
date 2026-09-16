#!/bin/sh
# Cross-platform verification run for the Occam proof-of-read canary.
# Produces the artefacts referenced by docs/testing/RESULTS.md. Stays inside ~/occam-test.
set -u

PLATFORM="${1:?usage: run-platform.sh <platform-label> <rid>}"
RID="${2:?usage: run-platform.sh <platform-label> <rid>}"

ROOT="$HOME/occam-test"
SRC="$ROOT/occam"
OUT="$ROOT/results/$PLATFORM"
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
DOTNET="$DOTNET_ROOT/dotnet"

mkdir -p "$OUT"

# ---------- runtime info ----------
{
  echo "platform_label=$PLATFORM"
  echo "rid=$RID"
  echo "collected_at=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo
  echo "== uname =="
  uname -a
  echo
  if [ -x /usr/bin/sw_vers ]; then
    echo "== sw_vers =="
    sw_vers
    echo
  fi
  if [ -r /etc/os-release ]; then
    echo "== os-release =="
    cat /etc/os-release
    echo
  fi
  echo "== cpu =="
  if command -v sysctl >/dev/null 2>&1 && sysctl -n machdep.cpu.brand_string >/dev/null 2>&1; then
    sysctl -n machdep.cpu.brand_string
    echo "cores=$(sysctl -n hw.ncpu)"
    echo "mem_bytes=$(sysctl -n hw.memsize)"
  else
    grep -m1 '^model name' /proc/cpuinfo 2>/dev/null || echo "model name: unknown"
    echo "cores=$(nproc 2>/dev/null || echo unknown)"
    echo "mem_kb=$(awk '/MemTotal/ {print $2}' /proc/meminfo 2>/dev/null || echo unknown)"
  fi
  echo
  echo "== dotnet =="
  "$DOTNET" --version
  "$DOTNET" --info 2>/dev/null | head -20
} > "$OUT/runtime-info.txt" 2>&1

# ---------- build ----------
cd "$SRC" || exit 90
BUILD_START=$(date +%s)
{
  echo "### dotnet restore"
  "$DOTNET" restore src/FFOccamMcp.Core/FFOccamMcp.Core.csproj
  echo "restore_core_exit=$?"
  "$DOTNET" restore tests/OccamMcp.Core.Tests/OccamMcp.Core.Tests.csproj
  echo "restore_tests_exit=$?"
  echo
  echo "### dotnet build -c Release (Core)"
  "$DOTNET" build src/FFOccamMcp.Core/FFOccamMcp.Core.csproj -c Release --no-restore
  echo "build_core_exit=$?"
  echo
  echo "### dotnet build -c Release (Tests, warnings-as-errors)"
  "$DOTNET" build tests/OccamMcp.Core.Tests/OccamMcp.Core.Tests.csproj -c Release --no-restore
  echo "build_tests_exit=$?"
} > "$OUT/build.log" 2>&1
BUILD_EXIT=$?
BUILD_END=$(date +%s)
echo "build_wall_seconds=$((BUILD_END - BUILD_START))" >> "$OUT/build.log"
echo "build_script_exit=$BUILD_EXIT" >> "$OUT/build.log"

# ---------- tests + coverage ----------
{
  echo "### dotnet test --collect:XPlat Code Coverage"
  "$DOTNET" test tests/OccamMcp.Core.Tests/OccamMcp.Core.Tests.csproj \
    -c Release --no-build \
    --collect:"XPlat Code Coverage" \
    --results-directory "$OUT/TestResults"
  echo "test_exit=$?"
} > "$OUT/test.log" 2>&1

COVERAGE=$(find "$OUT/TestResults" -name 'coverage.cobertura.xml' 2>/dev/null | head -1)
if [ -n "$COVERAGE" ]; then
  cp "$COVERAGE" "$OUT/coverage.cobertura.xml"
fi

# ---------- canary: state machine, vectors, HTTP round trip ----------
CORE_DLL="$SRC/src/FFOccamMcp.Core/bin/Release/net10.0/OccamMcp.Core.dll"
{
  echo "### occam canary selftest"
  "$DOTNET" "$CORE_DLL" canary selftest
  echo "selftest_exit=$?"
  echo
  echo "### occam canary vectors --verify docs/testing/canary-vectors.json"
  "$DOTNET" "$CORE_DLL" canary vectors --verify docs/testing/canary-vectors.json
  echo "vectors_exit=$?"
  echo
  echo "### re-emitted vectors (byte comparison lives in canary-vectors.log, via --out + SHA-256)"
  "$DOTNET" "$CORE_DLL" canary vectors --emit --out "$OUT/canary-vectors-local.json"
  echo "emit_exit=$?"
} > "$OUT/canary-smoke.log" 2>&1

{
  echo "### occam canary smoke (HTTP issue -> read -> verify -> reject)"
  "$DOTNET" "$CORE_DLL" canary smoke
  echo "smoke_exit=$?"
} >> "$OUT/canary-smoke.log" 2>&1

# ---------- capability exam: grading, tiering, cache, hysteresis ----------
{
  echo "### occam exam selftest"
  "$DOTNET" "$CORE_DLL" exam selftest
  echo "selftest_exit=$?"
  echo
  echo "### occam exam tasks (catalogue must be LF-only and platform-identical)"
  "$DOTNET" "$CORE_DLL" exam tasks --out "$OUT/exam-tasks.json"
  echo "tasks_exit=$?"
  if command -v sha256sum >/dev/null 2>&1; then
    echo "tasks_sha256=$(sha256sum "$OUT/exam-tasks.json" | awk '{print $1}')"
  else
    echo "tasks_sha256=$(shasum -a 256 "$OUT/exam-tasks.json" | awk '{print $1}')"
  fi
  echo "tasks_bytes=$(wc -c < "$OUT/exam-tasks.json" | tr -d ' ')"
  if LC_ALL=C grep -q $'\r' "$OUT/exam-tasks.json"; then
    echo "tasks_lf_only=no"
  else
    echo "tasks_lf_only=yes"
  fi
} > "$OUT/exam-selftest.log" 2>&1

# ---------- Native AOT publish (may legitimately fail without a system toolchain) ----------
AOT_START=$(date +%s)
{
  echo "### dotnet publish -c Release -r $RID (Native AOT)"
  "$DOTNET" publish src/FFOccamMcp.Core/FFOccamMcp.Core.csproj -c Release -r "$RID" \
    -o "$ROOT/publish/$PLATFORM"
  echo "publish_exit=$?"
} > "$OUT/aot-publish.log" 2>&1
AOT_END=$(date +%s)
echo "publish_wall_seconds=$((AOT_END - AOT_START))" >> "$OUT/aot-publish.log"

BIN="$ROOT/publish/$PLATFORM/OccamMcp.Core"
{
  if [ -f "$BIN" ]; then
    echo "binary=$BIN"
    echo "size_bytes=$(wc -c < "$BIN" | tr -d ' ')"
    echo "file_type=$(file -b "$BIN" 2>/dev/null || echo unknown)"
  else
    echo "binary=absent"
    echo "note=Native AOT publish did not produce a binary on this host; see aot-publish.log"
  fi
} > "$OUT/binary-size.txt" 2>&1

{
  if [ -f "$BIN" ]; then
    if command -v sha256sum >/dev/null 2>&1; then
      sha256sum "$BIN"
    else
      shasum -a 256 "$BIN"
    fi
  else
    echo "no binary to hash"
  fi
} > "$OUT/hash.txt" 2>&1

# AOT binary must reproduce the same vectors as the JIT build.
{
  if [ -f "$BIN" ]; then
    echo "### AOT binary: canary selftest"
    "$BIN" canary selftest
    echo "aot_selftest_exit=$?"
    echo
    echo "### AOT binary: canary vectors --verify"
    "$BIN" canary vectors --verify "$SRC/docs/testing/canary-vectors.json"
    echo "aot_vectors_exit=$?"
    echo
    echo "### AOT binary: canary smoke"
    "$BIN" canary smoke
    echo "aot_smoke_exit=$?"
    echo
    echo "### AOT binary: exam selftest"
    "$BIN" exam selftest
    echo "aot_exam_exit=$?"
  else
    echo "skipped: no AOT binary on this host"
  fi
} > "$OUT/canary-aot.log" 2>&1

echo "RUN_COMPLETE $PLATFORM $(date -u +%Y-%m-%dT%H:%M:%SZ)"
