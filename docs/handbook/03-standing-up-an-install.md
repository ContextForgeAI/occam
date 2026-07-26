# Chapter 3 — Standing up an install you can test this book against

**Part A — Orientation** · Prerequisites: [Chapter 1](01-what-occam-is.md) · Next: [Chapter 4](04-request-path.md)

---

## Mental model

**Install is a prerequisite, not the subject.**

You need: doctor → connect one MCP host → one successful `occam_transcode` → run this book's early CHECKs. Full operator depth is deferred; here the goal is a falsifiable baseline on your machine.

---

## Explanation

### Minimum path

1. **Set `OCCAM_HOME`** to the repository or install root (directory containing `workers/`, `scripts/`, `src/`).
2. **Run doctor** — installs Node dependencies, Playwright Chromium, publishes the host binary.
   ```powershell
   $env:OCCAM_HOME = "C:\path\to\FFOccamMCP"
   .\scripts\occam-doctor.ps1
   ```
   On Unix: `./scripts/occam-doctor.sh` with `export OCCAM_HOME="$(pwd)"`.
3. **Wire MCP** — copy `.cursor/mcp.json.example` or use `occam connect` for your host. Canonical launcher: `node scripts/launch-mcp-host.mjs` (stdio). The launcher is stdio-only and forwards no CLI args to alternate transports.
4. **Declare client budget once** — `occam_client_capabilities(context_tokens=…)` sizes later reads to ~20% of that window when `max_tokens` is omitted.
5. **First read** — `occam_transcode({ "url": "https://example.com/" })` or a docs index from your smoke corpus.

### What install writes (preview)

Install and connect are **destructive and persistent** in places:

- **`~/.occam/onboard.json`** — written during onboarding **before** verification; merged into every later launcher invocation's environment.
- **`~/.occam/keys/signing-key.pem`** — minted on first host start **regardless of `OCCAM_RECEIPTS`**.
- **Host MCP config files** — connect mutates third-party configs; backup before connect; rollback may be incomplete for restart-required hosts.
- **No rollback on reinstall** — Level A/B install replaces in place.

### What not to use as GA install

| Channel | Status |
|---------|--------|
| **`npx @ff-occam/mcp`** | Not GA; not a supported 1.0 install path (OD-3) |
| **Cosign bundle alone** | Release metadata; no shipped install path verifies it (OD-2) |
| **Docker HEALTHCHECK `--version`** | Unsupported verb; do not treat as production health signal |

Use tarball + manifest **sha256** verification for Level B installs ([install.md](../install.md)).

### Task R step 0

After connect: `occam_client_capabilities` once, then transcode the API docs index (or your chosen smoke URL). You will annotate this call in [Chapter 4](04-request-path.md).

---

## CHECK

**LOCAL** — Key mint vs receipts flag.

1. Start the host with `OCCAM_RECEIPTS=off`.
2. List `~/.occam/keys/` (or `%USERPROFILE%\.occam\keys\` on Windows).

A signing key is present anyway. This is your first evidence that `OCCAM_RECEIPTS` is **not** a master switch ([Chapter 14](14-what-a-receipt-proves.md)).

Optional: run CHECKs from Chapters 1–2 on the same host.

---

## Common misconception

**"`npx @ff-occam/mcp` is the quick path."**

npm is classified INTERNAL/EXPERIMENTAL until an end-to-end install contract passes. Use doctor + tarball paths documented in [install.md](../install.md) and [getting-started.md](../getting-started.md).

---

## Limitations

- This chapter is one deliberate path; OS matrices live in public install docs.
- Connect can affect unrelated Occam installs on the same machine when using operator refresh later ([getting-started.md](../getting-started.md)).
- WebSocket/remote/batch modes exist but are not reachable through the canonical stdio launcher without alternate entrypoints ([transports.md](../transports.md)).
- Uninstalling the tree leaves `~/.occam`, host configs, skills, and Playwright cache behind.

---

## Links

**Public docs:** [Install](../install.md) · [Getting started](../getting-started.md) · [Connect](../connect/index.md) · [MCP hosts](../mcp-hosts.md) · [Configuration](../configuration.md)

**Next chapter:** [Chapter 4 — The request path](04-request-path.md)
