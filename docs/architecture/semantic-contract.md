# Semantic contract notes (from RC.2)

Durable architecture decisions extracted from the RC.2 engineering set. The
RC.2 working diaries and PR-by-PR reports are **not** part of the public
documentation set.

**Status:** accepted for the `1.0.0-rc.2` line. Where this page and the code
disagree, the code wins. Normative API shapes remain in [MCP_API_SPEC.md](../../MCP_API_SPEC.md).

---

## Invariants

| ID | Invariant |
|----|-----------|
| INV-1 | Probe and transcode must not make access decisions through independent classifiers. |
| INV-2 | Authentication terminology alone must never imply login-required. |
| INV-3 | Digest transport compatibility terminates at one normalization boundary. |
| INV-4 | Focus selection operates on structured sections and deterministic identities, not only flat Markdown text. |
| INV-5 | Fragment and anchor identity outrank fuzzy textual relevance when exact. |
| INV-6 | TOC entries must not outrank their corresponding body section merely because they occur earlier. |
| INV-7 | Budget accounting applies to fields that are actually serialized. |
| INV-8 | The planner preserves a minimum answer-bearing unit when it fits. |
| INV-9 | Transport success, access, usability, focus, completeness, and verdict are separate semantic dimensions. |
| INV-10 | Lifecycle operations are scoped to an explicit process/host identity. |

---

## Access classification (ADR-0005)

One pure `AccessClassifier` owns the decision for both probe and transcode.

- Output: `Open`, `Restricted`, or `Unknown`, with scoped confidence and evidence codes.
- `Restricted` requires a **direct** signal: HTTP 401, authentication challenge header,
  redirect to a dedicated login route, or blocking identity UI (password control plus
  identity/action context) without usable content.
- Authentication terminology, a password control without blocking context, a login-like
  path, or insufficient evidence produce `Unknown` — not an automatic `requires_login`.

Workers may collect bounded boolean DOM signals. They must not return control values,
labels, page text, credentials, or cookies as access evidence.

---

## Structure-aware focus (ADR-0006)

Core builds a compact `SectionIndex` over the Markdown surface. Ranking priority:

1. exact decoded URL fragment;
2. exact anchor / normalized identity;
3. exact heading and heading-term coverage;
4. nearby body phrase/term evidence;
5. answer-bearing body evidence;
6. deterministic document ordinal.

Index/TOC-like entries are strongly penalized. Exact fragments are stripped from the
network request and retained as local intent. No embeddings or host allowlists.

---

## Projection-first budget (ADR-0007)

Whole-response allocation consumes an explicitly marked **public projection**, never
the raw extraction inventory. Unrequested structured fields receive zero allocation.
`max_tokens` is never silently enlarged. The planner protects a minimum answer unit
(selected heading, bounded explanatory body, tightly coupled list/table/code evidence)
when it fits.

---

## Compatibility and validation

- **RC.1 regression corpus:** `corpora/rc1-regression.jsonl` (via `benchmarks/l0-gate`).
- **RC.2 regression harness:** `benchmarks/rc2-regression/` — characterization freezes
  historical surfaces; `--regression` / `--pr-*` assert current production behavior.
- Fixtures under those trees are synthetic or frozen captures. See
  [FIXTURE_SOURCES.md](../maintenance/FIXTURE_SOURCES.md).

### Known current limitations

- Hard anti-bot / CAPTCHA walls remain honest failures (`http_403`,
  `captcha_or_challenge`) — not silently “solved”.
- Live L3 heal pilots can flake on third-party SPA drift; re-run before treating as a
  regression.
- npm / NuGet / VSIX distribution is **not** part of the `1.0.0-rc.2` archive RC
  (GitHub release tarballs only). See [roadmap.md](../roadmap.md).
