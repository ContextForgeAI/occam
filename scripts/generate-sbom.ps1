# generate-sbom.ps1
param(
    [string]$OutputPath = "sbom.spdx.json",
    [string]$ProjectPath = "src/FFOccamMcp.Core/FFOccamMcp.Core.csproj"
)

# 1. .NET SBOM (NuGet packages)
dotnet sbom "$ProjectPath" -o "$OutputPath.dotnet.json"

# 2. Syft SBOM (covers native deps, system packages)
& syft packages dir:. -o spdx-json="$OutputPath.syft.json"

# 3. Merge (use spdx-merger or manual)
# For now, upload both as artifacts