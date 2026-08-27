# Occam

*Local-first web context for AI agents.*

## Stop wasting your agent's context on webpage noise.

Most pages wrap the useful part in navigation, scripts, repeated interface
text, consent controls, and raw markup. Occam removes that presentation layer
before it reaches the model and returns compact, source-linked content your
agent can actually use.

It is for developers and technical users building or running AI agents,
especially in local and self-hosted environments.

Search helps an agent find pages; browser automation operates interfaces.
Occam turns a URL you already have into compact context with its source — or an
explicit failure when it cannot read the page.

**[Get your first result](https://contextforgeai.github.io/occam/quick-start/)** ·
[See the transformation](docs/examples/current-proof/README.md#representative-webpage-transformation) ·
[Read the documentation](https://contextforgeai.github.io/occam/)

> **Current status:** Published install channel is **`1.0.0-rc.4`**. The MCP host and documented connection paths are the supported
> product surface. `occam chat` and other features listed as experimental are not stable 1.0
> interfaces.

## See the webpage noise disappear

The current proof bundle includes a controlled engineering article wrapped in
navigation, search, related links, a newsletter form, a cookie notice, scripts,
layout CSS, and a footer. With the default `occam_transcode` options, Occam
kept the article structure and returned:

```markdown
# Web context without the chrome

AI agents rarely need the whole interface of a webpage. They need the useful
text, its structure, and enough source information to explain where the
material came from.
```

The controlled HTML body is **5,297 UTF-8 bytes**. Occam returned **1,736 UTF-8
bytes** of Markdown — **67.2% fewer bytes in this fixture**. No tokenizer was
used, so this is not a token-savings or answer-quality claim and not a universal
reduction rate.

Inspect the [input page](https://contextforgeai.github.io/occam/examples/current-proof/representative-input.html),
[complete output](docs/examples/current-proof/representative-output.md), and
[measurement method](docs/examples/current-proof/representative-measurement.json).
The bundle also retains the minimal live `example.com` smoke proof and a
controlled typed failure.

## Install to first result

Requires Node.js 20+. The release install does not require the .NET SDK.

**Linux x64 / macOS Apple Silicon**

```bash
curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh | bash
```

**Windows x64**

```powershell
irm https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.ps1 | iex
```

The installer adds a user-scoped launcher, creates local Occam state, downloads
browser dependencies when needed, and may update a detected host configuration
after making a backup. Review [installation safety](docs/trust/installation-safety.md)
before running it in a sensitive environment.

The currently published public release is `v1.0.0-rc.4`. It uses self-contained
archives (`runtimeLayout=self-contained-v1`): the installer matches version/RID,
verifies archive SHA-256, runs archive-member preflight before extract, verifies
Cosign when the manifest declares `signaturePolicy=required-cosign-v1` (**requires
the `cosign` CLI** — see [INSTALL.md](INSTALL.md)), checks the complete bundled
runtime before replacing an install, and does not fetch post-install helpers from
the mutable repository branch. Bootstrap scripts may still be delivered from the
mutable `main` raw URL (separate from release runtime content). It then checks
the local runtime, detects supported AI applications, and tells you whether a
restart or another action is needed.
Open a new conversation in the connected application and type:

Public release binaries are currently published for `win-x64`, `linux-x64`, and
`osx-arm64` only. Intel Macs, Linux ARM64, and Windows ARM64 fail before download
instead of receiving an incompatible archive.

> Use Occam to read https://example.com/ and summarize the page. Include the
> source URL.

Success means the application calls an Occam tool and returns content from the
live page. Installation alone is not the finish line. The
[Quick Start](docs/quick-start.md) shows exactly where to type, what to expect,
and how to recover for each workflow.

## Why Occam

### Cleaner context

Useful page content instead of navigation, scripts, repeated interface text,
and irrelevant boilerplate.

### Predictable behavior

A real result when Occam can read the source, and an explicit failure when it
cannot. `ok: false` means the page content is unknown.

### One reusable layer

A consistent web-context layer across currently supported AI tools, with
validation tiers that distinguish live-tested, config-tested, and assisted
paths.

## Choose your workflow

| Workflow | Current path |
|----------|--------------|
| Supported AI application | Install, run `occam connect` when needed, then use Occam in a new conversation |
| Cursor | Live-validated connection path; restart or reload when the connect result asks |
| Hermes Agent | Live-validated path; a failed verification can be undone and reported as **Not connected** |
| Local Ollama model | **Experimental:** `occam chat` with a locally installed, tool-capable model |
| Custom agent | Use the MCP contract and generated snippet |

Ollama is a model runtime, not an MCP host. Experimental `occam chat` calls the
documented local Ollama API and uses Occam's own acquisition tools; it is not a
native integration inside the Ollama App and does not use Ollama Web Search.

See [supported hosts and validation tiers](docs/mcp-hosts.md).

## How it works

```text
web page
  → local-first acquisition
  → useful page content
  → compact agent context
  → source and integrity information
```

Occam starts with a lightweight page read and can use a local browser when the
page requires it. Advanced controls can focus, budget, structure, or verify the
result. Start with one URL; add those controls only when the task needs them.

[How Occam works](docs/how-occam-works.md) ·
[Choose a tool](docs/choosing-a-tool.md) ·
[MCP API](MCP_API_SPEC.md)

## Trust and local control

Normal page reading runs on your machine by default. Private and local
destinations are denied unless the operator explicitly allows them. Source URLs
remain attached to results, and optional Receipt v1 artifacts can check output
integrity against a supplied key.

Local-first does not mean “never network” or “never cloud”: the origin page is a
network source, and explicitly configured search, managed acquisition, proxy,
or remote transport providers can change the boundary. Receipts prove integrity
relative to a key, not truth, identity, or authentic origin.

[Trust & Safety](docs/trust-and-safety.md) ·
[Installation safety](docs/trust/installation-safety.md) ·
[Security policy](SECURITY.md)

## Go deeper

- [Quick Start](docs/quick-start.md) — reach a real first result.
- [Install reference](INSTALL.md) — release channels, prerequisites, and verification.
- [Documentation hub](docs/index.md) — tasks, guides, and reference.
- [`llms.txt`](llms.txt) — machine-readable documentation map for agents.
- [Tool reference](docs/tools-reference.md) — current public tool contract.
- [Troubleshooting](docs/troubleshooting.md) — diagnostics and recovery.
- [Contributing](CONTRIBUTING.md) — development setup and contribution workflow.
- [Repository automation rules](AGENTS.md) — scope, doc sync, and verification requirements.
- [Review guide](REVIEW_GUIDE.md) — focused review workflow.

Occam is licensed under
[AGPL-3.0-or-later](LICENSE). Releases are published on
[GitHub](https://github.com/ContextForgeAI/occam/releases).
