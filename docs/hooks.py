"""MkDocs hooks for Occam docs presentation (visual classes only).

Does not alter product claims, schemas, or acquisition semantics.
Root normative files stay at repository root; site pages use in-site links.
"""

from __future__ import annotations

import re
from typing import Any


def on_pre_build(config: dict[str, Any]) -> None:
    return None


_PROSE_PAGES = {
    "what-is-occam.md",
    "why-occam.md",
    "how-occam-works.md",
    "getting-started.md",
    "quick-start.md",
    "choosing-a-tool.md",
    "trust-and-safety.md",
    "ask-ai.md",
    "faq.md",
    "install.md",
    "recipes.md",
    "roadmap.md",
    "quality-baseline.md",
}

_CAPABILITY_PAGES = {
    "acquisition.md",
    "materialization.md",
    "networking.md",
    "sessions.md",
    "experimental.md",
    "operators.md",
    "playbooks.md",
    "datasets.md",
    "receipts.md",
    "receipt_verification.md",
    "concepts.md",
}

_REFERENCE_PAGES = {
    "configuration.md",
    "failure-codes.md",
    "tools-reference.md",
    "transports.md",
    "documentation-map.md",
    "troubleshooting.md",
    "mcp-hosts.md",
}


def _body_classes(page: Any) -> str:
    src = getattr(getattr(page, "file", None), "src_uri", "") or ""
    classes: list[str] = ["oc-docs"]

    if src in {"index.md", "index.html"}:
        classes.extend(["oc-page-home", "oc-home"])
    elif src.startswith("handbook/"):
        classes.extend(["oc-page-handbook", "oc-page-prose"])
    elif src.startswith("tools/"):
        classes.extend(["oc-page-tool", "oc-page-wide"])
    elif src.startswith("guides/") or src.startswith("examples/") or src.startswith("trust/") or src.startswith("connect/"):
        classes.append("oc-page-prose")
    elif src.startswith("reference/") or src in _REFERENCE_PAGES:
        classes.extend(["oc-page-reference", "oc-page-wide"])
    elif src in _CAPABILITY_PAGES:
        classes.append("oc-page-capability")
    elif src in _PROSE_PAGES:
        classes.append("oc-page-prose")
    elif src.startswith("developers/") or src.startswith("architecture/"):
        classes.append("oc-page-prose")

    if src in {
        "experimental.md",
        "examples/watch-experimental.md",
        "examples/crosscheck-experimental.md",
    }:
        classes.append("oc-page-experimental")

    return " ".join(dict.fromkeys(classes))


_STATUS_LINE = re.compile(r"(?m)^(\*\*Status:\*\*\s*)(.+)$")

_STATUS_TOKEN = re.compile(
    r"\b(STABLE|CORE|ADVANCED|LIMITED|EXPERIMENTAL|OPERATOR|INTERNAL|"
    r"USABLE_WITH_LIMITATIONS|PUBLIC_CORE|PUBLIC_ADVANCED)\b"
)


def _status_class(token: str) -> str:
    key = token.lower().replace("usable_with_limitations", "limited")
    key = key.replace("public_core", "core").replace("public_advanced", "advanced")
    mapping = {
        "stable": "stable",
        "core": "core",
        "advanced": "advanced",
        "limited": "limited",
        "experimental": "experimental",
        "operator": "operator",
        "internal": "internal",
    }
    return mapping.get(key, "advanced")


def _decorate_status_line(match: re.Match[str]) -> str:
    prefix = match.group(1)
    rest = match.group(2)

    def repl(m: re.Match[str]) -> str:
        token = m.group(1)
        cls = _status_class(token)
        return f'<span class="oc-status oc-status--{cls}">{token}</span>'

    decorated = _STATUS_TOKEN.sub(repl, rest)
    return f"{prefix}{decorated}"


def on_page_markdown(
    markdown: str,
    page: Any,
    config: dict[str, Any],
    files: Any,
) -> str:
    """Light presentation polish: status badges + handbook chapter meta."""
    src = getattr(getattr(page, "file", None), "src_uri", "") or ""

    if src.startswith("handbook/") and src != "handbook/index.md":
        lines = markdown.splitlines(keepends=True)
        out: list[str] = []
        seen_h1 = False
        wrapped = False
        for line in lines:
            if not seen_h1 and line.startswith("# "):
                seen_h1 = True
                out.append(line)
                continue
            if (
                seen_h1
                and not wrapped
                and line.startswith("**")
                and ("Part " in line or "Status:" in line or "Prerequisites:" in line)
            ):
                stripped = line.strip()
                out.append(
                    f'<p class="oc-chapter-meta" markdown="1">{stripped}</p>\n'
                )
                wrapped = True
                continue
            out.append(line)
        markdown = "".join(out)

    if "**Status:**" in markdown:
        markdown = _STATUS_LINE.sub(_decorate_status_line, markdown)
    return markdown


def on_post_page(output: str, page: Any, config: dict[str, Any]) -> str:
    classes = _body_classes(page)
    if not classes:
        return output

    def inject(match: re.Match[str]) -> str:
        tag = match.group(0)
        if 'class="' in tag:
            return tag.replace('class="', f'class="{classes} ', 1)
        return tag[:-1] + f' class="{classes}">'

    return re.sub(r"<body\b[^>]*>", inject, output, count=1)
