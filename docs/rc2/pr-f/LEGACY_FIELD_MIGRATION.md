# Legacy field migration (PR-F)

## Compatibility window

RC.2 keeps legacy aliases for one documented release-candidate migration window. No public field is
removed in PR-F. Removal requires an announced breaking boundary (expected no earlier than a post-RC.2
stable major/minor cut).

## Alias map

| Legacy field | Additive replacement | Migration guidance |
|---|---|---|
| `recovery[].ok` | `recovery[].transportOk` | Prefer `transportOk`; `ok` remains identical to transport completion |
| (none) | `recovery[].usable` | New; do not infer from `ok` |
| (none) | `recovery[].failureCode` | Per-attempt typed failure when unusable |
| (none) | `recovery[].escalationReason` | Why this attempt was started after a prior unusable attempt |
| (none) | `access` | Shared access assessment; `likelyLoginRequired` / `requires_login` remain |
| `focusMatched` | `focus.status` | Keep `focusMatched` for digest lexical evidence; use `focus` for structural status |
| `compile.truncated` / `omitted` | `completeness` | Truncation telemetry remains; completeness is the answer-unit verdict |
| `found` | `retrieved` | Identical boolean; prefer `retrieved` in new clients |
| (none) | `verdict` | `not_evaluated` on retrieval-only tools; attest/verify for judgment |
| `confidence` | scoped `access.confidence` / future focus confidence | Generic extraction confidence stays extraction-only |

## Client checklist

1. Continue reading legacy fields if you already depend on them.
2. Gate new retry/trust logic on additive dimensions.
3. Do not redefine legacy booleans in place.
4. Expect agent hints to mention completeness/focus/access when those dimensions are non-success.
