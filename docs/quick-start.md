# Quick Start

Get a real web read through Occam in about **three to five minutes**.

This page stays short on purpose: **install → doctor/connect → first read → understand success/failure**. Playbooks, sessions, receipts, and experimental tools come *after* first success.

---

## 1. Install

=== "Linux / macOS"

    ```bash
    curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh | bash
    ```

=== "Windows"

    ```powershell
    irm https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.ps1 | iex
    ```

Needs **Node.js 20+**. The bootstrap downloads a release archive, **SHA-256-verifies** it, installs the runtime quietly, and connects a detected AI app. Default output is short; set `OCCAM_VERBOSE=1` for doctor/smoke internals. Details: [Install](install.md).

**npm / npx is not a GA 1.0 install path.**

---

## 2. Occam connects your AI

The installer detects supported AI / MCP hosts (`occam connect`).

- **One** supported host → connects automatically.  
- **Several** → confirms before writing multiple configs (or use `OCCAM_CONNECT_ALL=1` for automation).  
- **None** → Occam is still installed; connect later with `occam connect`.

| Status | What to do |
|--------|------------|
| **Ready** | Continue — host integration verified |
| **Almost ready** | Restart the named app once |
| **Action required** | Trust a folder, approve a prompt, or paste an entry — the install itself is fine |
| **Installed** | Runtime OK; no host connected yet |
| **Not ready** | Occam failed to start — see [Troubleshooting](troubleshooting.md) |

```bash
occam connect
```

Host tiers: [Supported hosts](mcp-hosts.md).

---

## 3. Restart or trust (only if asked)

If connect named a host, restart it or approve what it asks. Then continue.

---

## 4. Ask your agent

> Use Occam to read https://example.com

Or call the tool directly:

```json
{ "name": "occam_transcode", "arguments": { "url": "https://example.com" } }
```

---

## 5. What success and failure look like

**Success**

- `ok: true`
- Non-empty `markdown` (the usable page content)
- Optionally a signed `receipt` (integrity of those bytes relative to a key — not proof the page was true)

**Failure**

- `ok: false`
- Read `failure.code` and any `agentMeta.decisions`
- Do **not** invent the page from memory — content is **unknown**

See [Honest failures](trust/honest-failures.md) · [Failure codes](failure-codes.md).

**Stop here.** You have a working install.

---

## After first success

| Goal | Go here |
|------|---------|
| Difficult / JS-heavy pages | [Read a page](guides/read-a-page.md) · backend_policy |
| Login walls | [Sessions](guides/sessions.md) |
| Prove a receipt later | [Verify sources](guides/verify-sources.md) |
| Several URLs / search | [Research multiple](guides/research-multiple.md) · [Search & discover](guides/search-and-discover.md) |
| What Occam is | [What is Occam?](what-is-occam.md) |
| Deep understanding | [Handbook](handbook/index.md) |
