# Workflow gallery

Three recorded jobs. Each one is a prompt you can type, a captured result you
can inspect, and a named host build — not a global quality score.

Start with the [connection test](../quick-start.md#choose-how-you-use-ai)
(`example.com`). Then run **Understand an instruction**. Host-specific
where-to-type notes: [Host onboarding](../onboarding/index.md).

| Job | What you ask | What the capture shows | Named build |
|-----|--------------|------------------------|-------------|
| [Understand an instruction](golden-workflows/understand-instruction/) | MDN Functions: scope, closures, syntax, conditions, citations, omissions | `ok:true`, `focus:hit`, 18 named omitted sections | `ff-occam/1.0.0-rc.2` workspace MCP, recaptured 2026-09-07 — [ledger](release-evidence.md) |
| [Compare known sources](golden-workflows/compare-sources/) | Two official nginx pages; do not invent `proxy_read_timeout` | Both URLs ok; focus weak; host said so | same named host, not Release `v1.0.0` |
| [Inspect changes](golden-workflows/inspect-changes/) | Same MDN URL + stored `contentHash` as `if_none_match` | `unchanged:true`, empty body | same named host, not Release `v1.0.0` |

The same three jobs as folders (`manifest.json`, excerpts, omissions, wrapper
budget): [Context packs](context-packs/). Operator CLIs for a docs delta and
a claim inspect: [Docs change brief](docs-change-brief/) ·
[Citation inspector](citation-inspector/). Bounded site research:
[Site research](site-research/).

The published GitHub Release identity (`ff-occam/1.0.0`) is recorded on the
[current proof](current-proof/) smoke and controlled refusal — see the
[release evidence ledger](release-evidence.md). Do not treat a workspace
capture as byte-identical to every installed Release binary.

## First useful prompt

After `example.com` works, paste this in a **new conversation**:

```text
Use Occam to read https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide/Functions
Show how function scope and closures work. Include the syntax I need, any
conditions, and source links. Tell me if relevant content was omitted.
```

Settings used in the capture: `fit_markdown:true`,
`focus_query:"function scope closures"`, `max_tokens:800`,
`backend_policy:http`. Reproduce from
[settings.json](golden-workflows/understand-instruction/settings.json)
or:

```bash
occam read https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide/Functions --focus "function scope closures" --fit --max-tokens 800
```

If the result is incomplete, raise the budget or follow `suggestedMinTokens`.
Do not invent omitted steps.

## Feedback

If a recorded job missed a command, condition, or citation you needed, send
[this template](feedback.md) — task, expected content, redacted diagnostics.
Do not send cookies or full HTML.
