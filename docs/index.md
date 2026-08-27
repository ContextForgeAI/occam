---
hide:
  - navigation
  - toc
---

<div class="oc-hero" markdown="1">

<p class="oc-brand-mark" aria-label="Occam"><span class="oc-brand-mark__glyph" aria-hidden="true">⌥</span><span class="oc-brand-mark__rule" aria-hidden="true"></span><span class="oc-brand-mark__letters">O C C A M</span></p>

<p class="oc-wordmark">Local-first web context for AI agents.</p>

</div>

# Stop wasting your agent's context on webpage noise.

<div class="oc-hero oc-hero--rest" markdown="1">

<p class="oc-hero-lead">
Most pages wrap the useful part in navigation, scripts, repeated interface
text, consent controls, and raw markup. Occam removes that presentation layer
before it reaches the model and returns compact, source-linked content your
agent can actually use.
</p>

<p class="oc-hero-audience">
For developers and technical users building or running AI agents — especially
local and self-hosted setups. Search finds pages and browser automation operates
interfaces; Occam turns a known URL into compact context or an explicit failure.
</p>

<p class="oc-hero-actions">
<a class="oc-btn oc-btn--primary" href="quick-start/">Get your first result</a>
<a class="oc-btn oc-btn--secondary" href="#measured-before-and-after">See the transformation</a>
</p>

<div class="oc-hero-visual" aria-label="Measured comparison: controlled webpage input becomes compact agent context">
  <div class="oc-hero-visual__panel oc-hero-visual__panel--noise">
    <span class="oc-hero-visual__label">Controlled webpage</span>
    <span class="oc-hero-visual__concept">Measured fixture · 5,297 UTF-8 bytes</span>
    <ul class="oc-hero-visual__noise" aria-hidden="true">
      <li>navigation · search · scripts</li>
      <li>related links · newsletter form</li>
      <li>cookie notice · footer · layout CSS</li>
    </ul>
  </div>
  <span class="oc-hero-visual__arrow" aria-hidden="true">→</span>
  <div class="oc-hero-visual__panel oc-hero-visual__panel--clean">
    <span class="oc-hero-visual__label">Compact agent context</span>
    <p class="oc-hero-visual__clean">Article structure and useful text preserved — 1,736 UTF-8 bytes, with the source URL attached.</p>
  </div>
</div>

</div>

## Measured before and after

One controlled representative fixture. Bytes, not tokens. Not a universal
benchmark.

<div class="oc-compare oc-compare--measured" markdown="0">
  <div class="oc-compare__panel oc-compare__panel--before">
    <div class="oc-compare__eyebrow">Measured input</div>
    <div class="oc-compare__title">Article plus webpage chrome</div>
    <p class="oc-compare__url"><a href="examples/current-proof/representative-input.html">Open the controlled input</a></p>
    <pre class="oc-compare__md oc-compare__md--input"><code>&lt;header&gt;…search…&lt;/header&gt;
&lt;nav&gt;…repeated links…&lt;/nav&gt;
&lt;main&gt;
  &lt;article&gt;Web context without the chrome…&lt;/article&gt;
  &lt;aside&gt;…newsletter…related reading…&lt;/aside&gt;
&lt;/main&gt;
&lt;section&gt;…cookie notice…&lt;/section&gt;
&lt;footer&gt;…company links…&lt;/footer&gt;</code></pre>
    <p class="oc-compare__measure">5,297 UTF-8 HTML bytes</p>
  </div>
  <div class="oc-compare__panel oc-compare__panel--after">
    <div class="oc-compare__eyebrow">Measured output</div>
    <div class="oc-compare__title">Compact Markdown</div>
    <pre class="oc-compare__md"><code># Web context without the chrome

AI agents rarely need the whole interface of a webpage.
They need the useful text, its structure, and enough
source information to explain where the material came from.</code></pre>
    <ul class="oc-compare__meta">
      <li><span>State</span> ok: true</li>
      <li><span>Preserved</span> headings, paragraphs, list, code</li>
      <li><span>Source</span> attached in the result</li>
    </ul>
    <p class="oc-compare__measure">1,736 UTF-8 Markdown bytes</p>
  </div>
