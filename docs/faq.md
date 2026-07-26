# FAQ

**What you'll find here:** short answers to the questions operators and agents ask most often.

---

## Is extraction local?

Yes. The host runs on your machine. Node.js workers fetch and parse pages locally. Nothing is sent to an Occam cloud service.

Optional features (web search, managed scraping APIs, LibreTranslate, time-stamp authorities) call **endpoints you configure** — see [Configuration](configuration.md).

---

## Does page content leave my machine?

By default, only to **the URLs you ask Occam to fetch** (and your configured proxy, if any).

Content also leaves your machine when you enable an outbound integration (`OCCAM_SEARCH_*`, `OCCAM_MANAGED_*`, `OCCAM_TRANSLATE_URL`, `OCCAM_TSA_URL`). Managed extract sends the target URL to the provider you configured — it is not a default path.

Markdown and receipts in MCP responses stay on your machine unless your AI client logs them elsewhere.

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

**15 core MCP tools** (always on). Four optional env-gated tools add batch submit/status/results, page watch, cross-check, and failure atlas — see [Tools reference — opt-in tools](tools-reference.md#opt-in-tools).

---

## Is there a file cache?

**Default extract is live** — each call fetches the page again unless you opt in.

You can enable a **short-lived in-memory / temp-dir cache** on `occam_transcode` via `cache_ttl_s` (stored under `{TEMP}/occam-cache/` or `OCCAM_CACHE_DIR`). That is opt-in, TTL-bound, and replays the signed envelope when receipts are on — not a persistent page archive.

Occam also keeps **other durable local state** (sessions, keys, playbooks, watch/batch stores) — see [Sessions](sessions.md) and [Configuration](configuration.md).

---

## How do I install Occam for 1.0?

Use the **release tarball / bootstrap scripts** — see [Install](install.md).

| Channel | Supported? |
|---------|------------|
| `get-ff-occam.sh` / `.ps1` bootstrap | **Yes** (GA) |
| Manual tarball + SHA-256 manifest | **Yes** (GA) |
| `npx @ff-occam/mcp` | **No** — not a GA 1.0 install channel |
| Cosign `.bundle` alone | **No** — not verified by shipped install paths |

---

## Are releases cosign-verified?

**No.** A Cosign bundle may exist on GitHub Releases as metadata. **Installers verify SHA-256 against the release manifest only.** Do not treat Cosign as part of the shipped trust bar.

---

## What license applies?

AGPL-3.0-or-later. See the root [LICENSE](https://github.com/ContextForgeAI/occam/blob/main/LICENSE).

---

## How do I verify an extraction really happened?

Use a **signed Receipt v1** (`receipt.signed` on transcode, digest, claim-check, dataset export, watch history entries) and verify with [`occam_verify`](tools/occam_verify.md) or the bundled CLI:

```bash
FFOccamMcp.Core verify --receipt receipt.json --pubkey pubkey.pem --markdown page.md
```

**Honesty limits:**

- Verification proves **integrity under a public key you supply** — not truth, not author identity, not that the page was fetched fresh (cache replay returns the stored signed envelope).
- [`occam_extract_knowledge`](tools/occam_extract_knowledge.md) returns a **`receipt` field that is extraction telemetry only** (`confidence`, `elapsedMs`) — unsigned, **not** Receipt v1, **not** accepted by `occam_verify`.
- [`occam_crosscheck`](tools/occam_crosscheck.md) per-vantage extracts may be signed; the **agreement verdict itself is computed, not signed**.

Details: [Receipts](receipts.md) · [Receipt verification](receipt_verification.md) · [Trust & Safety](trust-and-safety.md)
