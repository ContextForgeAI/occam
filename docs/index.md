<div class="oc-hero" markdown="1">

<p class="oc-wordmark">Local host · MCP</p>

</div>

# Occam

<div class="oc-hero oc-hero--rest" markdown="1">

<p class="oc-hero-lead">
A locally run host that helps AI agents acquire usable web content, shape it for context windows, fail honestly when content is unknown, and optionally attach integrity artifacts verifiable against a key.
</p>

<p class="oc-hero-actions">
<a class="oc-btn oc-btn--primary" href="quick-start/">Get started</a>
<a class="oc-btn oc-btn--secondary" href="handbook/">Read the handbook</a>
</p>

<div class="oc-proof" markdown="0">
<span class="oc-prompt">$</span> occam connect<br>
<br>
Detects supported AI / MCP hosts and configures live-validated ones.<br>
Statuses: <span class="oc-ok">Ready</span> · Almost ready · Action required · Not ready
</div>

</div>

<nav class="oc-path" aria-label="Occam request path">
  <span class="oc-path__step">URL</span>
  <span class="oc-path__sep" aria-hidden="true">→</span>
  <span class="oc-path__step"><span class="oc-system-label">Acquire</span></span>
  <span class="oc-path__sep" aria-hidden="true">→</span>
  <span class="oc-path__step"><span class="oc-system-label">Shape</span></span>
  <span class="oc-path__sep" aria-hidden="true">→</span>
  <span class="oc-path__step">usable context</span>
  <span class="oc-path__branch">
    <span class="oc-path__sep" aria-hidden="true">↳</span>
    <span class="oc-path__step oc-path__step--gate"><span class="oc-system-label">Unknown</span> <code>ok:false</code></span>
    <span class="oc-path__sep" aria-hidden="true">·</span>
    <span class="oc-path__step"><span class="oc-system-label">Check</span> vs key</span>
  </span>
</nav>

<div class="oc-pillars" markdown="1">

<div class="oc-pillar" markdown="1">
<div class="oc-pillar-label">Acquire</div>
<p>HTTP → browser → optional managed acquisition — a gated ladder. Live extract by default.</p>
</div>

<div class="oc-pillar" markdown="1">
<div class="oc-pillar-label">Shape</div>
<p>Token-bounded, focused Markdown and structured fields sized for agent context windows.</p>
</div>

<div class="oc-pillar" markdown="1">
<div class="oc-pillar-label">Check</div>
<p>Honest <code>ok:false</code> when content is unknown, plus optional integrity artifacts vs a key.</p>
</div>

</div>

<p class="oc-meta-line"><strong>Version:</strong> 1.0.0-rc.2 · <strong>License:</strong> AGPL-3.0-or-later · <a href="https://contextforgeai.github.io/occam/">Docs site</a> · <a href="https://raw.githubusercontent.com/ContextForgeAI/occam/main/llms.txt"><code>llms.txt</code></a> · <a href="ask-ai/">Ask AI</a></p>

<p class="oc-meta-line">Core MCP tools are registry-defined; runtime <code>tools/list</code> varies by <strong>profile</strong> and <strong>opt-in</strong> flags — do not treat a fixed “15” as a health check by itself.</p>

## Start with a task

<div class="oc-task-list" markdown="1">

[**Read one page**](guides/read-a-page.md) — `occam_transcode` · [Quick Start](quick-start.md)

[**Research several sources**](guides/research-multiple.md) — `occam_digest`

[**Search & discover**](guides/search-and-discover.md) — probe / map / search

[**Login / session walls**](guides/sessions.md) — [Sessions](sessions.md)

[**Extract structured fields**](guides/structured-extraction.md) — playbooks · [Playbooks](playbooks.md)

[**Verify an artifact**](guides/verify-sources.md) — receipts · `occam_verify`

[**Understand Occam deeply**](handbook/index.md) — [What is Occam?](what-is-occam.md) · [How Occam works](how-occam-works.md)

</div>

More task → tool routes: [Choosing a tool](choosing-a-tool.md) · [Examples](examples/index.md) · [Recipes](recipes.md) · [Claims](guides/claims.md)

## Explore the system

<div class="oc-explore" markdown="1">

**Acquisition** — [Acquisition](acquisition.md) · [Networking](networking.md) · [Sessions](sessions.md)

**Materialization** — [Materialization](materialization.md) · [Concepts](concepts.md)

**Discovery** — [Search & discover](guides/search-and-discover.md) · [Choosing a tool](choosing-a-tool.md)

**Playbooks** — [Playbooks](playbooks.md) · [Datasets](datasets.md)

**Trust** — [Trust & Safety](trust-and-safety.md) · [Receipts](receipts.md) · [Receipt verification](receipt_verification.md)

**Operations** — [Install](install.md) · [Getting started](getting-started.md) · [Operators](operators.md) · [Connect](connect/index.md) · [MCP hosts](mcp-hosts.md)

</div>

## Deeper routes

[Tool index](tools/index.md) · [Tools reference](tools-reference.md) · [Configuration](configuration.md) · [Transports](transports.md) · [Failure codes](failure-codes.md) · [MCP API](reference/mcp-api.md) · [Handbook](handbook/index.md) · [Experimental](experimental.md) · [Operators](operators.md) · [Troubleshooting](troubleshooting.md) · [FAQ](faq.md) · [Full documentation map](documentation-map.md)

## Packages

This RC ships via **GitHub Release** archives — **npm is not a GA 1.0 channel.**

- [`@ff-occam/mcp`](https://github.com/ContextForgeAI/occam/tree/main/packages/occam-mcp) — launcher package (non-GA)  
- [`@ff-occam/agent-sdk`](https://github.com/ContextForgeAI/occam/tree/main/packages/occam-agent-sdk) — TypeScript workflows  
- [`@ff-occam/skill`](https://github.com/ContextForgeAI/occam/tree/main/packages/occam-skill) — portable agent skill  

## All top-level pages

Compact index (gate + scanning). Narrative map: [Documentation map](documentation-map.md).

[acquisition](acquisition.md) · [ask-ai](ask-ai.md) · [choosing-a-tool](choosing-a-tool.md) · [concepts](concepts.md) · [configuration](configuration.md) · [datasets](datasets.md) · [documentation-map](documentation-map.md) · [experimental](experimental.md) · [failure-codes](failure-codes.md) · [faq](faq.md) · [friend-test](friend-test.md) · [getting-started](getting-started.md) · [how-occam-works](how-occam-works.md) · [install](install.md) · [materialization](materialization.md) · [mcp-hosts](mcp-hosts.md) · [networking](networking.md) · [operators](operators.md) · [playbooks](playbooks.md) · [quality-baseline](quality-baseline.md) · [quick-start](quick-start.md) · [receipt_verification](receipt_verification.md) · [receipts](receipts.md) · [recipes](recipes.md) · [roadmap](roadmap.md) · [sessions](sessions.md) · [tools-reference](tools-reference.md) · [transports](transports.md) · [troubleshooting](troubleshooting.md) · [trust-and-safety](trust-and-safety.md) · [what-is-occam](what-is-occam.md)
