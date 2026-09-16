# Search

Hub for open-web discovery. Occam does **not** index the web; providers return
result URLs for later probe / transcode / digest.

Deep config: [configuration.md](configuration.md) (search section).
Guide: [guides/search-and-discover.md](guides/search-and-discover.md).
Tool: [tools/occam_search.md](tools/occam_search.md).
Inventory: [CAPABILITIES](CAPABILITIES.md).

## Default path

Unset `OCCAM_SEARCH_PROVIDER` → keyless **DuckDuckGo** HTML SERP
(`Search/DuckDuckGoSearchProvider.cs:12`). On soft failure, falls back to the
lite endpoint (`:30–40`). Outcome discloses `provider: duckduckgo`.

MCP tool: `occam_search` (`Tools/OccamSearchTool.cs:17–22`) — query →
`{id, handle, title, url, snippet}`. Handles `S1` / `H…` bind into later tools
via `SourceHandleStore`.

## Providers

Interface: `Search/ISearchProvider.cs:8`.
DI registration: `Composition/OccamServiceCollectionExtensions.cs:85–90`.

| Name | API key? | Base URL | Notes |
|------|----------|----------|-------|
| `duckduckgo` | no | built-in HTML/lite | Default |
| `searxng` | no | **required** `OCCAM_SEARCH_URL` | Self-hosted |
| `brave` | yes (`OCCAM_SEARCH_API_KEY`) | default `api.search.brave.com` | |
| `tavily` | yes (`OCCAM_SEARCH_API_KEY`) | default `api.tavily.com` | |
| `donsetch` | no | local binary | **Experimental** — not bundled (AGPL); `OCCAM_DONSETCH_PATH` or `PATH` (`DonsetchSearchProvider.cs:10–28`) |

`OCCAM_SEARCH_PROVIDER=off|none` → configured-off failure
(`SearchService.cs` resolve path).

## Fan-out + health

When `OCCAM_SEARCH_PROVIDERS` is set (CSV), it **wins** over the singular
provider (`SearchService.cs:228–238`). Healthy configured backends are polled;
results merge by URL consensus; response `provider` is `fanout` with
`providersUsed[]`.

Health / degrade / rate limits: `Search/SearchProviderHealth.cs`
(env: `OCCAM_SEARCH_DEGRADE_MINUTES`, `OCCAM_SEARCH_RATE_MAX`,
`OCCAM_SEARCH_RATE_WINDOW_S`, timeouts — see [configuration.md](configuration.md)).

```
OCCAM_SEARCH_PROVIDER  XOR  OCCAM_SEARCH_PROVIDERS (CSV wins)
  → skip providers missing required key/URL
  → poll healthy arms
  → merge / dedup by URL
  → degrade on 429 / CAPTCHA-like 202 / timeout
```

## Honesty notes

- Occam search is **discovery**, not a crawl index.
- **Donsetch** is an optional operator-installed CLI — do not imply it ships in
  the release tarball.
- PDF OCR and translate are separate advanced env knobs
  (`OCCAM_PDF_OCR*`, `OCCAM_TRANSLATE_*`) — not search providers.

## Related

- [networking.md](networking.md) — egress / proxy affecting SERP fetches
- [experimental.md](experimental.md) — other opt-in MCP surfaces (not search)
