#!/usr/bin/env python3
"""Contract self-test for the WRB Occam runner."""

from __future__ import annotations

import importlib.util
import os
from pathlib import Path
import sys
import tempfile
import textwrap
import types


def _load_runner():
    base = types.ModuleType("base")
    base.Runner = type("Runner", (), {})
    sys.modules["base"] = base
    path = Path(__file__).with_name("occam.py")
    spec = importlib.util.spec_from_file_location("wrb_occam", path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module.Runner


FAKE_HOST = r"""
import json
import sys

for line in sys.stdin:
    message = json.loads(line)
    if "id" not in message:
        continue
    method = message.get("method")
    if method == "initialize":
        result = {
            "protocolVersion": "2024-11-05",
            "capabilities": {"tools": {}},
            "serverInfo": {"name": "fake-occam", "version": "0"},
        }
    elif method == "tools/call":
        params = message["params"]
        name = params["name"]
        args = params["arguments"]
        if name == "occam_transcode":
            if args["url"].endswith("/blocked"):
                payload = {
                    "ok": False,
                    "backend": "node_readability_turndown",
                    "failure": {"code": "http_401"},
                }
            else:
                payload = {
                    "ok": True,
                    "markdown": "needle-" + ("x" * 40),
                    "backend": "npm_registry_package",
                    "url": {
                        "requestedUrl": args["url"],
                        "finalUrl": "https://registry.example.test/package/latest",
                    },
                }
        elif name == "occam_search":
            payload = {
                "ok": True,
                "results": [
                    {"title": "Result", "url": "https://example.com", "snippet": "needle"}
                ],
            }
        elif name == "occam_map" and args["source"] == "sitemap":
            payload = {"ok": False, "failureCode": "sitemap_not_found"}
        elif name == "occam_map":
            payload = {
                "ok": True,
                "links": [
                    {"url": "https://example.com/docs", "title": "Docs"}
                ],
            }
        else:
            payload = {"ok": False}
        result = {
            "content": [{"type": "text", "text": json.dumps(payload)}],
            "structuredContent": payload,
        }
    else:
        result = {}
    print(json.dumps({"jsonrpc": "2.0", "id": message["id"], "result": result}), flush=True)
"""


def main() -> None:
    Runner = _load_runner()
    old_command = os.environ.get("OCCAM_WRB_COMMAND")
    old_timeout = os.environ.get("OCCAM_WRB_TIMEOUT_MS")
    try:
        with tempfile.TemporaryDirectory(prefix="occam-wrb-selftest-") as temp:
            host = Path(temp) / "fake_host.py"
            host.write_text(textwrap.dedent(FAKE_HOST), encoding="utf-8")
            os.environ["OCCAM_WRB_COMMAND"] = f'"{sys.executable}" "{host}"'
            os.environ["OCCAM_WRB_TIMEOUT_MS"] = "5000"

            runner = Runner()
            fetched = runner.fetch("https://example.com", max_chars=12)
            assert fetched["success"] is True
            assert fetched["content"] == "needle-xxxxx"
            assert fetched["tokens"] == 3
            assert fetched["tier"] == "npm_registry_package"
            assert fetched["backend"] == "npm_registry_package"
            assert fetched["final_url"] == "https://registry.example.test/package/latest"
            assert fetched["failure_code"] is None

            blocked = runner.fetch("https://example.com/blocked")
            assert blocked["success"] is False
            assert blocked["content"] == ""
            assert blocked["backend"] == "node_readability_turndown"
            assert blocked["final_url"] is None
            assert blocked["failure_code"] == "http_401"

            searched = runner.search("needle", max_results=1)
            assert searched["success"] is True
            assert searched["results"][0]["url"] == "https://example.com"
            assert searched["tokens"] > 0

            crawled = runner.crawl(
                "https://example.com", focus="docs", max_pages=4
            )
            assert crawled["success"] is True
            assert crawled["mode"] == "occam_map_proxy"
            assert crawled["pages"][0]["url"] == "https://example.com/docs"
            runner.close()
    finally:
        if old_command is None:
            os.environ.pop("OCCAM_WRB_COMMAND", None)
        else:
            os.environ["OCCAM_WRB_COMMAND"] = old_command
        if old_timeout is None:
            os.environ.pop("OCCAM_WRB_TIMEOUT_MS", None)
        else:
            os.environ["OCCAM_WRB_TIMEOUT_MS"] = old_timeout

    print("WRB_OCCAM_RUNNER_SELFTEST_OK")


if __name__ == "__main__":
    main()
