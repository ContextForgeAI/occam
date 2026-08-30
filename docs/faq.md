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

The product default is `OCCAM_PROFILE=reader`, which exposes **8** day-to-day
tools. `OCCAM_PROFILE=full` exposes the complete **15-tool** core catalog.
Environment-gated batch, watch, cross-check, failure-atlas, and browser-action
tools can add more — runtime `tools/list` is authoritative. See
[Configuration — profiles](configuration.md#tool-surface-profile-occam_profile)
and [Tools reference — opt-in tools](tools-reference.md#opt-in-tools).

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
| `get-ff-occam.sh` / `.ps1` bootstrap | **Yes** — supported release channel |
| Manual tarball + SHA-256 manifest | **No** — integrity inspection only; use the guarded bootstrap to install |
| `npx ff-occam@1.0.0-rc.5` | Experimental npm RC — primary package name; not the guarded GA install path |
| `npx @ff-occam/mcp` | Low-level npm entry — not the primary public package name |
| Cosign `.bundle` alone | **No** — the bootstrap must verify it under the manifest policy |

---

## Are releases cosign-verified?

Published `v1.0.0-rc.3` and later declare
`signaturePolicy=required-cosign-v1`. The bootstrap always verifies SHA-256 and,
when that policy is declared, also verifies the Cosign bundle fail-closed
(requiring the `cosign` CLI). Legacy `v1.0.0-rc.2` remains SHA-256-only.
Cosign proves release authenticity relative to the configured workflow identity,
not page-content truth.

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
