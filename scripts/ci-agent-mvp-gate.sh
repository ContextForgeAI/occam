#!/usr/bin/env bash
# CI / Hermes — publish AOT (doctor) then Agent-First MVP gate on subprocess MCP.
#
# Usage (from repo root):
#   ./scripts/ci-agent-mvp-gate.sh
#
# Environment:
#   OCCAM_HOME              repo root (default: parent of scripts/)
#   CI_AGENT_MVP_SKIP_DOCTOR=1   skip dotnet publish when binary already built
#   CI_AGENT_MVP_LATENCY=1       pass --latency to run-agent-mvp-gate.mjs (slower)
#
# Exits non-zero on doctor or gate failure. Prints JSON summary on last stdout line.
set -euo pipefail

ROOT="${OCCAM_HOME:-$(cd "$(dirname "$0")/.." && pwd)}"
export OCCAM_HOME="$ROOT"
unset OCCAM_FORCE_DOTNET_RUN 2>/dev/null || true

echo "[ci-agent-mvp-gate] OCCAM_HOME=$ROOT"

if [[ "${CI_AGENT_MVP_SKIP_DOCTOR:-0}" != "1" ]]; then
  echo "[ci-agent-mvp-gate] running occam-doctor (AOT publish) ..."
  bash "$ROOT/scripts/occam-doctor.sh"
else
  echo "[ci-agent-mvp-gate] CI_AGENT_MVP_SKIP_DOCTOR=1 — skipping doctor"
fi

GATE_ARGS=(--skip-refresh)
if [[ "${CI_AGENT_MVP_LATENCY:-0}" == "1" ]]; then
  GATE_ARGS+=(--latency)
fi

echo "[ci-agent-mvp-gate] running run-agent-mvp-gate.mjs ${GATE_ARGS[*]} ..."
exec node "$ROOT/scripts/run-agent-mvp-gate.mjs" "${GATE_ARGS[@]}"
