# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| 1.0.0-rc.2 (current RC) | Yes |
| 0.9.x | Security fixes only until GA `1.0.0` |
| < 0.9 | No — upgrade to the current RC or later |

Support is limited to the rows marked above. The temporary `0.9.x` backport
window ends at GA `1.0.0`; there is no long-term backport program.

## Reporting a Vulnerability

**Please do not open public issues for security vulnerabilities.**

This repository does not currently expose GitHub private vulnerability
reporting, and no public security email is published. Until one of those
channels is enabled, open a public issue **without vulnerability details** and
ask the maintainer to establish a private channel before sending the report.

Include in your report:
1. Affected version
2. Steps to reproduce
3. Impact assessment
4. Suggested fix (if any)

No response-time target is published until a monitored confidential reporting
channel exists.

## Security Boundaries

Occam is a **local-first** MCP tool. Understanding its trust model:

### What Occam does
- Fetches web pages via HTTP or Playwright Chromium
- Extracts content (DOM, Markdown, structured facts)
- Returns typed results or honest failures
- Processes data locally — no cloud service required for core extract

### What Occam does NOT do
- Core extraction requires no Occam-hosted cloud service or remote telemetry endpoint
- No automatic self-update; `occam status` and `occam update` query the GitHub Releases API, while normal MCP extraction does not
- No managed credential vault; optional session profiles are credential-bearing local files controlled by the operator
- The HTTP backend does not evaluate page scripts; the browser backend renders page code in Playwright Chromium

Configured search, managed acquisition, translation, proxy, timestamp-authority, or remote transport integrations expand the network boundary. Review their endpoints before enabling them.

### Trust boundaries

| Boundary | Trust level | Notes |
|----------|-------------|-------|
| Local filesystem | Full | Occam reads workers, playbooks, sessions from `OCCAM_HOME` |
| Network (egress) | Untrusted | All fetched content is untrusted; the browser backend may execute page code while rendering |
| MCP client | Trusted operator boundary | Stdio is local by default; any enabled remote transport expands the boundary |
| Node.js workers | High | Spawned as child processes; output is JSON-parsed |
| Playwright browser | Separate process; not privilege-isolated | Chromium inherits the Occam user's OS permissions; browser sandbox behavior depends on the host environment |
| Session profiles | User-managed | Local JSON files; never commit to repos |

## Known Risks

### 1. Untrusted content extraction

Web pages may contain malicious content. Occam:
- Does not evaluate page scripts in the HTTP backend
- Executes page code when the Playwright browser backend is used, including automatic fallback from HTTP extraction
- Converts extracted content to Markdown and removes script/style elements from returned content
- Returns typed failures when extraction cannot produce an accepted result

**Operator advice:** Review `workers/` code if you process sensitive pages. Core
extraction is model-free; it does not require an external LLM call. Live pages,
network responses, and browser-rendered DOM can still change between calls.

### 2. Session profile leakage

Session profiles stored under `OCCAM_SESSIONS_ROOT` (default `~/.occam/sessions/`) contain cookies, headers, or tokens. These are:
- Stored as local files under the operator's control
- Loaded into extraction process memory when selected
- Sent as applicable to the requested origin through the selected backend or configured proxy
- Designed not to be logged or included in result telemetry

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

1. Establish a private channel without moving vulnerability details into a public issue
2. Acknowledge receipt and begin impact assessment
3. Agree on disclosure timing and a severity-based remediation plan
4. Develop and verify the fix
5. Publish a security advisory and patch release when required

For critical vulnerabilities (RCE, credential leakage), we aim for an expedited timeline.
