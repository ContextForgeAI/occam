---
hide:
  - navigation
  - toc
---

<div class="oc-hero" markdown="1">

<p class="oc-brand-mark" aria-label="Occam"><span class="oc-brand-mark__glyph" aria-hidden="true">⌥</span><span class="oc-brand-mark__rule" aria-hidden="true"></span><span class="oc-brand-mark__letters">O C C A M</span></p>

<p class="oc-wordmark">Local-first web context for AI agents.</p>

</div>

# Give your AI the page — not the webpage noise.

<div class="oc-hero oc-hero--rest" markdown="1">

<p class="oc-hero-lead">
Occam reads live web pages, removes the noise, and returns compact,
source-linked content your AI agent can actually use.
</p>

<p class="oc-hero-audience">
For developers and technical users building or running AI agents — especially
local and self-hosted setups.
</p>

<p class="oc-hero-actions">
<a class="oc-btn oc-btn--primary" href="quick-start/">Get your first result</a>
<a class="oc-btn oc-btn--secondary" href="#real-output">See real output</a>
</p>

<div class="oc-hero-visual" aria-label="Conceptual comparison: typical webpage input becomes compact agent context">
  <div class="oc-hero-visual__panel oc-hero-visual__panel--noise">
    <span class="oc-hero-visual__label">Typical webpage input</span>
    <span class="oc-hero-visual__concept">Conceptual · not a measured page</span>
    <ul class="oc-hero-visual__noise" aria-hidden="true">
      <li>nav · scripts · chrome</li>
      <li>cookie banners · footers</li>
      <li>repeated UI · markup</li>
    </ul>
  </div>
  <span class="oc-hero-visual__arrow" aria-hidden="true">→</span>
  <div class="oc-hero-visual__panel oc-hero-visual__panel--clean">
    <span class="oc-hero-visual__label">Compact agent context</span>
    <p class="oc-hero-visual__clean">Useful page content your agent can use — with the source URL attached.</p>
  </div>
</div>

</div>

## Real output

One deterministic smoke fixture. Bytes, not tokens. Not a universal benchmark.

<div class="oc-compare oc-compare--measured" markdown="0">
  <div class="oc-compare__panel oc-compare__panel--before">
    <div class="oc-compare__eyebrow">Measured input</div>
    <div class="oc-compare__title">Live HTML body</div>
    <p class="oc-compare__url"><a href="https://example.com/">https://example.com/</a></p>
    <pre class="oc-compare__md oc-compare__md--input"><code>&lt;!doctype html&gt;
&lt;html&gt;
&lt;head&gt;&lt;title&gt;Example Domain&lt;/title&gt;…
&lt;body&gt;
  &lt;h1&gt;Example Domain&lt;/h1&gt;
  &lt;p&gt;This domain is for use in documentation
  examples…&lt;/p&gt;
&lt;/body&gt;
&lt;/html&gt;</code></pre>
    <p class="oc-compare__measure">559 UTF-8 HTML bytes <span class="oc-compare__measure-note">after HTTP decoding</span></p>
  </div>
  <div class="oc-compare__panel oc-compare__panel--after">
    <div class="oc-compare__eyebrow">Measured output</div>
    <div class="oc-compare__title">Compact Markdown</div>
    <pre class="oc-compare__md"><code># Example Domain

This domain is for use in documentation examples without needing permission.
Avoid use in operations.</code></pre>
    <ul class="oc-compare__meta">
      <li><span>State</span> ok: true</li>
      <li><span>Requested / final</span> https://example.com/</li>
      <li><span>Source</span> attached in the result</li>
    </ul>
    <p class="oc-compare__measure">167 UTF-8 Markdown bytes</p>
  </div>
</div>

<div class="oc-measure-note" markdown="1">

For this one page, Occam returned **70.1% fewer UTF-8 bytes** than the decoded
HTML body (559 → 167). No tokenizer was used — not a token claim, and not a
universal reduction rate.

<details class="oc-proof-disclosure">
<summary>Method and source revision</summary>

Source revision
`3d871d34f52180f8e0046f505de577b6aa3417e4`.
Full method, complete output, and reproduction scripts:
[current proof fixture](examples/current-proof/README.md).

</details>

</div>

## Webpages were designed for people, not context windows

An ordinary page can contain navigation, scripts, repeated interface text,
cookie controls, footers, and large amounts of raw markup around the part an
agent actually needs.

Passing all of that through wastes a local model's limited context and makes
larger models work around irrelevant text. A search result can help find the
source; Occam focuses on reading the chosen source and preparing usable page
content for the agent.

