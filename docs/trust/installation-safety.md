# Installation and connect safety

## Release artifacts

The canonical installer downloads a GitHub Release archive and verifies **SHA-256** against the published `*-manifest.json` before extract.

**Cosign honesty:** a `.bundle` file may appear on Releases as metadata. **No shipped install or update path verifies Cosign.** Do not describe releases as cosign-verified. Integrity for operators is **SHA-256 manifest matching only**.

**npm honesty:** `npx @ff-occam/mcp` is **not** a GA 1.0 install channel. Use the release tarball / bootstrap scripts documented in [Install](../install.md).

## What install writes

| Location | Sensitivity |
|----------|-------------|
| `OCCAM_HOME` install tree | Product binaries, workers, scripts |
| `~/.occam/onboard.json` | Operator env defaults (merged on every launch) |
| `~/.occam/keys/signing-key.pem` | **Critical** — ECDSA signing key, minted on first host start |
| Host MCP configs + `*.occam-bak` | Connect mutations |
| Playwright browser cache | Large but not secret |

Removing the install directory does **not** uninstall all Occam state. Session profiles, keys, watch/batch stores, and host configs may remain under `~/.occam/` and your AI host config paths.

## Config changes (`occam connect`)

When Occam registers itself with an AI host it:

- **Never overwrites** an existing `ff-occam` entry it did not create (unless you pass `--force`)  
- **Backs up** before writing  
- Writes **atomically** (no half-written config on interrupt)  
- **Rolls back** a broken registration it owns  
- **Keeps** registrations that only need a restart or trust prompt  
- **Does not mutate desktop configs in CI** by default  

Connect makes **no network calls** for registration — it only touches local files and may start the local Occam server to verify it responds.

## Sessions and secrets

- Session profiles are local JSON under `OCCAM_SESSIONS_ROOT` (default `~/.occam/sessions/`)  
- They contain **secrets** (cookies, headers, Playwright `storageState`)  
- `occam session import` does **not** retain plaintext sources under `_imports/` by default — use `--keep-import` only when intended  
- Do not commit profiles  
- Do not put LLM API keys in Occam's environment  

## Managed egress (optional)

If you configure managed extract providers (`OCCAM_MANAGED_*`), page URLs and fetch parameters leave your machine to the provider you chose. That is separate from the default local HTTP/browser path. See [Configuration](../configuration.md) and [Trust: local-first](local-first.md).

## Next

- [Install](../install.md)
- [Supported hosts](../mcp-hosts.md)
- [Trust & Safety](../trust-and-safety.md)
- [Security policy](security-policy.md)
