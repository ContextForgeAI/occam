<div class="oc-hero" markdown="1">

<p class="oc-wordmark">Local-first web context for AI agents.</p>

</div>

# Give your AI the page — not the webpage noise.

<div class="oc-hero oc-hero--rest" markdown="1">

<p class="oc-hero-lead">
Occam reads live web pages, removes the noise, and returns compact,
source-linked content your AI agent can actually use.
</p>

<p class="oc-hero-actions">
<a class="oc-btn oc-btn--primary" href="quick-start/">Get your first result</a>
<a class="oc-btn oc-btn--secondary" href="#real-output">See real output</a>
</p>

<div class="oc-hero-visual" aria-label="Webpage noise becomes compact source-linked content">
  <div class="oc-hero-visual__panel oc-hero-visual__panel--noise">
    <span class="oc-hero-visual__label">Raw webpage noise</span>
    <ul class="oc-hero-visual__noise" aria-hidden="true">
      <li>nav · scripts · chrome</li>
      <li>cookie banners · footers</li>
      <li>repeated UI · markup</li>
    </ul>
  </div>
  <span class="oc-hero-visual__arrow" aria-hidden="true">→</span>
  <div class="oc-hero-visual__panel oc-hero-visual__panel--clean">
    <span class="oc-hero-visual__label">Compact, source-linked</span>
    <p class="oc-hero-visual__clean">Useful page content your agent can use — with the source URL attached.</p>
  </div>
</div>

</div>

For developers and technical users building or running AI agents, especially in
local and self-hosted environments.

## Real output

<div class="oc-compare" markdown="0">
  <div class="oc-compare__panel oc-compare__panel--before">
    <div class="oc-compare__eyebrow">Webpage input</div>
    <div class="oc-compare__title">Noise around the page</div>
    <p class="oc-compare__url"><a href="https://example.com/">https://example.com/</a></p>
    <ul class="oc-compare__noise">
      <li>Navigation chrome</li>
      <li>Scripts and markup</li>
      <li>Repeated UI text</li>
      <li>Boilerplate around the body</li>
    </ul>
    <p class="oc-compare__measure">559 UTF-8 HTML bytes <span class="oc-compare__measure-note">after HTTP decoding</span></p>
  </div>
  <div class="oc-compare__panel oc-compare__panel--after">
    <div class="oc-compare__eyebrow">Occam output</div>
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

In this reproducible example, the HTML response body was **559 UTF-8 bytes**
after HTTP decoding. Occam returned **167 UTF-8 bytes** of Markdown — **70.1%
fewer bytes for this page**. No tokenizer was used, so this is not a token
claim. Source SHA `3d871d34f52180f8e0046f505de577b6aa3417e4`.

[Method, complete output, and reproduction scripts](examples/current-proof/README.md)

</div>

## Webpages were designed for people, not context windows

An ordinary page can contain navigation, scripts, repeated interface text,
cookie controls, footers, and large amounts of raw markup around the part an
agent actually needs.

Passing all of that through wastes a local model's limited context and makes
larger models work around irrelevant text. A search result can help find the
source; Occam focuses on reading the chosen source and preparing usable page
content for the agent.

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

<div class="oc-workflows" markdown="0">
  <a class="oc-workflow" href="quick-start/#path-b-cursor">
    <div class="oc-workflow__head">
      <span class="oc-workflow__name">Cursor</span>
      <span class="oc-status oc-status--stable">Live-validated</span>
    </div>
    <p class="oc-workflow__fit">You use Cursor as your AI coding app.</p>
    <dl class="oc-workflow__facts">
      <div><dt>Command</dt><dd><code>occam connect --only cursor</code></dd></div>
      <div><dt>Where to type</dt><dd>A new Cursor chat after restart/reload</dd></div>
      <div><dt>Next</dt><dd>Exact first prompt in Quick Start</dd></div>
    </dl>
  </a>
  <a class="oc-workflow" href="quick-start/#path-c-hermes-agent">
    <div class="oc-workflow__head">
      <span class="oc-workflow__name">Hermes Agent</span>
      <span class="oc-status oc-status--stable">Live-validated</span>
    </div>
    <p class="oc-workflow__fit">Hermes is genuinely installed — not leftover config.</p>
    <dl class="oc-workflow__facts">
      <div><dt>Command</dt><dd><code>occam connect --only hermes</code></dd></div>
      <div><dt>Where to type</dt><dd>A new Hermes conversation</dd></div>
      <div><dt>Next</dt><dd>Continue only if ready; failed verify → Not connected</dd></div>
    </dl>
  </a>
  <a class="oc-workflow" href="quick-start/#path-a-supported-ai-application">
    <div class="oc-workflow__head">
      <span class="oc-workflow__name">Supported MCP app</span>
      <span class="oc-status oc-status--limited">Tiers apply</span>
    </div>
    <p class="oc-workflow__fit">Another MCP-capable AI application the installer detects.</p>
    <dl class="oc-workflow__facts">
      <div><dt>Command</dt><dd><code>occam connect</code></dd></div>
      <div><dt>Where to type</dt><dd>A new conversation in that app</dd></div>
      <div><dt>Next</dt><dd>Validation tiers differ by host</dd></div>
    </dl>
  </a>
  <a class="oc-workflow oc-workflow--experimental" href="quick-start/#path-d-local-ollama-model-experimental">
    <div class="oc-workflow__head">
      <span class="oc-workflow__name">Local Ollama model</span>
      <span class="oc-status oc-status--experimental">Experimental</span>
    </div>
    <p class="oc-workflow__fit">Tool-capable local model via <code>occam chat</code> — not an MCP host.</p>
    <dl class="oc-workflow__facts">
      <div><dt>Command</dt><dd><code>occam chat</code></dd></div>
      <div><dt>Where to type</dt><dd>The <code>occam chat</code> terminal prompt</dd></div>
      <div><dt>Next</dt><dd>Not native Ollama App · no Ollama Web Search</dd></div>
    </dl>
  </a>
  <a class="oc-workflow" href="quick-start/#path-e-no-supported-application-detected">
    <div class="oc-workflow__head">
      <span class="oc-workflow__name">Developer integration</span>
      <span class="oc-status oc-status--advanced">MCP contract</span>
    </div>
    <p class="oc-workflow__fit">You are wiring your own agent or MCP client.</p>
    <dl class="oc-workflow__facts">
      <div><dt>Command</dt><dd><code>occam snippet</code></dd></div>
      <div><dt>Where to type</dt><dd>Your custom MCP client</dd></div>
      <div><dt>Next</dt><dd>Use the MCP API and generated snippet</dd></div>
    </dl>
  </a>
</div>

[Choose a path and get the exact first prompt](quick-start.md) ·
[See host validation tiers](mcp-hosts.md)

Ollama is a model runtime, not an MCP host. Experimental `occam chat` calls the
documented local Ollama API and uses Occam's acquisition stack. It is not a
native integration inside the Ollama App and does not use Ollama Web Search.

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
use one exact prompt against the same stable page used in the proof.

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
