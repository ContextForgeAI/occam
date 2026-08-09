# Choose how you use AI

Install Occam, choose one workflow, and finish with a real page read. Do not
stop at “installed.”

<div class="oc-first-prompt" markdown="1">

**Quick connection test** (same for every path):

> Use Occam to read https://example.com/ and summarize the page. Include the
> source URL.

This proves the tool call works. The page is deliberately small.

**Optional richer prompt** (after the connection test succeeds):

> Use Occam to read https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide
> and summarize what the guide covers. Include the source URL.

This is a stable public documentation page used in Occam’s smoke corpus — useful
enough to show compact, source-linked content without relying on a flaky site.

</div>

## Choose your path

<div class="oc-path-chooser" markdown="0">
  <a class="oc-path-card" href="#path-b-cursor">
    <span class="oc-path-card__name">Cursor</span>
    <span class="oc-status oc-status--friendly oc-status--stable">Validated</span>
    <span class="oc-path-card__hint">New Cursor chat after connect</span>
    <span class="oc-path-card__cta">Open Cursor guide →</span>
  </a>
  <a class="oc-path-card" href="#path-c-hermes-agent">
    <span class="oc-path-card__name">Hermes Agent</span>
    <span class="oc-status oc-status--friendly oc-status--limited">Supported · verification required</span>
    <span class="oc-path-card__hint">Only if Hermes is genuinely installed</span>
    <span class="oc-path-card__cta">Open Hermes guide →</span>
  </a>
  <a class="oc-path-card" href="#path-a-supported-ai-application">
    <span class="oc-path-card__name">Another AI app</span>
    <span class="oc-status oc-status--friendly oc-status--limited">Support varies</span>
    <span class="oc-path-card__hint">Another MCP-capable application</span>
    <span class="oc-path-card__cta">Use another AI app →</span>
  </a>
  <a class="oc-path-card oc-path-card--experimental" href="#path-d-local-ollama-model-experimental">
    <span class="oc-path-card__name">Local Ollama model</span>
    <span class="oc-status oc-status--friendly oc-status--experimental">Experimental</span>
    <span class="oc-path-card__hint">Type in the Occam chat terminal</span>
    <span class="oc-path-card__cta">Start local Ollama chat →</span>
  </a>
</div>

<div class="oc-recovery" markdown="0">
  <div class="oc-recovery__label">No supported app detected?</div>
  <p class="oc-recovery__text">The install can still succeed. Connect a host later, use experimental chat, or integrate your own MCP client.</p>
  <a class="oc-recovery__cta" href="#path-e-no-supported-application-detected">Open recovery options →</a>
</div>

Occam normally appears as a tool **inside your AI application**. The `occam`
terminal commands install, connect, check, and diagnose that tool. The
experimental Ollama path is different: you type directly in an `occam chat`
terminal.

## Install once

Requires Node.js 20+. The release install does not require the .NET SDK.

=== "Linux x64 / macOS Apple Silicon"

    ```bash
    curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh | bash
    ```

=== "Windows x64"

    ```powershell
    irm https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.ps1 | iex
    ```

The installer verifies the requested release and complete bundled runtime before
replacing an existing install. Post-install helpers come from that verified
archive, not the mutable repository branch. It then checks the local runtime,
detects supported applications, and explains the next action. If it detects one
supported application, it can connect it. If it finds several, it asks which one
to configure.

Release binaries exist for Windows x64, Linux x64, and Apple Silicon macOS. An
unsupported CPU/OS combination is rejected before download.

Read the final status before continuing:

<div class="oc-status-grid" markdown="0">
  <div class="oc-status-row"><strong>Connected and ready</strong><span>The host confirmed the connection</span></div>
  <div class="oc-status-row"><strong>Needs restart</strong><span>Configuration is present; restart or reload the named application</span></div>
  <div class="oc-status-row"><strong>Not connected</strong><span>The attempt failed verification or was undone; Occam is not available in that application</span></div>
  <div class="oc-status-row"><strong>Needs your action</strong><span>Complete the named trust, permission, conflict, or paste step</span></div>
  <div class="oc-status-row"><strong>No apps ready</strong><span>Occam is installed, but no AI application is connected yet</span></div>
</div>

`npm` / `npx` is not the supported 1.0 release install path. See
[Install](install.md) for the full contract.

## Path A: supported AI application

Use this path for a supported application that the installer reports as ready,
configured, or awaiting restart. Validation tiers and exact behavior differ by
host; see [MCP hosts](mcp-hosts.md).

1. Install Occam.
2. Select an application when the installer asks, or run:

    ```bash
    occam connect
    ```

