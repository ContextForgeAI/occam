# Local-first model

Occam is designed to run as a **local** MCP server on your machine.

## What that means

- Your AI host starts Occam as a local process (stdio by default).  
- HTML extraction and Markdown compilation happen locally via Node.js workers.  
- There is no required cloud API key for core extract tools.  
- Session profiles, receipt keys, saved playbooks, watch/batch stores, and opt-in response cache files are **local on-disk state** — not a remote Occam service.  

## What leaves your machine

By default, only **the URLs you ask Occam to fetch** (and DNS lookups for those hosts).

Optional integrations send data to endpoints **you configure**:

| Integration | What leaves |
|-------------|-------------|
| Web search (`OCCAM_SEARCH_*`) | Query + provider API traffic |
| Translation (`OCCAM_TRANSLATE_URL`) | Text you send for translation |
| Time anchor (`OCCAM_TSA_URL`) | Hash of signature bytes to your TSA |
| HTTP(S) proxy (`OCCAM_HTTP_PROXY`, …) | Traffic routed through your proxy — not Occam cloud |

Acquisition stays on the **local** HTTP → browser ladder. There is no third-party scrape escalation rung.

Fetched web content is **untrusted input**. Occam extracts text; it does not treat page JavaScript as trusted code beyond the browser sandbox used for rendering.

## Live extract vs durable state

**Default extract is live** — Occam does not reuse prior page content from a persistent page cache unless you opt in (`cache_ttl_s` on `occam_transcode`).

That does **not** mean Occam is stateless. Keys, sessions, playbooks, watch/batch JSON stores, connect backups, and optional temp cache directories persist on disk. See [Configuration](../configuration.md) and [Sessions](../sessions.md).

## What Occam does not do (default product)

- No telemetry endpoint  
- No automatic update phone-home as part of a normal extract  
- No cloud middleman for ordinary `occam_transcode` / `occam_digest` flows  
- No CAPTCHA solving  

See also: [Trust & Safety](../trust-and-safety.md) · [Configuration](../configuration.md) · [Networking](../networking.md)