A richer before/after fixture with realistic page chrome is planned as a
follow-up; until then, the conceptual comparison above explains the job, and
`example.com` remains the deterministic smoke proof.

## A useful result — or an explicit unknown

<div class="oc-result-pair" markdown="0">
  <div class="oc-result oc-result--success">
    <div class="oc-result__badge">Successful read</div>
    <div class="oc-result__state">ok: true</div>
    <p class="oc-result__lead">Readable content with the source URL and result metadata.</p>
    <pre class="oc-result__sample"><code># Example Domain

This domain is for use in documentation examples…</code></pre>
    <p class="oc-result__meta">Source: https://example.com/</p>
  </div>
  <div class="oc-result oc-result--unknown">
    <div class="oc-result__badge">Explicit unknown</div>
    <div class="oc-result__state">ok: false</div>
    <p class="oc-result__lead">Page content is unknown — do not invent it from memory.</p>
    <pre class="oc-result__sample"><code>{
  "ok": false,
  "failure": {
    "code": "private_url_blocked",
    "message": "Private or local URLs are blocked."
  }
}</code></pre>
    <p class="oc-result__meta">Typed reason · no invented page text</p>
  </div>
</div>

This predictable failure is a trust feature, not the main reason to try Occam:
start with the successful read. The proof fixture includes the controlled
private-destination case above.

## Choose your workflow

Three routes. Pick one, then open the matching Quick Start path for the exact
prompt.

<div class="oc-workflows" markdown="0">
  <a class="oc-workflow" href="quick-start/#path-b-cursor">
    <div class="oc-workflow__head">
      <span class="oc-workflow__name">Use Occam in an AI application</span>
      <span class="oc-status oc-status--friendly oc-status--stable">Validated paths</span>
    </div>
    <p class="oc-workflow__fit">Connect Cursor, Hermes, or another MCP-capable app, then type the first prompt in a new conversation.</p>
    <span class="oc-workflow__cta">Open AI app paths →</span>
  </a>
  <a class="oc-workflow oc-workflow--experimental" href="quick-start/#path-d-local-ollama-model-experimental">
    <div class="oc-workflow__head">
      <span class="oc-workflow__name">Use a local Ollama model</span>
      <span class="oc-status oc-status--friendly oc-status--experimental">Experimental</span>
    </div>
    <p class="oc-workflow__fit">Tool-capable local model via Occam’s terminal chat — Ollama is a model runtime, not an MCP host.</p>
    <span class="oc-workflow__cta">Start local Ollama chat →</span>
  </a>
  <a class="oc-workflow" href="quick-start/#path-e-no-supported-application-detected">
    <div class="oc-workflow__head">
      <span class="oc-workflow__name">Integrate Occam into an agent</span>
      <span class="oc-status oc-status--friendly oc-status--advanced">Developer</span>
    </div>
    <p class="oc-workflow__fit">Wire your own MCP client with the generated launch snippet and API contract.</p>
    <span class="oc-workflow__cta">Developer integration →</span>
  </a>
</div>

[Choose a detailed path and get the exact first prompt](quick-start.md) ·
[See host validation tiers](mcp-hosts.md)

## How Occam works

<nav class="oc-flow" aria-label="How Occam works">
  <div class="oc-flow__step">
    <span class="oc-flow__title">Live webpage</span>
    <span class="oc-flow__hint">acquisition</span>
  </div>
  <span class="oc-flow__sep" aria-hidden="true">→</span>
  <div class="oc-flow__step oc-flow__step--core">
    <span class="oc-flow__title">Occam</span>
    <span class="oc-flow__hint">cleanup</span>
  </div>
  <span class="oc-flow__sep" aria-hidden="true">→</span>
  <div class="oc-flow__step">
    <span class="oc-flow__title">Compact context</span>
    <span class="oc-flow__hint">source-linked</span>
  </div>
  <span class="oc-flow__sep" aria-hidden="true">→</span>
  <div class="oc-flow__step">
    <span class="oc-flow__title">AI agent</span>
    <span class="oc-flow__hint">usable input</span>
  </div>
</nav>

Occam starts with a lightweight local read and can use a local browser when the
page requires it. It then returns the useful content and metadata through one
agent-facing contract. Focus, budgets, structured extraction, sessions, and
verification are available when the task needs more control.

[How Occam works](how-occam-works.md) · [Read one page](guides/read-a-page.md) ·
[Choose a tool](choosing-a-tool.md)

## Why Occam

<div class="oc-pillars" markdown="1">

<div class="oc-pillar" markdown="1">
<div class="oc-pillar-label">Cleaner context</div>
<p>Useful page content instead of navigation, scripts, repeated UI, and irrelevant boilerplate.</p>
</div>

