# Choose how you use AI

Install Occam, choose one workflow, and finish with a real page read. Do not
stop at “installed.”

The same first prompt is used throughout this guide:

> Use Occam to read https://example.com/ and summarize the page. Include the
> source URL.

## Choose your path

| Your setup | Start here |
|------------|------------|
| Supported AI application | [Path A](#path-a-supported-ai-application) |
| Cursor | [Path B](#path-b-cursor) |
| Hermes Agent | [Path C](#path-c-hermes-agent) |
| Local Ollama model | [Path D — experimental](#path-d-local-ollama-model-experimental) |
| No supported application detected | [Path E](#path-e-no-supported-application-detected) |

Occam normally appears as a tool **inside your AI application**. The `occam`
terminal commands install, connect, check, and diagnose that tool. The
experimental Ollama path is different: you type directly in an `occam chat`
terminal.

## Install once

Requires Node.js 20+. The release install does not require the .NET SDK.

=== "Linux / macOS"

    ```bash
    curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh | bash
    ```

=== "Windows"

    ```powershell
    irm https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.ps1 | iex
    ```

The installer downloads and verifies the release archive, checks the local
runtime, detects supported applications, and explains the next action. If it
detects one supported application, it can connect it. If it finds several, it
asks which one to configure.

Read the final status before continuing:

| Status | Meaning |
|--------|---------|
| **Connected and ready** | The host confirmed the connection |
| **Needs restart** | Configuration is present; restart or reload the named application |
| **Not connected** | The attempt failed verification or was undone; Occam is not available in that application |
| **Needs your action** | Complete the named trust, permission, conflict, or paste step |
| **No apps ready** | Occam is installed, but no AI application is connected yet |

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
5. Type:

    > Use Occam to read https://example.com/ and summarize the page. Include
    > the source URL.

| Question | Answer |
|----------|--------|
| **Where do I type?** | In a new conversation in the connected AI application. |
| **What should happen?** | The application asks an Occam page-reading tool to read the live URL, then summarizes the returned page content. |
| **How do I know Occam was used?** | If the application shows tool activity, look for an `occam_*` call, usually `occam_transcode`. The result should identify `https://example.com/` as its source. Do not rely on a plausible answer alone. |
| **What if it was not used?** | Run `occam status`, then `occam smoke`. If the local host is healthy, run `occam connect` again and follow its host-specific instruction. |

Success is a real tool call and live page content, not the model explaining what
Occam is or running `which occam` in a shell.

## Path B: Cursor

Cursor has a live-validated connection path, but a configuration write does not
make an already-open chat reload its tools.

1. Install Occam.
2. If Cursor was not connected during install, run:

    ```bash
    occam connect --only cursor
    ```

3. Restart or reload Cursor if the connect result asks.
4. Open a **new Cursor chat**.
5. Type:

    > Use Occam to read https://example.com/ and summarize the page. Include
    > the source URL.

| Question | Answer |
|----------|--------|
| **Where do I type?** | In a new Cursor chat after the requested restart or reload. |
| **What should happen?** | Cursor calls an Occam tool and uses the returned `Example Domain` content. |
| **How do I know Occam was used?** | Inspect the tool activity Cursor exposes for that chat and look for an `occam_*` tool. UI labels can change, so this guide does not depend on a specific button name. |
| **What if tools are absent?** | Run `occam smoke`. If it passes, run `occam connect --only cursor`, follow the printed restart/reload step, and create another new chat. |

## Path C: Hermes Agent

Hermes must be genuinely installed. A leftover configuration directory is not
enough, and configuration alone does not prove that Hermes loaded Occam.

1. Install Occam.
2. Connect Hermes:

    ```bash
    occam connect --only hermes
    ```

3. Continue only if the result says Hermes is ready or gives a concrete
   restart/session instruction.
4. Open a **new Hermes conversation**.
5. Type:

    > Use Occam to read https://example.com/ and summarize the page. Include
    > the source URL.

| Question | Answer |
|----------|--------|
| **Where do I type?** | In a new Hermes conversation, not in the terminal used for `occam connect`. |
| **What should happen?** | Hermes calls the local Occam MCP server and summarizes the live page. |
| **How do I know Occam was used?** | Hermes should expose or call Occam tools. A shell lookup such as `which occam` is not an Occam web read. |
| **What does Not connected mean?** | Verification did not establish readiness. Occam may have undone the attempted registration. Do not test Hermes as if Occam were available. |
| **What should I retry?** | Run `occam doctor`, then `occam connect --only hermes`. Follow the resulting diagnostic or use another validated path. |

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
5. At the `>` prompt, type:

    > Use Occam to read https://example.com/ and summarize the page. Include
    > the source URL.

6. Type `/exit` to leave the chat.

| Question | Answer |
|----------|--------|
| **Where do I type?** | In the terminal opened by `occam chat`. |
| **What should happen?** | The local model asks Occam to read the URL, and the terminal shows a reading/tool status before the answer. |
| **How do I know Occam was used?** | The answer follows an Occam tool call and uses the live `Example Domain` content. |
| **What if it was not used?** | Read the terminal message. Start Ollama if it is unreachable; install/select a tool-capable model if no compatible model is available; run `occam doctor` if the Occam tools are unavailable. |

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

| Question | Answer |
|----------|--------|
| **Where do I type the first prompt?** | In the supported application you connect later, the `occam chat` terminal, or your custom MCP client. |
| **What exactly do I type?** | Use the same `https://example.com/` first prompt at the top of this guide. |
| **What should happen?** | The chosen host completes a real Occam tool call and returns source-linked page content. |
| **What if it does not?** | Run `occam status`, `occam doctor`, and `occam smoke`; then connect or configure the chosen host. |

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

## You have your first result when

- your selected application or chat terminal made a real Occam tool call;
- the result contains the live `Example Domain` page content and source URL; or
- Occam returned `ok: false` and the application handled the page as unknown
  instead of inventing content.

The reproducible success and controlled failure are available in the
[current proof fixture](examples/current-proof/README.md).

After first success:

| Goal | Next guide |
|------|------------|
| Read a difficult or JavaScript-heavy page | [Read a page](guides/read-a-page.md) |
| Use authenticated access | [Sessions](guides/sessions.md) |
| Research several URLs | [Research multiple sources](guides/research-multiple.md) |
| Check an integrity receipt | [Verify a source](guides/verify-sources.md) |
| Understand the architecture | [How Occam works](how-occam-works.md) |