3. Follow the exact restart, reload, trust, or paste instruction in the result.
4. Open a **new conversation** in the selected application.
5. Type the [quick connection test](#choose-how-you-use-ai) prompt.

<div class="oc-qa" markdown="0">
  <div class="oc-qa__item">
    <div class="oc-qa__q">Where do I type?</div>
    <div class="oc-qa__a">In a new conversation in the connected AI application.</div>
  </div>
  <div class="oc-qa__item">
    <div class="oc-qa__q">What should happen?</div>
    <div class="oc-qa__a">The application asks an Occam page-reading tool to read the live URL, then summarizes the returned page content.</div>
  </div>
  <div class="oc-qa__item">
    <div class="oc-qa__q">How do I know Occam was used?</div>
    <div class="oc-qa__a">If the application shows tool activity, look for an <code>occam_*</code> call, usually <code>occam_transcode</code>. The result should identify <code>https://example.com/</code> as its source. Do not rely on a plausible answer alone.</div>
  </div>
  <div class="oc-qa__item oc-qa__item--recover">
    <div class="oc-qa__q">What if it was not used?</div>
    <div class="oc-qa__a">Run <code>occam status</code>, then <code>occam smoke</code>. If the local host is healthy, run <code>occam connect</code> again and follow its host-specific instruction.</div>
  </div>
</div>

!!! tip "Success signal"

    A real tool call and live page content — not the model explaining what
    Occam is or running `which occam` in a shell.

## Path B: Cursor

Cursor has a validated connection path, but a configuration write does not
make an already-open chat reload its tools.

1. Install Occam.
2. If Cursor was not connected during install, run:

    ```bash
    occam connect --only cursor
    ```

3. Restart or reload Cursor if the connect result asks.
4. Open a **new Cursor chat**.
5. Type the [quick connection test](#choose-how-you-use-ai) prompt.

<div class="oc-qa" markdown="0">
  <div class="oc-qa__item">
    <div class="oc-qa__q">Where do I type?</div>
    <div class="oc-qa__a">In a new Cursor chat after the requested restart or reload.</div>
  </div>
  <div class="oc-qa__item">
    <div class="oc-qa__q">What should happen?</div>
    <div class="oc-qa__a">Cursor calls an Occam tool and uses the returned <code>Example Domain</code> content.</div>
  </div>
  <div class="oc-qa__item">
    <div class="oc-qa__q">How do I know Occam was used?</div>
    <div class="oc-qa__a">Inspect the tool activity Cursor exposes for that chat and look for an <code>occam_*</code> tool. UI labels can change, so this guide does not depend on a specific button name.</div>
  </div>
  <div class="oc-qa__item oc-qa__item--recover">
    <div class="oc-qa__q">What if tools are absent?</div>
    <div class="oc-qa__a">Run <code>occam smoke</code>. If it passes, run <code>occam connect --only cursor</code>, follow the printed restart/reload step, and create another new chat.</div>
  </div>
</div>

## Path C: Hermes Agent

Hermes must be genuinely installed. A leftover configuration directory is not
enough, and configuration alone does not prove that Hermes loaded Occam.
Continue only when connect verification succeeds — do not treat Hermes as ready
from a config write alone.

1. Install Occam.
2. Connect Hermes:

    ```bash
    occam connect --only hermes
    ```

3. Continue only if the result says Hermes is ready or gives a concrete
   restart/session instruction. If the result is **Not connected**, stop and use
   another path.
4. Open a **new Hermes conversation**.
5. Type the [quick connection test](#choose-how-you-use-ai) prompt.

<div class="oc-qa" markdown="0">
  <div class="oc-qa__item">
    <div class="oc-qa__q">Where do I type?</div>
    <div class="oc-qa__a">In a new Hermes conversation, not in the terminal used for <code>occam connect</code>.</div>
  </div>
  <div class="oc-qa__item">
    <div class="oc-qa__q">What should happen?</div>
    <div class="oc-qa__a">Hermes calls the local Occam MCP server and summarizes the live page.</div>
  </div>
  <div class="oc-qa__item">
    <div class="oc-qa__q">How do I know Occam was used?</div>
    <div class="oc-qa__a">Hermes should expose or call Occam tools. A shell lookup such as <code>which occam</code> is not an Occam web read.</div>
  </div>
  <div class="oc-qa__item">
    <div class="oc-qa__q">What does Not connected mean?</div>
    <div class="oc-qa__a">Verification did not establish readiness. Occam may have undone the attempted registration. Do not test Hermes as if Occam were available.</div>
  </div>
  <div class="oc-qa__item oc-qa__item--recover">
    <div class="oc-qa__q">What should I retry?</div>
    <div class="oc-qa__a">Run <code>occam doctor</code>, then <code>occam connect --only hermes</code>. Follow the resulting diagnostic or use another validated path.</div>
  </div>
</div>

## Path D: local Ollama model (experimental)

`occam chat` is an experimental local-chat path, not a stable 1.0 interface.
Ollama is a model runtime, not an MCP host.

1. Start Ollama.
2. Make sure at least one installed model reports tool support.
3. Run:

    ```bash
    occam chat
    ```

4. Select a tool-capable model or accept the offered default.
5. At the `>` prompt, type the [quick connection test](#choose-how-you-use-ai)
   prompt.
6. Type `/exit` to leave the chat.

<div class="oc-qa" markdown="0">
  <div class="oc-qa__item">
    <div class="oc-qa__q">Where do I type?</div>
    <div class="oc-qa__a">In the terminal opened by <code>occam chat</code>.</div>
  </div>
  <div class="oc-qa__item">
    <div class="oc-qa__q">What should happen?</div>
    <div class="oc-qa__a">The local model asks Occam to read the URL, and the terminal shows a reading/tool status before the answer.</div>
  </div>
  <div class="oc-qa__item">
    <div class="oc-qa__q">How do I know Occam was used?</div>
    <div class="oc-qa__a">The answer follows an Occam tool call and uses the live <code>Example Domain</code> content.</div>
  </div>
  <div class="oc-qa__item oc-qa__item--recover">
    <div class="oc-qa__q">What if it was not used?</div>
    <div class="oc-qa__a">Read the terminal message. Start Ollama if it is unreachable; install/select a tool-capable model if no compatible model is available; run <code>occam doctor</code> if the Occam tools are unavailable.</div>
  </div>
</div>

This path requires no Ollama login and does not use Ollama Web Search. Occam
calls the documented local Ollama API and uses its own acquisition stack. It is
not a native integration inside the Ollama App. A custom remote `OLLAMA_HOST`
changes the network boundary.

No model names are promised here: compatibility is experimental and should be
checked against the models installed on your machine.

## Path E: no supported application detected

No detected host does **not** mean the Occam installation failed. It means the
local runtime is installed but has nowhere to expose its tools yet.

Choose one:

1. Install a [supported AI application](mcp-hosts.md), then run:

    ```bash
    occam connect
    ```

2. If local Ollama is running with a tool-capable model, use the
   [experimental chat path](#path-d-local-ollama-model-experimental).
3. If you are integrating your own MCP client, generate the launch snippet:

    ```bash
    occam snippet
    ```

    Then use the [transport](transports.md) and
    [MCP API](reference/mcp-api.md) documentation.

<div class="oc-qa" markdown="0">
  <div class="oc-qa__item">
    <div class="oc-qa__q">Where do I type the first prompt?</div>
    <div class="oc-qa__a">In the supported application you connect later, the <code>occam chat</code> terminal, or your custom MCP client.</div>
  </div>
  <div class="oc-qa__item">
    <div class="oc-qa__q">What exactly do I type?</div>
    <div class="oc-qa__a">Use the same quick connection test at the top of this guide.</div>
  </div>
  <div class="oc-qa__item">
    <div class="oc-qa__q">What should happen?</div>
    <div class="oc-qa__a">The chosen host completes a real Occam tool call and returns source-linked page content.</div>
  </div>
  <div class="oc-qa__item oc-qa__item--recover">
    <div class="oc-qa__q">What if it does not?</div>
    <div class="oc-qa__a">Run <code>occam status</code>, <code>occam doctor</code>, and <code>occam smoke</code>; then connect or configure the chosen host.</div>
  </div>
</div>

## Diagnostics

These are current public commands:

```bash
occam status   # installed version and setup summary
occam doctor   # runtime, browser, and web-safety checks
occam smoke    # local MCP initialize, tools/list, and live probe
occam connect  # detect and connect supported applications
occam update   # read-only release check
```

Use the result in this order:

1. If `occam doctor` fails, fix the local runtime first.
2. If `occam smoke` fails, the local MCP host is not ready.
3. If both pass but the application has no tools, reconnect that application
   and follow its restart/reload instruction.
4. If connect reports **Not connected**, do not treat that host as ready.

More help: [Troubleshooting](troubleshooting.md).

## Preview removal

Before deciding to keep Occam, you can inspect exactly what removal would
change:

```bash
occam disconnect --dry-run  # managed host registrations only
occam uninstall --dry-run   # registrations, generated launcher, release tree
```

Default uninstall preserves local state, skills, backups, the response cache,
and the shared Playwright browser cache. State and cache cleanup require
explicit flags. See [Install: disconnect or uninstall](install.md#disconnect-or-uninstall)
for the complete boundary.

## You have your first result when

!!! success "First-result checklist"

    - your selected application or chat terminal made a real Occam tool call;
    - the result contains the live `Example Domain` page content and source URL; or
    - Occam returned `ok: false` and the application handled the page as unknown
      instead of inventing content.

The reproducible success and controlled failure are available in the
[current proof fixture](examples/current-proof/README.md).

After first success:

<div class="oc-next-grid" markdown="0">
  <a class="oc-next" href="guides/read-a-page/"><strong>Read a difficult page</strong><span>JavaScript-heavy sources</span></a>
  <a class="oc-next" href="guides/sessions/"><strong>Authenticated access</strong><span>Sessions / login walls</span></a>
  <a class="oc-next" href="guides/research-multiple/"><strong>Research several URLs</strong><span>Multi-source digest</span></a>
  <a class="oc-next" href="guides/verify-sources/"><strong>Check an integrity receipt</strong><span>Verify a source</span></a>
  <a class="oc-next" href="how-occam-works/"><strong>Understand the architecture</strong><span>How Occam works</span></a>
</div>
