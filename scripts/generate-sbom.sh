#!/usr/bin/env bash
set -euo pipefail
OUTPUT="${1:-sbom.spdx.json}"
PROJECT="${2:-src/FFOccamMcp.Core/FFOccamMcp.Core.csproj}"
dotnet sbom "$PROJECT" -o "$OUTPUT.dotnet.json"
syft packages dir:. -o spdx-json="$OUTPUT.syft.json"