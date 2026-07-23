# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| 1.0.0-rc.2 (current RC) | Yes |
| 0.9.x | Security fixes only until GA `1.0.0` |
| < 0.9 | No — upgrade to the current RC or later |

Only the current supported line receives security patches. There is no long-term backport program.

## Reporting a Vulnerability

**Please do not open public issues for security vulnerabilities.**

Instead, report privately:

- **Email:** *(add your security contact email here)*
- Or use GitHub's [private vulnerability reporting](https://docs.github.com/en/code-security/security-advisories/guidelines-on-reporting-and-writing-information-about-vulnerabilities) if enabled.

Include in your report:
1. Affected version
2. Steps to reproduce
3. Impact assessment
4. Suggested fix (if any)

We acknowledge reports within 72 hours and aim for a fix within 30 days.

## Security Boundaries

FF-Occam MCP is a **local-first** tool. Understanding its trust model:

### What Occam does
- Fetches web pages via HTTP or Playwright Chromium
- Extracts content (DOM, Markdown, structured facts)
- Returns typed results or honest failures
- Processes data locally — no cloud service

### What Occam does NOT do
- No cloud API or telemetry endpoint
- No automatic updates or phone-home
- No credential storage (session profiles are local files, user-managed)
- No execution of fetched content (no eval, no script execution beyond Playwright)

### Trust boundaries

| Boundary | Trust level | Notes |
|----------|-------------|-------|
| Local filesystem | Full | Occam reads workers, playbooks, sessions from `OCCAM_HOME` |
| Network (egress) | Untrusted | All fetched content is untrusted; never execute |
| MCP client | Full | Your host process spawns Occam; stdio/WS is local |
| Node.js workers | High | Spawned as child processes; output is JSON-parsed |
| Playwright browser | Sandboxed | Chromium in separate process; `storageState` for sessions |
| Session profiles | User-managed | Local JSON files; never commit to repos |

## Known Risks

### 1. Untrusted content extraction

Web pages may contain malicious content. Occam:
- Never executes fetched HTML/JS (except in Playwright for rendering)
- Sanitizes output to Markdown (strips scripts/styles)
- Returns typed failures instead of potentially dangerous content

**Operator advice:** Review `worker/` code if you process sensitive pages. The extraction pipeline is deterministic — no external model calls.

### 2. Session profile leakage

Session profiles stored under `OCCAM_SESSIONS_ROOT` (default `~/.occam/sessions/`) contain cookies and tokens. These are:
- Local files only — never transmitted
- Loaded into the extraction process memory
- **Never logged or included in telemetry**

**Operator advice:** Add `~/.occam/sessions/` to your `.gitignore`. Never commit session files.

### 3. Egress proxy trust

If using `OCCAM_PROXY_LIST`, traffic flows through the proxy operator's infrastructure. Choose proxies you trust.

### 4. Browser sandboxing

Playwright Chromium runs with the same permissions as the Occam process. For high-security environments, run Occam in a container or restricted user account.

## Hardening Recommendations

For production deployments:

1. **Run as non-root** — create a dedicated user for Occam
2. **File permissions** — `OCCAM_HOME` should be readable only by the Occam user
3. **Network** — firewall egress if you only need specific domains
4. **Session hygiene** — periodically rotate session profiles
5. **Audit** — review stderr logs for unexpected activity

## Security Response Process

1. Report received and acknowledged (< 72h)
2. Investigation and impact assessment (< 7 days)
3. Fix developed and tested (< 30 days)
4. Security advisory published (GitHub + CHANGELOG)
5. Patch release issued

For critical vulnerabilities (RCE, credential leakage), we aim for an expedited timeline.
