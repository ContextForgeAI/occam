# Session-aware fetch

**Slug:** `session-fetch` · **Product system:** PS-1 Acquisition · **CAPs:** 19 · **Public relevance:** HIGH

**Member CAPs:** CAP-068, CAP-069, CAP-167–CAP-178, CAP-182, CAP-191, CAP-192, CAP-227, CAP-249  
**Product capability:** CAP-167  
**Artifacts (family ledger):** ART-037 · **Ontology:** ART-026 = session profile files; ART-037 = retained import `cookies.txt` (EF-054) — ledger lists ART-037 only  
**Engineering findings:** EF-017, EF-039, EF-040, EF-054

## What it is

Local **session profiles** (headers, cookies, optional Playwright `storageState`) and the plumbing that attaches them to fetch/extract calls — plus operator CLI for import/export. Distinct from recipe cookie inject (`WT_COOKIE_INJECT`) which is playbook-gated.

## Why it exists

Authenticated or personalized pages without Occam storing cloud credentials: operator exports cookies/storageState; agent passes `session_profile` id.

## User-visible entrypoints

| Surface | Session support | Evidence |
|---------|-----------------|----------|
| `session_profile` on transcode/digest/… | Tier 1 full (headers + storageState) | CAP-068/191; session-lifecycle |
| probe / map | Headers; **no** storageState | EF-017; Tier 2 |
| heal / extract_knowledge | Headers forwarded; storageState **dropped** | EF-017; Tier 3 |
| `occam-session.mjs` CLI | init/list/import/export-state | CAP-174–176 |
| Recipe cookies | Separate path | CAP-177 / CAP-213 |

## Core behavior

### Profile files (CAP-067 naming: CAP-167)

- Path: `OCCAM_SESSIONS_ROOT/<id>.json` (default under user data `sessions`).
- Id sanitized (CAP-069 hardening).
- Missing/bad → `session_profile_not_found` / `invalid_session_profile`.

### storageState (CAP-168)

Path resolved with **containment** under sessions root. Windows ordinal-ignore-case vs Unix ordinal prefix check (`SessionProfileHeaders.cs:230-232`).

### Headers merge (CAP-169)

`OCCAM_REQUEST_HEADERS_FILE` JSON merged; session headers take precedence where overlapping.

### Handoff (CAP-170)

`FetchHeadersScope`: ephemeral temp headers file + storageState path to workers; **never log header values** (filename only).

### Cookie → Playwright (CAP-171)

Cookie header parsed into `context.addCookies` on browser path.

### Credential hygiene on redirects (CAP-172–173)

HTTP: strip cross-origin credentials on redirect. Browser: isolate `extraHTTPHeaders` so Authorization does not leak cross-origin; cookies via addCookies path.

### Requires-login interaction (CAP-182)

`RequiresLoginPostProcessor` **skips** automatic `requires_login` conversion when `session_profile` is set (operator claimed auth context).

## Advanced behavior

| CAP | Behavior |
|-----|----------|
| CAP-174 | CLI scaffold `~/.occam/sessions/` + `_imports/`, `states/`, `.gitignore` |
| CAP-175 | Netscape cookies.txt import + risk warnings |
| CAP-176 | Headed `export-state` login capture → storageState |
| CAP-177 | Recipe cookie injection (env + playbook) — not session_profile |
| CAP-178 | Consent dismissal (overlap access-consent; session-adjacent browser UX) |
| CAP-191 | Plumbed across nearly all URL-touching MCP tools |
| CAP-192 | Secret-hygiene defaults (.gitignore sessions) |
| CAP-227/249 | Pool recycle on headers/storageState change; shared context caveat |

## Automatic / silent behavior

| Behavior | Notes |
|----------|-------|
| Per-call GUID headers temp file | Defeats browser pool warm reuse — **EF-039** |
| Pool shared BrowserContext | Cookie bleed until recycle — **EF-002/040** |
| import retains plaintext cookies.txt under `_imports/` | **EF-054** |
| Cache ineligible when session_profile set | `TranscodeCacheEligibility` |

## Parameters

| Name | Default | Effect |
|------|---------|--------|
| `session_profile` | unset | Load profile id; skip RequiresLogin PP; disable response cache eligibility |

## Configuration

| Knob | Default | Effect |
|------|---------|--------|
| `OCCAM_SESSIONS_ROOT` | user-data `sessions` | Profile root |
| `OCCAM_REQUEST_HEADERS_FILE` | unset | Ambient headers for all preflights |
| `WT_COOKIE_INJECT` | off | Recipe cookies |

## Backends

- HTTP: headers/Cookie only.
- Browser: headers + storageState + cookie inject.
- css-extract: no storageState param (Tier 3).

## Sessions / state

Durable on disk as ART-037 JSON + optional storageState files. In-memory: ambient header merge per call; pool holds warm context (CAP-249). Process-scoped — not multi-tenant isolated.

## Network behavior

Authenticated requests use operator-supplied cookies/headers. Cross-origin redirect stripping reduces credential leak. Does not bypass SSRF.

## Artifacts produced

| Artifact | ID / notes |
|----------|------------|
| Session profile JSON | **ART-026** (`ARTIFACT-ONTOLOGY.md`) |
| Playwright `storageState` file | Referenced by ART-026; export-state |
| Retained import `cookies.txt` | **ART-037** — plaintext under `_imports/` (EF-054); not auto-consumed by Core |
| Ephemeral headers temp file | CAP-170 — process-scoped |

**Ledger note:** `canonical-capabilities.json` family `artifacts: [ART-037]` understates ART-026; card follows ontology for identity.

## Trust / provenance properties

Session proves **operator-provided** credentials were attached — not that the site accepted them. Access evidence collection is leak-resistant (CAP-183 in access-consent). Do not log secrets.

## Failure / fallback behavior

| Condition | Code / behavior |
|-----------|-----------------|
| Bad id | `invalid_session_profile` |
| Missing file | `session_profile_not_found` |
| storageState outside root | Rejected by containment |
| No session + login wall | `requires_login` (PP) |
| Session set + still blocked | Typed http/challenge — PP skip does not invent success |

## Platform differences

storageState path containment case-sensitivity (Win vs Unix). Session CLI paths use OS user home.

## Composition with other capabilities

- Used by `acquisition-routing` preflight.
- Enables browser login walls (`access-consent` / RequiresLogin skip).
- Disables `response-cache` eligibility.
- Tier differences documented in `STATE-MODEL` / session-lifecycle subsystem.

## Known limitations

- Name “session-aware” overstates probe/map/heal/extract (headers-only or dropped storageState) — **EF-017**.
- Pool isolation is weak (EF-002/040).
- Import plaintext retention (EF-054).
- No cloud session sync; local files only.

## Engineering findings

| ID | Finding |
|----|---------|
| **EF-017** | storageState dropped on probe/map/heal/extract |
| **EF-039** | GUID temp headers defeat pool reuse |
| **EF-040** | Anon→anon context bleed refinement |
| **EF-054** | cookies.txt retained under `_imports/` |

## Code evidence

- `Session/SessionProfileHeaders.cs:54-238`
- `Session/RequestHeadersMerger.cs`, `FetchHeadersScope`
- `scripts` / `occam-session.mjs` (operator CLI)
- `docs-audit/subsystems/network-fetch-proxy.md` §3
- `docs-audit/subsystems/session-lifecycle.md` (tiers)

## Public-doc relevance

**HIGH.** Document `session_profile`, export-state flow, tier limits, and that Occam does not solve CAPTCHA/login for you.

## Handbook relevance

Operator sessions chapter + agent recipe “login wall → export-state → session_profile → browser”.
