# PR-D SectionIndex design

## Representation

| Field | Purpose |
|---|---|
| `ordinal` | Stable document-order identity and final tie-break |
| `level`, `parentOrdinal` | Heading hierarchy |
| `heading`, `normalizedHeading` | Display label and deterministic comparison form |
| `anchorIds[]` | Preserved explicit anchor or synthetic slug with duplicate suffix |
| `start`, `length` | Source-surface section boundary |
| `text`, `body` | Selected section and nearby evidence region |
| `linkDensity`, `isIndexLike` | TOC/index discrimination |

Explicit `{#anchor}` identities are preserved. Without one, Core synthesizes a lowercase normalized slug.
Duplicate synthetic anchors receive `-2`, `-3`, and so on in document order. Index construction never
mutates the returned Markdown.

## Query and fragment handling

Technical tokenization retains Unicode letter/number runs and internal `.`, `_`, and `-`, so `401`,
`15.5.2`, `client_max_body_size`, and `Sec-Fetch-Mode` remain discriminating input. Fragment percent
decoding is bounded and malformed escapes remain safe literal input.

`FocusIntent` separates `FetchUrl` from `Fragment`. Backends, robots checks, privacy checks, and access
classification receive the fragment-free URL. The fragment travels only through internal options into the
materialization request and `TokenBudget` structural selector.

## Ranking and diagnostics

`SectionRanker.Select` returns `Hit`, `Weak`, or `Miss`, scoped confidence, selected section/anchor,
fragment-resolution state, and a score trace for every candidate. Reason codes include
`exact_fragment`, `exact_anchor`, `exact_heading`, `heading_coverage`, `nearby_body_terms`,
`nearby_phrase`, `answer_body`, and `index_penalty`.

Exact identities have score bands above fuzzy evidence. An index penalty is larger than ordinary textual
matches, preventing repetition in a TOC from winning. Equal final scores sort by ordinal. A global
definitional paragraph cannot be reattached from a losing section: anchor recovery is scoped to the
selected section.

## Bounds

The index stores section text/body slices as strings in the current implementation. A recorded
56,576-character, 1,600-section synthetic run completed in 2.919 ms and allocated 3,196,312 bytes on the
local validation machine. This is an observation, not a universal guarantee. A future optimization may
store spans only, provided identities, traces, and output bytes remain compatible.

## Deferred to later stages

- Minimum answer-unit protection and serialized projection accounting: PR-E.
- Additive public focus/completeness fields and confidence: PR-F.
- Public semantics for unresolved fragments beyond the internal miss: evidence-limited and not invented.
- DOM-native anchor propagation beyond explicit Markdown anchors: compatible future refinement.