<div class="oc-pillar" markdown="1">
<div class="oc-pillar-label">Predictable behavior</div>
<p>A real result when Occam can read the source, and an explicit failure when it cannot.</p>
</div>

<div class="oc-pillar" markdown="1">
<div class="oc-pillar-label">One reusable layer</div>
<p>A consistent web-context layer across currently supported AI tools.</p>
</div>

</div>

## Trust and local control

<div class="oc-trust" markdown="0">
  <div class="oc-trust__item">
    <span class="oc-trust__label">Local-first defaults</span>
    <span class="oc-trust__text">Normal page reading runs on your machine by default.</span>
  </div>
  <div class="oc-trust__item">
    <span class="oc-trust__label">Source traceability</span>
    <span class="oc-trust__text">Source URLs remain attached to results.</span>
  </div>
  <div class="oc-trust__item">
    <span class="oc-trust__label">Explicit unknown</span>
    <span class="oc-trust__text">Unsuccessful reads return a typed result — not invented page text.</span>
  </div>
  <div class="oc-trust__item">
    <span class="oc-trust__label">Private destinations blocked</span>
    <span class="oc-trust__text">Unsafe private/local destinations are denied unless explicitly allowed.</span>
  </div>
  <div class="oc-trust__item">
    <span class="oc-trust__label">Consent before config</span>
    <span class="oc-trust__text">Connection changes require an explicit install/connect action.</span>
  </div>
  <div class="oc-trust__item">
    <span class="oc-trust__label">Local Ollama path</span>
    <span class="oc-trust__text">No Ollama login required · Occam does not use Ollama Web Search.</span>
  </div>
</div>

Local-first is not an absolute “never cloud” claim. The origin website is a
network source, and explicitly configured search, managed acquisition, proxy,
or remote transport providers can change the boundary.

Optional Receipt v1 artifacts can check returned-byte integrity against a
supplied key. They do not prove truth, identity, or authentic origin.

[Trust & Safety](trust-and-safety.md) ·
[Installation safety](trust/installation-safety.md) ·
[Receipts](receipts.md)

## Get your first Occam result

Install Occam, choose the path for your AI application or local runtime, and
finish with a real page read.

<p class="oc-hero-actions">
<a class="oc-btn oc-btn--primary" href="quick-start/">Get your first result</a>
<a class="oc-btn oc-btn--secondary" href="examples/current-proof/">Inspect the proof</a>
</p>

<p class="oc-meta-line"><strong>Version:</strong> 1.0.0-rc.2 · <strong>Status:</strong> release candidate · <strong>License:</strong> AGPL-3.0-or-later</p>

## Explore deeper

[Task router](choosing-a-tool.md) · [Examples](examples/index.md) ·
[Recipes](recipes.md) · [Tools](tools/index.md) ·
[Tools reference](tools-reference.md) · [MCP API](reference/mcp-api.md) ·
[Handbook](handbook/index.md) · [Experimental](experimental.md) ·
[Operators](operators.md) · [Troubleshooting](troubleshooting.md) ·
[FAQ](faq.md) · [Full documentation map](documentation-map.md) ·
[Ask AI](ask-ai.md) · [`llms.txt`](https://raw.githubusercontent.com/ContextForgeAI/occam/main/llms.txt)

<div class="oc-sitemap-quiet" markdown="1">

## All top-level pages

Compact index (gate + scanning). Narrative map: [Documentation map](documentation-map.md).

[acquisition](acquisition.md) · [ask-ai](ask-ai.md) · [choosing-a-tool](choosing-a-tool.md) · [concepts](concepts.md) · [configuration](configuration.md) · [datasets](datasets.md) · [documentation-map](documentation-map.md) · [experimental](experimental.md) · [failure-codes](failure-codes.md) · [faq](faq.md) · [friend-test](friend-test.md) · [getting-started](getting-started.md) · [how-occam-works](how-occam-works.md) · [install](install.md) · [materialization](materialization.md) · [mcp-hosts](mcp-hosts.md) · [networking](networking.md) · [operators](operators.md) · [playbooks](playbooks.md) · [quality-baseline](quality-baseline.md) · [quick-start](quick-start.md) · [receipt_verification](receipt_verification.md) · [receipts](receipts.md) · [recipes](recipes.md) · [roadmap](roadmap.md) · [sessions](sessions.md) · [tools-reference](tools-reference.md) · [transports](transports.md) · [troubleshooting](troubleshooting.md) · [trust-and-safety](trust-and-safety.md) · [what-is-occam](what-is-occam.md)

</div>
