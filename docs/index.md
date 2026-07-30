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

<div class="oc-proof" markdown="0">
<span class="oc-prompt">URL</span> https://example.com/<br>
<br>
559 decoded HTML bytes → <span class="oc-ok">167 Markdown bytes</span><br>
Current source · exact method · reproducible result
</div>

</div>

For developers and technical users building or running AI agents, especially in
local and self-hosted environments.

## Real output

At source SHA `3d871d34f52180f8e0046f505de577b6aa3417e4`,
Occam read [`https://example.com/`](https://example.com/) using
`occam_transcode` with only its required `url` argument:

```markdown
# Example Domain

This domain is for use in documentation examples without needing permission.
Avoid use in operations.
```

The HTML response body was **559 UTF-8 bytes** after HTTP decoding. The
returned Markdown was **167 UTF-8 bytes** — **70.1% fewer bytes in this
example**. No tokenizer was used, so this is not a token-savings claim.

The result keeps the requested and final URLs, extraction backend, content
hash, and an optional signed integrity receipt. Inspect the
[complete output, measurement method, and reproduction scripts](examples/current-proof/README.md).

## Webpages were designed for people, not context windows

An ordinary page can contain navigation, scripts, repeated interface text,
cookie controls, footers, and large amounts of raw markup around the part an
agent actually needs.

Passing all of that through wastes a local model's limited context and makes
larger models work around irrelevant text. A search result can help find the
source; Occam focuses on reading the chosen source and preparing usable page
content for the agent.

## A useful result — or an explicit unknown

**When the read succeeds**, Occam returns compact content with its source URL
and result metadata.

**When the read does not succeed**, Occam returns `ok: false` with a failure
code. The content is unknown; the agent should not fill the gap from memory.

The proof fixture includes a controlled private-destination case that returns:

```json
{
  "ok": false,
  "failure": {
    "code": "private_url_blocked",
    "message": "Private or local URLs are blocked."
  }
}
```

This predictable failure is a trust feature, not the main reason to try Occam:
start with the successful read.

## Choose your workflow

<div class="oc-task-list" markdown="1">

**Supported AI application** — install, run `occam connect`, then open a new
conversation. Validation tiers apply; not every host is automatic.

**Cursor** — connect, restart or reload when asked, then open a new chat.
Live-validated connection path.

**Hermes Agent** — connect only when Hermes is genuinely installed.
Live-validated; failed verification can report **Not connected**.

**Local Ollama runtime** — run `occam chat` with a tool-capable local model.
**Experimental.**

**Your own agent** — use the MCP contract and generated snippet.

</div>

[Choose a path and get the exact first prompt](quick-start.md) ·
[See host validation tiers](mcp-hosts.md)

Ollama is a model runtime, not an MCP host. Experimental `occam chat` calls the
documented local Ollama API and uses Occam's acquisition stack. It is not a
native integration inside the Ollama App and does not use Ollama Web Search.

## How Occam works

<nav class="oc-path" aria-label="Occam request path">
  <span class="oc-path__step">web page</span>
  <span class="oc-path__sep" aria-hidden="true">→</span>
  <span class="oc-path__step"><span class="oc-system-label">acquisition</span></span>
  <span class="oc-path__sep" aria-hidden="true">→</span>
  <span class="oc-path__step">useful content</span>
  <span class="oc-path__sep" aria-hidden="true">→</span>
  <span class="oc-path__step">compact agent context</span>
  <span class="oc-path__sep" aria-hidden="true">→</span>
  <span class="oc-path__step">source information</span>
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

Normal page reading runs on your machine by default. Private and local
destinations are denied unless the operator explicitly allows them. Connection
changes require an explicit install/connect action, and failed host
verification can be undone.

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

## All top-level pages

Compact index (gate + scanning). Narrative map: [Documentation map](documentation-map.md).

[acquisition](acquisition.md) · [ask-ai](ask-ai.md) · [choosing-a-tool](choosing-a-tool.md) · [concepts](concepts.md) · [configuration](configuration.md) · [datasets](datasets.md) · [documentation-map](documentation-map.md) · [experimental](experimental.md) · [failure-codes](failure-codes.md) · [faq](faq.md) · [friend-test](friend-test.md) · [getting-started](getting-started.md) · [how-occam-works](how-occam-works.md) · [install](install.md) · [materialization](materialization.md) · [mcp-hosts](mcp-hosts.md) · [networking](networking.md) · [operators](operators.md) · [playbooks](playbooks.md) · [quality-baseline](quality-baseline.md) · [quick-start](quick-start.md) · [receipt_verification](receipt_verification.md) · [receipts](receipts.md) · [recipes](recipes.md) · [roadmap](roadmap.md) · [sessions](sessions.md) · [tools-reference](tools-reference.md) · [transports](transports.md) · [troubleshooting](troubleshooting.md) · [trust-and-safety](trust-and-safety.md) · [what-is-occam](what-is-occam.md)
