# FAQ

**What you'll find here:** short answers to the questions operators and agents ask most often.

---

## Is extraction local?

Yes. The host runs on your machine. Node.js workers fetch and parse pages locally. Nothing is sent to an Occam cloud service.

Optional features (web search, managed scraping APIs, LibreTranslate, time-stamp authorities) call **endpoints you configure** — see [Configuration](configuration.md).

---

## Does page content leave my machine?

By default, no. The URL is fetched by local workers; Markdown and receipts stay in the MCP response.

Content leaves your machine only when you enable an outbound integration (`OCCAM_SEARCH_*`, `OCCAM_MANAGED_*`, `OCCAM_TRANSLATE_URL`, `OCCAM_TSA_URL`, or a proxy).

---

## What does `ok: false` mean?

The tool could not produce trustworthy page content. The `failure.code` field tells you why (timeout, login wall, thin extract, HTTP 404, …).

**Never invent article text from memory when `ok` is false.** See [Concepts — trust model](concepts.md#trust-model).

---

## Which agent or model is this for?

Any MCP client — Cursor, Claude Desktop, custom agents, RAG pipelines. Tools return JSON strings; your client passes them to the model.

The product is **agent-first**: tool descriptions and [Choosing a tool](choosing-a-tool.md) are written so models pick the right call.

---

## How many tools ship by default?

**Core MCP tools** (always on). Four optional env-gated tools add batch submit/status/results, page watch, cross-check, and failure atlas — see [Tools reference — opt-in tools](tools-reference.md#opt-in-tools).

---

## Is there a file cache?

No persistent file cache. Each call does a live fetch unless you opt in to a short-lived in-memory cache via `cache_ttl_s` on `occam_transcode`.

---

## What license applies?

AGPL-3.0-or-later. See the root [LICENSE](../LICENSE).

---

## How do I verify an extraction really happened?

Use the signed `receipt` on success responses and `occam_verify` (offline) or the bundled CLI:

```bash
FFOccamMcp.Core verify --receipt receipt.json --pubkey pubkey.pem --markdown page.md
```

Details: [Receipts](receipts.md) · [Receipt verification](receipt_verification.md).
