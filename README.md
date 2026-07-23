# FF-Occam MCP

**Turn any URL into clean, token-budgeted Markdown locally — with honest failures and a verifiable receipt.**

FF-Occam MCP is a [Model Context Protocol](https://modelcontextprotocol.io/) server: a Native AOT .NET host plus Node.js extract workers. Core MCP tools ship by default; batch, watch, cross-check, and failure atlas are opt-in.

[![License](https://img.shields.io/badge/license-AGPL--3.0-blue?logo=gnu)](LICENSE)

**Install:** [INSTALL.md](INSTALL.md) · **Human docs:** [docs/index.md](docs/index.md) ·
**LLM map:** [llms.txt](llms.txt) · **API:** [MCP_API_SPEC.md](MCP_API_SPEC.md) ·
**Contributing:** [AGENTS.md](AGENTS.md)

> **Release status:** Occam Core **1.0.0-rc.2** is a **GitHub tarball-only** release candidate.
>
> Occam `1.0.0-rc.2` is distributed through GitHub release archives for:
> - Linux x64
> - macOS arm64
> - Windows x64
>
> npm packages, NuGet packages, and the VS Code extension are **not**
> part of this release candidate. From a checkout, use the git-clone / doctor path below.

---

## Server / Hermes (GitHub Releases)

**No .NET 10 SDK?** Use the Level B release tarball — **not** a bare git clone.

**From downloaded assets (no checkout):** download `ff-occam-<ver>-linux-x64.tar.gz` (or
`osx-arm64` on Apple Silicon) and its `-manifest.json` from the GitHub Release, extract, then run
doctor from the extracted tree:

```bash
INSTALL_DIR="${OCCAM_INSTALL_DIR:-$HOME/.local/share/ff-occam}"
mkdir -p "$INSTALL_DIR"
tar -xzf ff-occam-1.0.0-rc.2-linux-x64.tar.gz -C "$INSTALL_DIR" --strip-components=1
export OCCAM_HOME="$INSTALL_DIR"
cd "$OCCAM_HOME"
bash scripts/occam-doctor.sh --skip-build
node scripts/hermes-smoke.mjs
```

**From an existing checkout:** set `OCCAM_RELEASE_BASE` / `OCCAM_VERSION` and run
`bash scripts/get-ff-occam.sh`, or `./scripts/install.sh --from-url <tarball-url>`.

Git clone + `./scripts/occam-doctor.sh` requires **.NET 10 SDK** on that machine. .NET 8 will not work.

Full steps: [INSTALL.md](INSTALL.md)

---

## npm packages (not part of 1.0.0-rc.2)

`@ff-occam/mcp`, `@ff-occam/agent-sdk`, and `@ff-occam/skill` remain in the repository for future
publication, but they are **not** a supported install path for this release candidate. Do not use
`npx @ff-occam/mcp` as the RC install method.

**Git clone** (contributors — .NET 10 SDK):

```powershell
git clone https://github.com/ContextForgeAI/occam.git occam
cd occam
$env:OCCAM_HOME = (Get-Location).Path
.\scripts\occam-doctor.ps1
node scripts/launch-mcp-host.mjs
```

**Cursor** — add to `.cursor/mcp.json` (clone / tarball install; set `OCCAM_HOME`):

```json
{
  "mcpServers": {
    "ff-occam": {
      "command": "node",
      "args": ["C:\\path\\to\\occam\\scripts\\launch-mcp-host.mjs"],
      "env": { "OCCAM_HOME": "C:\\path\\to\\occam" }
    }
  }
}
```

**Agent skill (any harness)** — lazy MCP orchestration for Cursor, Claude Code, Hermes, etc.
From a clone: `occam skill install` (or the packaged skill under `skills/occam/`).
`npx @ff-occam/skill` is **not** part of `1.0.0-rc.2`. See [docs/getting-started.md](docs/getting-started.md#agent-skill-any-harness).

---

## Core MCP tools

| Tool | One line |
|------|----------|
| `occam_transcode` | One page → Markdown + signed receipt |
| `occam_probe` | Cheap URL diagnosis before a full fetch |
| `occam_digest` | Up to 8 URLs → research digest |
| `occam_map` | Discover same-domain links |
| `occam_search` | Web search → candidate URLs |
| `occam_playbook_resolve` | Read-only playbook lookup |
| `occam_playbook_heal` | DOM skeleton for playbook authoring |
| `occam_playbook_save` | Save a local playbook |
| `occam_extract_knowledge` | Structured `facts[]` from playbook schema |
| `occam_verify` | Verify a signed receipt |
| `occam_claim_check` | Ground one claim in one page |
| `occam_attest` | Batch citation check |
| `occam_playbook_lint` | Validate playbook JSON |
| `occam_dataset_export` | Signed dataset export |

Agent guide: [docs/choosing-a-tool.md](docs/choosing-a-tool.md) · Reference: [docs/tools-reference.md](docs/tools-reference.md)

---

## Trust model

- `ok: true` — content came from a live extract.
- `ok: false` — content is **unknown**; read `failure.code`, never hallucinate the page.

---

## License

AGPL-3.0-or-later — see [LICENSE](LICENSE).
