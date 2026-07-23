# Verify SLSA provenance
slsa-verifier verify-artifact occam-mcp-win-x64.exe `
  --provenance-path occam-mcp-provenance.intoto.jsonl `
  --source-uri github.com/FF-Occam/FFOccamMCP

# Verify cosign signature
cosign verify-blob --bundle occam-mcp-win-x64.exe.bundle occam-mcp-win-x64.exe

# Verify SBOM
syft packages occam-mcp-win-x64.exe -o table