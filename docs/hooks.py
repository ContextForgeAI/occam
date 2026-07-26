"""MkDocs hooks reserved for future docs build helpers.

Root normative files (INSTALL.md, MCP_API_SPEC.md, SECURITY.md, …) stay at the
repository root. Site pages under docs/ are written for in-site relative links
and point at GitHub when the full root document is the source of truth.
"""


def on_pre_build(config) -> None:
    return None