</div>

<div class="oc-measure-note" markdown="1">

For this controlled page, Occam returned **67.2% fewer UTF-8 bytes** than the
HTML body (5,297 → 1,736). No tokenizer was used — not a token claim, not an
answer-quality claim, and not a universal reduction rate.

<details class="oc-proof-disclosure">
<summary>Method and source revision</summary>

Runtime source revision
`669e8dcae1afa7ddcf1ca5cf9388c4615e3a3603`.
Full method, complete input/output, the minimal live smoke proof, controlled
failure, and reproduction scripts:
[current proof bundle](examples/current-proof/README.md).

</details>

</div>

## The cost grows with every source

An ordinary page can contain navigation, scripts, repeated interface text,
cookie controls, footers, and large amounts of raw markup around the part an
agent actually needs.

Passing all of that through wastes a local model's limited context and makes
larger models work around irrelevant text. A search result can help find the
source; Occam focuses on reading the chosen source and preparing usable page
content for the agent.

The measured fixture above makes that transformation inspectable. The proof
bundle also retains `example.com` as the minimal live smoke case so the richer
marketing example does not replace the smallest deterministic contract check.

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

[Trust and security](trust-and-safety.md) ·
[Capabilities](capabilities/index.md) ·
[Reference overview](documentation-map.md) ·
[Installation safety](trust/installation-safety.md) ·
[Receipts](receipts.md)

## Get your first Occam result

Install Occam, choose the path for your AI application or local runtime, and
finish with a real page read.

<p class="oc-hero-actions">
<a class="oc-btn oc-btn--primary" href="quick-start/">Get your first result</a>
<a class="oc-btn oc-btn--secondary" href="examples/current-proof/">Inspect the proof</a>
</p>

<p class="oc-meta-line"><strong>Version:</strong> 1.0.0-rc.4 (published install channel) · <strong>Status:</strong> release candidate · <strong>License:</strong> AGPL-3.0-or-later</p>

## Explore deeper

[Task router](choosing-a-tool.md) · [Examples](examples/index.md) ·
[Recipes](recipes.md) · [Tools](tools/index.md) ·
[Tools reference](tools-reference.md) · [MCP API](reference/mcp-api.md) ·
[Handbook](handbook/index.md) · [Experimental](experimental.md) ·
[Operators](operators.md) · [Troubleshooting](troubleshooting.md) ·
[FAQ](faq.md) · [Reference overview](documentation-map.md) ·
[Ask AI](ask-ai.md) · [`llms.txt`](https://raw.githubusercontent.com/ContextForgeAI/occam/main/llms.txt)

<div class="oc-sitemap-quiet" markdown="1">

## All top-level pages

Compact index (gate + scanning). Narrative map: [Reference overview](documentation-map.md).

[acquisition](acquisition.md) · [ask-ai](ask-ai.md) · [choosing-a-tool](choosing-a-tool.md) · [concepts](concepts.md) · [configuration](configuration.md) · [datasets](datasets.md) · [documentation-map](documentation-map.md) · [experimental](experimental.md) · [failure-codes](failure-codes.md) · [faq](faq.md) · [friend-test](friend-test.md) · [getting-started](getting-started.md) · [how-occam-works](how-occam-works.md) · [install](install.md) · [materialization](materialization.md) · [mcp-hosts](mcp-hosts.md) · [networking](networking.md) · [operators](operators.md) · [playbooks](playbooks.md) · [quality-baseline](quality-baseline.md) · [quick-start](quick-start.md) · [receipt_verification](receipt_verification.md) · [receipts](receipts.md) · [recipes](recipes.md) · [roadmap](roadmap.md) · [sessions](sessions.md) · [tools-reference](tools-reference.md) · [transports](transports.md) · [troubleshooting](troubleshooting.md) · [trust-and-safety](trust-and-safety.md) · [what-is-occam](what-is-occam.md)

</div>
