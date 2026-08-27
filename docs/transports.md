# Transports

**What you'll do:** run the host over stdio, WebSocket, Remote MCP, or the experimental batch HTTP server.

---

## stdio (default)

MCP clients spawn the host as a child process. JSON-RPC frames on **stdin/stdout** only.

```bash
node scripts/launch-mcp-host.mjs
# Experimental npm (not GA): npx ff-occam   or   npx @ff-occam/mcp
```

**Rule:** stdout = MCP JSON only. Banners and logs go to stderr (`OCCAM_BANNER`, `OCCAM_LOG`).

---

## WebSocket (legacy / advanced)

For clients that connect to a long-lived server over raw WebSocket JSON-RPC:

```bash
OccamMcp.Core --mcp-server
OccamMcp.Core --mcp-server --port 5050
# Experimental npm (not GA): npx ff-occam --mcp-server
```

| Setting | Default |
|---------|---------|
| Bind | `127.0.0.1` only |
| Port | `5050` |

.NET host equivalent: `OccamMcp.Core --mcp-server [--port N]`

---

## Streamable HTTP (MCP 2026-07-28)

Standard **Streamable HTTP** transport at **`POST /mcp`** (ModelContextProtocol.AspNetCore 2.2).
Local-first: loopback bind only unless you explicitly change `--bind`.

```bash
OccamMcp.Core --mcp-http
OccamMcp.Core --streamable-http --port 5055
```

| Setting | Default |
|---------|---------|
| Bind | `127.0.0.1` |
| Port | `5055` |
| MCP endpoint | `http://127.0.0.1:5055/mcp` |
| Health | `GET /health` → `{"ok":true,"mode":"streamable-http","endpoint":"/mcp"}` |

Modern clients use **`server/discover`** with per-request `_meta` (protocol `2026-07-28`).
Legacy **`initialize`** clients continue to work on stdio and WebSocket.

**Capability honesty:** the host advertises tools with `listChanged: false` and does not claim
a logging capability it does not push on the default path. Clients must not wait for
`notifications/tools/list_changed` after profile/env changes — restart the host instead.

**Auth (rc.4):** Streamable HTTP is **loopback-first and unauthenticated**. There is **no OAuth**
on `/mcp` in this release. For authenticated remote agents use **Remote MCP** (`--remote` + JWT /
OIDC) below — not Streamable HTTP.

Smoke (dual-era + Streamable HTTP):

```bash
OCCAM_HOME=$PWD OCCAM_FORCE_DOTNET_RUN=1 node scripts/lib/mcp-dual-era.selftest.mjs
OCCAM_HOME=$PWD OCCAM_FORCE_DOTNET_RUN=1 node scripts/lib/mcp-streamable-http.selftest.mjs
```

Authenticated remote WSS (`--remote`) is a **separate** advanced path — not Streamable HTTP.

---

## Remote MCP (TLS + JWT)

Authenticated WSS for remote agents. This is Occam's WebSocket transport carrying MCP JSON-RPC;
it is not MCP Streamable HTTP.

```bash
export OCCAM_TLS_CERT_PASSWORD='use-a-secret-store'
OccamMcp.Core --remote \
  --bind 0.0.0.0 \
  --tls-cert /path/to/cert.pfx \
  --jwt-issuer https://identity.example \
  --jwt-audience occam-mcp
```

| Setting | Default |
|---------|---------|
| Bind | `127.0.0.1`; pass a numeric IP such as `0.0.0.0` to accept remote connections |
| Port | `8443` |
| TLS cert | `OCCAM_TLS_CERT_PATH` or `--tls-cert` |
| JWT issuer / audience | `occam-mcp`; issuer must be an HTTPS discovery base unless metadata is explicit |
| OIDC metadata | `OCCAM_JWT_METADATA_URI` or `--jwt-metadata-uri`; HTTPS only |
| Concurrent sessions | `4`; set `OCCAM_REMOTE_MAX_SESSIONS` to `1`–`32` |
| Message size | `4 MiB`; set `OCCAM_MCP_MAX_MESSAGE_BYTES` from `64 KiB` to `16 MiB` |

The WebSocket upgrade must carry `Authorization: Bearer <access-token>`. Tokens in `?token=` or
`?access_token=` are rejected with `400 query_token_forbidden` because URI tokens leak into logs and
history. The JWT must be signed, unexpired, and match both issuer and audience. Key rotation comes
from the OpenID Connect metadata document and its `jwks_uri`; a raw JWKS document is not a metadata
document. `OCCAM_JWT_JWKS_URI` / `--jwt-jwks-uri` remain deprecated aliases for the metadata setting.

When all session slots are occupied, `/mcp` returns `503 remote_capacity_exceeded` with
`Retry-After: 1`. Disconnecting the WebSocket cancels and disposes its MCP host. `/health` remains an
unauthenticated liveness endpoint and returns no content or credentials.

Both WebSocket transports accept text messages only and reject a fragmented message once its total
size exceeds the configured limit. stdio is unaffected.

Requires a valid TLS certificate file and HTTPS identity metadata. Prefer the password environment
variable over `--tls-password`, which may be visible in a process listing. See
[Configuration — Remote MCP](configuration.md#remote-mcp-tls-jwt).

---

## Batch HTTP server (experimental)

Fire-and-forget transcodes over HTTP (separate from opt-in MCP batch tools):

```bash
OccamMcp.Core --batch-server --port 5051
```

Default port `5051`. Shares job store with `OCCAM_BATCH_MCP=1` MCP tools when configured.

---

## CLI help and offline verify

```bash
OccamMcp.Core --help
```

Offline receipt verification (no MCP transport):

```bash
OccamMcp.Core keys export [--keys-root DIR]
OccamMcp.Core verify --receipt F --pubkey F [--markdown F]
OccamMcp.Core verify --mode citation --receipt F --pubkey F --block-text T --proof F
OccamMcp.Core verify --mode manifest --input F --pubkey F
OccamMcp.Core verify --mode history --input F --pubkey F
OccamMcp.Core version-surface
```

Exit codes: `0` verified · `1` not verified · `2` usage error.

`version-surface` prints `{ hostVersion, assemblyPath, packageVersion }` for the binary on disk.
Compose the full public surface (including `protocolVersion` + `schemaFingerprint` from live
`tools/list`) with:

```bash
node scripts/check-public-mcp-contract.mjs
# same launch path as the ChatGPT tunnel (scripts/launch-mcp-host.mjs)
```

Details: [Receipts](receipts.md).

---

## Choosing a transport

| Need | Use |
|------|-----|
| Cursor, Claude Desktop, most IDEs | stdio |
| MCP 2026 Streamable HTTP on loopback | `--mcp-http` / `--streamable-http` |
| Separate process / LAN client with WS support | WebSocket (`--mcp-server`) |
| Authenticated remote host | Remote (`--remote`) |
| High-volume async URL lists without MCP | Batch HTTP or `OCCAM_BATCH_MCP=1` tools |
