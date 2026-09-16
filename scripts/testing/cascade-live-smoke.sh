#!/usr/bin/env bash
# Live cascade smoke — three public URLs. Mirrors cascade-live-smoke.ps1.
# Usage: OCCAM_HOME=/path/to/repo ./scripts/testing/cascade-live-smoke.sh [platform-label]
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
PLATFORM_LABEL="${1:-}"
if [[ -z "$PLATFORM_LABEL" ]]; then
  case "$(uname -s)" in
    Darwin*) PLATFORM_LABEL=macos-arm64 ;;
    Linux*) PLATFORM_LABEL=linux-x64 ;;
    *) PLATFORM_LABEL=unknown ;;
  esac
fi

OUT="${ROOT}/docs/testing/${PLATFORM_LABEL}"
mkdir -p "$OUT"
export OCCAM_HOME="${OCCAM_HOME:-$ROOT}"
export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export PATH="${HOME}/.dotnet:${PATH}"

LOG="${OUT}/cascade-live-smoke.log"
JSONL="${OUT}/cascade-live-smoke.jsonl"
: >"$JSONL"
{
  echo "### cascade live smoke $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "repo=${ROOT}"
  echo "platform=${PLATFORM_LABEL}"
  echo
} >"$LOG"

URLS=(
  "https://example.com"
  "https://nginx.org/"
  "https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/404"
)

ok_count=0
fail_count=0
lats=()

now_ms() {
  python3 -c 'import time; print(int(time.time()*1000))'
}

for url in "${URLS[@]}"; do
  start=$(now_ms)
  set +e
  out=$(dotnet run --project "${ROOT}/src/FFOccamMcp.Core" -c Release --no-build -- \
    cascade run --url="$url" --task=overview --budget=512 2>&1)
  exit_code=$?
  set -e
  end=$(now_ms)
  ms=$((end - start))

  json_line=$(printf '%s\n' "$out" | grep '^{' | tail -n 1 || true)
  [[ -z "$json_line" ]] && json_line='{}'

  eval "$(URL_JSON="$json_line" python3 <<'PY'
import json, os, shlex
raw = os.environ["URL_JSON"]
try:
    o = json.loads(raw)
    ok = 1 if o.get("ok") else 0
    backend = o.get("backend") or ""
    partial = "true" if o.get("partial") else "false"
    fail = ""
    if isinstance(o.get("failure"), dict):
        fail = f"{o['failure'].get('code', '')}:{o['failure'].get('message', '')}"
except Exception:
    ok, backend, partial, fail = 0, "", "false", "json_parse_error"
print(f"ok={ok}")
print(f"backend={shlex.quote(backend)}")
print(f"partial={partial}")
print(f"failure={shlex.quote(fail)}")
PY
)"

  if [[ "$exit_code" -eq 0 && "$ok" -eq 1 ]]; then
    ok_count=$((ok_count + 1))
    lats+=("$ms")
    status=OK
    row_ok=true
  else
    fail_count=$((fail_count + 1))
    status=FAIL
    row_ok=false
  fi

  python3 -c '
import json, sys
print(json.dumps({
  "url": sys.argv[1],
  "ok": sys.argv[2] == "true",
  "exitCode": int(sys.argv[3]),
  "latencyMs": int(sys.argv[4]),
  "backend": sys.argv[5] or None,
  "partial": sys.argv[6] == "true",
  "failure": sys.argv[7] or None,
}, separators=(",", ":")))
' "$url" "$row_ok" "$exit_code" "$ms" "$backend" "$partial" "$failure" >>"$JSONL"

  echo "url=${url} status=${status} exit=${exit_code} latency_ms=${ms} backend=${backend} partial=${partial} failure=${failure}" | tee -a "$LOG"
done

{
  echo
  echo "ok_count=${ok_count}"
  echo "fail_count=${fail_count}"
} | tee -a "$LOG"

if ((${#lats[@]} > 0)); then
  IFS=$'\n' sorted=($(printf '%s\n' "${lats[@]}" | sort -n))
  n=${#sorted[@]}
  p50=${sorted[$(( (n - 1) * 50 / 100 ))]}
  p95=${sorted[$(( (n - 1) * 95 / 100 ))]}
  min=${sorted[0]}
  max=${sorted[$((n - 1))]}
  sum=0
  for x in "${sorted[@]}"; do sum=$((sum + x)); done
  mean=$((sum / n))
  {
    echo "latency_n=${n}"
    echo "latency_p50_ms=${p50}"
    echo "latency_p95_ms=${p95}"
    echo "latency_mean_ms=${mean}"
    echo "latency_min_ms=${min}"
    echo "latency_max_ms=${max}"
  } | tee -a "$LOG"
else
  echo "latency_n=0" | tee -a "$LOG"
fi

if [[ "$fail_count" -eq 0 && "$ok_count" -eq ${#URLS[@]} ]]; then
  echo "CASCADE_LIVE_SMOKE_OK" | tee -a "$LOG"
  exit 0
fi
echo "CASCADE_LIVE_SMOKE_FAIL ok=${ok_count} fail=${fail_count}" | tee -a "$LOG"
exit 1
