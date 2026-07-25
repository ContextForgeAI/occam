# Your first web read

**What you'll do:** after Occam is installed and connected, run a successful `occam_transcode` and understand the result.

If you still need install: [Quick Start](quick-start.md) · canonical reference [Install](install.md) · root [`INSTALL.md`](https://github.com/ContextForgeAI/occam/blob/main/INSTALL.md).

---

## Confirm the host sees Occam

Add `$OCCAM_HOME/scripts` to `PATH` (default install: `~/.local/share/ff-occam`).

```bash
occam smoke
# expect exit 0 and 15 occam_* tools
```

Re-check AI host registration:

```bash
occam connect
```

Restart or trust the named app if the status is **Almost ready** or **Action required**. Host tiers: [MCP hosts](mcp-hosts.md).

---

## First call

Ask your agent:

> Use Occam to read https://example.com

Or invoke:

```json
{ "name": "occam_transcode", "arguments": { "url": "https://example.com" } }
```

### Expected success

- `ok: true`
- Non-empty `markdown`
- Optional signed `receipt` (default on)

### Expected failure handling

- `ok: false` → read `failure.code` → [Failure codes](failure-codes.md)  
- Do **not** invent page content from memory  

Optional session budget (once per chat):

```json
{ "name": "occam_client_capabilities", "arguments": { "context_tokens": 200000 } }
```

---

## Operator CLI

After installation, add `$OCCAM_HOME/scripts` to `PATH`. Run `occam --help` for the live command list.

| Command | Use it for |
|---|---|
| `occam connect` | Detect AI tools and register Occam with validated ones |
| `occam doctor` | Validate workers, browser, and host binary |
| `occam smoke` | stdio tools/list + probe |
| `occam snippet` | Paste-ready MCP config (advanced) |
| `occam status` | Install / onboarding state |
| `occam session` | Session profiles |
| `occam skill` | Portable agent skill |

---

## Session profiles (login walls)

For gated pages, export a browser session to a local profile and pass `session_profile` on extract tools. Occam does not solve CAPTCHAs. See [Sessions guide](guides/sessions.md) and [Configuration](configuration.md).

---

## Advanced: manual MCP wiring

Prefer **`occam connect`**. Use manual JSON only for generic clients or contributor checkouts.

| Field | Value |
|-------|-------|
| Command | `node` |
| Args | `["$OCCAM_HOME/scripts/launch-mcp-host.mjs"]` |
| Env | `OCCAM_HOME=<install root>` |

Server registration name: `ff-occam`.

### Wire into Cursor

If you must edit JSON by hand (connect unavailable), register:

```json
{
  "mcpServers": {
    "ff-occam": {
      "command": "node",
      "args": ["C:\\path\\to\\ff-occam\\scripts\\launch-mcp-host.mjs"],
      "env": { "OCCAM_HOME": "C:\\path\\to\\ff-occam" }
    }
  }
}
```

Prefer `occam connect` so backups and ownership checks apply. More: [Manual connect](connect/manual.md).

!!! tip "Contributor checkout"
    From a git clone with `.NET 10` SDK, set `OCCAM_HOME` to the repo root, run doctor, then `occam connect`. See [Install — Advanced](https://github.com/ContextForgeAI/occam/blob/main/INSTALL.md#advanced--contributors).

Do **not** put LLM API keys in Occam's env.

---

## Programmatic TypeScript client

From a source checkout, see the [`@ff-occam/agent-sdk` package README](https://github.com/ContextForgeAI/occam/blob/main/packages/occam-agent-sdk/README.md). Registry publication is not part of this RC.

---

## Agent skill (any harness)

Occam ships a portable skill under `skills/occam/`. Install via `occam skill install` from an Occam install. Details: [`@ff-occam/skill`](https://github.com/ContextForgeAI/occam/blob/main/packages/occam-skill/README.md).

---

## Next

- [How Occam works](how-occam-works.md)
- [Examples](examples/index.md)
- [Verify a receipt](examples/verify-receipt.md)
- [Trust & Safety](trust-and-safety.md)
