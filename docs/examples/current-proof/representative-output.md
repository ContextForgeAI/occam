# Web context without the chrome

Agent infrastructure · 8 minute read

AI agents rarely need the whole interface of a webpage. They need the useful text, its structure, and enough source information to explain where the material came from.

## The hidden cost of a page read

A typical page wraps an article in navigation, consent controls, repeated links, related-content cards, scripts, and layout markup. When an agent receives that representation unchanged, limited context is spent on interface text before the actual task begins.

The problem compounds during research. Reading several sources can repeat the same headers and footers many times, leaving less room for the evidence that the model must compare.

## Shape the source before the model sees it

A web-context layer should preserve headings, paragraphs, lists, code, and the source URL while removing presentation-only material. It should also return an explicit failure when a page cannot be read instead of asking the model to reconstruct missing content from memory.

```
web page
  -> useful page structure
  -> compact agent context
  -> source-linked result
```

## What to measure

Measure the input and output using a declared method. A byte comparison can show representation size for one fixture, but it is not automatically a token-savings, answer-quality, or universal coverage claim. Record the source revision and keep the complete output available for inspection.

## Operational boundary

Public demos need stricter limits than local tools. They must reject private destinations, cap response sizes, bound execution time, and expose typed failures for rate limits or access walls. Cookies and authenticated sessions do not belong in an anonymous demo.