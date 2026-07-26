# PR-C access evidence model

## Contract

Access is a separate decision from transport success, extraction quality, and focus relevance. PR-C keeps
that decision internal while preserving the existing public `likelyLoginRequired` and `requires_login`
surfaces. PR-F owns the later public semantic envelope.

| Disposition | Meaning | Current public effect |
|---|---|---|
| `Open` | Usable public content is present and no direct restriction signal exists | Continue normally |
| `Restricted` | Direct access-control evidence exists | Probe sets `likelyLoginRequired`; transcode returns `requires_login` without a session |
| `Unknown` | Evidence is absent, weak, or ambiguous | No hard login verdict; continue or inspect other outcome dimensions |

## Evidence stages

| Stage | Source | Bounded signals |
|---|---|---|
| Prefetch | HTTP probe | status, authentication challenge, requested/final URL and redirect chain |
| DOM | HTTP/browser worker before pruning | password/identity controls, login form/action/heading, blocking overlay, usable-content threshold, terminology presence |
| Extracted | conservative fallback | exact Markdown labels/headings/actions plus length; never loose prose alone |
| Combined | transcode adapter | worker evidence plus status and redirect confirmation |

## Decision table

| Evidence | Disposition | Stable code |
|---|---|---|
| HTTP 401 | `Restricted` | `http_401` |
| Authentication challenge | `Restricted` | `authentication_challenge` |
| Requested page redirected to login route | `Restricted` | `redirected_to_login` |
| Password control plus identity/action/heading, no usable content | `Restricted` | `blocking_identity_ui` |
| Usable content, including a non-blocking login widget | `Open` | `usable_public_content` |
| Authentication prose only | `Unknown` | `authentication_terminology_only` |
| Password control without blocking context | `Unknown` | `password_field_without_blocking_context` |
| No usable evidence | `Unknown` | `insufficient_access_evidence` |

Multiple strong codes may coexist. Ordering is deterministic. Diagnostic codes contain no source text.

## Bounds and privacy

- The worker scans the already loaded DOM once and returns booleans only.
- Terminology scanning is capped to the first 65,536 characters of tag-stripped HTML.
- No hosted model, extra request, credential value, form value, cookie, or raw evidence excerpt is used.
- Probe uses its existing bounded HTML sample; Markdown fallback uses the already extracted surface.
- A 600-visible-character threshold distinguishes usable content from a blocking shell. It is a tested
  implementation constant, not a claim that content below the threshold is inaccessible.

## Compatibility boundary

No tool was added or removed. No existing response field was renamed. `Unknown` is intentionally not
serialized as a new public field in PR-C; clients see the absence of a hard login verdict. This avoids
silently redefining generic booleans before PR-F documents the additive semantic contract.
