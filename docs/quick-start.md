# Quick Start

**Your first Occam result** — install → connect (or chat) → one successful URL read.

This page is the canonical post-install next step. Keep it short. Playbooks, sessions, and receipts come *after* first success.

---

## What Occam is

Occam is a local helper that lets an AI app **read a real web page now** and return compact Markdown — or an honest failure when the page content is unknown.

You usually talk to Occam **inside your AI app** (as tools), not by typing `occam` in a shell. The CLI is for install, connect, doctor, and the optional `occam chat` path.

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

Needs **Node.js 20+**. The installer downloads a release archive, verifies it, installs the runtime, then tries to connect a supported AI app. Default output is short; set `OCCAM_VERBOSE=1` for internals. Details: [Install](install.md).

**npm / npx is not a GA 1.0 install path.**

---

## 2. Pick your path

Read the installer’s final block — then follow **exactly one** branch.

### A. Ollama is installed (local chat)

```bash
occam chat
```

Select the recommended model when asked. Then ask a URL question, for example:

> Read https://example.com and summarize it

**You know it worked** when the answer uses live page text (title/body), not a generic “I can’t browse.”

### B. Cursor / Claude / Hermes / another MCP host is Ready or needs restart

1. If the installer said **Needs restart** — restart or reload that app once.
2. Open a **new** chat in that app (not an old session).
3. Ask exactly:

> Use Occam to read https://example.com

**You know it worked** when the app calls an Occam tool (often shown as `occam_transcode` / similar) and returns real page content with `ok: true` semantics — not “I don’t know what Occam is” and not `which occam` in the shell.

Occam is an **MCP tool** inside the host. The host should not look for a global `occam` binary to “use Occam.”

### C. No supported app is ready

Installer meaning: Occam itself is installed; **no AI app is connected yet**.

```text
What to do now:
1. Install a supported AI app (Cursor, Claude, Hermes, …), then:
     occam connect
2. Or, if Ollama is present:
     occam chat
```

Host list: [Supported hosts](mcp-hosts.md).

---

## 3. Connect later

```bash
occam connect
```

| Installer status | Meaning | Your next move |
|------------------|---------|----------------|
| **Connected and ready** | Host sees Occam tools | New chat → prompt above |
| **Needs restart** | Config OK; session stale | Restart/reload → new chat → prompt |
| **Not connected** | Attempt undone or never verified | Do **not** test Occam in that app; use another path or fix then `occam connect` |
| **Needs your action** | Trust / permission / conflict | Complete the named step, then verify |
| **No apps ready** | Runtime OK only | `occam chat` and/or `occam connect` after installing a host |

---

## 4. What success and failure look like

**Success**

- Tool result with usable page Markdown
- Often `ok: true` and non-empty content

**Failure (honest)**

- `ok: false` — page content is **unknown**
- Read `failure.code` — do **not** invent the page from memory

See [Honest failures](trust/honest-failures.md) · [Failure codes](failure-codes.md).

**Stop here.** You have a working first result.

---

## If connection failed

1. Read the installer “Not connected” / “Needs your action” lines — they are authoritative.
2. Do not retry the failed app as if it were ready.
3. Prefer `occam chat` when Ollama exists, or fix the host and run `occam connect`.
4. Still stuck: [Troubleshooting](troubleshooting.md).

---

## After first success

| Goal | Go here |
|------|---------|
| Difficult / JS-heavy pages | [Read a page](guides/read-a-page.md) |
| Login walls | [Sessions](guides/sessions.md) |
| Prove a receipt later | [Verify sources](guides/verify-sources.md) |
| Several URLs / search | [Research multiple](guides/research-multiple.md) |
| What Occam is (deeper) | [What is Occam?](what-is-occam.md) |
| Handbook | [Handbook](handbook/index.md) |
