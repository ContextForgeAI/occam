# Quick Start

Get a real web read through Occam in about **three to five minutes**.

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

Needs **Node.js 20+**. Details and air-gap options: [Install](install.md).

---

## 2. Occam connects your AI

The installer detects supported AI / MCP hosts on the machine and configures **live-validated** ones automatically (`occam connect`).

You may see:

| Status | What to do |
|--------|------------|
| **Ready** | Continue |
| **Almost ready** | Restart the named app once |
| **Action required** | Trust a folder, approve a prompt, or paste an entry — the install itself is fine |
| **Not ready** | Occam failed to start — see [Troubleshooting](troubleshooting.md) |

Re-run any time:

```bash
occam connect
```

Which hosts are automatic vs `--only`: [Supported hosts](mcp-hosts.md).

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

## 5. What success looks like

- `ok: true`
- Non-empty `markdown` (the page content)
- Often a signed `receipt` (URL, time, content hash, backend) you can verify later

If `ok: false`, read `failure.code` — do **not** invent the page. See [Failure codes](failure-codes.md).

**Stop here.** You have a working install.

---

## Next

- [What is Occam?](what-is-occam.md)
- [Read a page (guide)](guides/read-a-page.md)
- [Verify a receipt](examples/verify-receipt.md)
- [Connect another host](connect/index.md)
