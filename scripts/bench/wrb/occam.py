"""FF-Occam runner adapter for WRB.

The adapter keeps one MCP stdio host alive for the benchmark. Fetch maps to
occam_transcode, search maps to occam_search, and crawl maps to occam_map.
The crawl score therefore measures URL discovery only; it is not presented as
a resumable content-crawl implementation.
"""

from __future__ import annotations

import atexit
from collections import deque
import json
import os
from pathlib import Path
import queue
import shlex
import signal
import subprocess
import threading
import time
from typing import Any

from base import Runner as BaseRunner


def _approx_tokens(text: str) -> int:
    """Use WRB's chars/4 convention so cross-runner scores stay comparable."""
    return max(1, len(text) // 4) if text else 0


class _McpClient:
    def __init__(self) -> None:
        timeout_ms = int(os.environ.get("OCCAM_WRB_TIMEOUT_MS", "90000"))
        self.timeout_s = max(1, timeout_ms) / 1000
        self._next_id = 1
        self._messages: queue.Queue[dict[str, Any]] = queue.Queue()
        self._stderr: deque[str] = deque(maxlen=20)
        self._closed = False

        command_override = os.environ.get("OCCAM_WRB_COMMAND", "").strip()
        if command_override:
            command = shlex.split(command_override)
            cwd = os.environ.get("OCCAM_HOME") or None
        else:
            home = os.environ.get("OCCAM_HOME", "").strip()
            if not home:
                raise RuntimeError(
                    "OCCAM_HOME is required unless OCCAM_WRB_COMMAND is set."
                )
            launcher = Path(home) / "scripts" / "launch-mcp-host.mjs"
            command = [
                os.environ.get("OCCAM_WRB_NODE", "node"),
                str(launcher),
            ]
            cwd = home

        env = {
            **os.environ,
            "OCCAM_BANNER": "0",
            "OCCAM_PROFILE": os.environ.get("OCCAM_PROFILE", "reader"),
            "Logging__LogLevel__Default": "None",
        }
        self._proc = subprocess.Popen(
            command,
            cwd=cwd,
            env=env,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
            bufsize=1,
            start_new_session=os.name != "nt",
        )
        if self._proc.stdin is None or self._proc.stdout is None:
            raise RuntimeError("Could not open MCP stdio pipes.")

        threading.Thread(target=self._read_stdout, daemon=True).start()
        threading.Thread(target=self._read_stderr, daemon=True).start()
        atexit.register(self.close)

        self.request(
            "initialize",
            {
                "protocolVersion": "2024-11-05",
                "capabilities": {},
                "clientInfo": {"name": "wrb-occam", "version": "1.0"},
            },
        )
        self.notify("notifications/initialized")

    def _read_stdout(self) -> None:
        assert self._proc.stdout is not None
        for line in self._proc.stdout:
            try:
                message = json.loads(line)
            except json.JSONDecodeError:
                continue
            if isinstance(message, dict):
                self._messages.put(message)

    def _read_stderr(self) -> None:
        if self._proc.stderr is None:
            return
        for line in self._proc.stderr:
            stripped = line.strip()
            if stripped:
                self._stderr.append(stripped)

    def _send(self, message: dict[str, Any]) -> None:
        if self._closed or self._proc.poll() is not None:
            detail = "; ".join(self._stderr)
            raise RuntimeError(f"Occam MCP host is not running. {detail}".strip())
        assert self._proc.stdin is not None
        self._proc.stdin.write(json.dumps(message, separators=(",", ":")) + "\n")
        self._proc.stdin.flush()

    def notify(self, method: str, params: dict[str, Any] | None = None) -> None:
        self._send(
            {"jsonrpc": "2.0", "method": method, "params": params or {}}
        )

    def request(
        self, method: str, params: dict[str, Any] | None = None
    ) -> dict[str, Any]:
        request_id = self._next_id
        self._next_id += 1
        self._send(
            {
                "jsonrpc": "2.0",
                "id": request_id,
                "method": method,
                "params": params or {},
            }
        )

        deadline = time.monotonic() + self.timeout_s
        while True:
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                raise TimeoutError(f"MCP request timed out: {method}")
            try:
                message = self._messages.get(timeout=remaining)
            except queue.Empty as exc:
                raise TimeoutError(f"MCP request timed out: {method}") from exc
            if message.get("id") != request_id:
                continue
            if message.get("error") is not None:
                raise RuntimeError(json.dumps(message["error"], ensure_ascii=False))
            result = message.get("result")
            return result if isinstance(result, dict) else {}

    def call_tool(self, name: str, arguments: dict[str, Any]) -> dict[str, Any]:
        result = self.request(
            "tools/call", {"name": name, "arguments": arguments}
        )
        structured = result.get("structuredContent")
        if isinstance(structured, dict):
            return structured
        content = result.get("content")
        if isinstance(content, list):
            for item in content:
                if not isinstance(item, dict) or item.get("type") != "text":
                    continue
                text = item.get("text")
                if not isinstance(text, str):
                    continue
                try:
                    parsed = json.loads(text)
                except json.JSONDecodeError:
                    continue
                if isinstance(parsed, dict):
                    return parsed
        return {}

    def close(self) -> None:
        if self._closed:
            return
        self._closed = True
        try:
            if self._proc.stdin is not None:
                self._proc.stdin.close()
        except OSError:
            pass
        try:
            self._proc.wait(timeout=2)
            return
        except subprocess.TimeoutExpired:
            pass
        if os.name == "nt":
            self._proc.terminate()
        else:
            os.killpg(self._proc.pid, signal.SIGTERM)
        try:
            self._proc.wait(timeout=2)
        except subprocess.TimeoutExpired:
            if os.name == "nt":
                self._proc.kill()
            else:
                os.killpg(self._proc.pid, signal.SIGKILL)
            self._proc.wait(timeout=2)


class Runner(BaseRunner):
    """WRB adapter for the shipped Occam MCP surface."""

    name = "FF-Occam"

    def __init__(self) -> None:
        self._client = _McpClient()

    def close(self) -> None:
        self._client.close()

    def fetch(self, url: str, max_chars: int = 5000) -> dict[str, Any]:
        started = time.monotonic()
        try:
            payload = self._client.call_tool(
                "occam_transcode",
                {
                    "url": url,
                    "backend_policy": "http_then_browser",
                    "include_media_refs": False,
                },
            )
            markdown = payload.get("markdown", "")
            content = markdown[:max_chars] if isinstance(markdown, str) else ""
            return {
                "success": payload.get("ok") is True,
                "content": content,
                "latency_ms": round((time.monotonic() - started) * 1000),
                "tokens": _approx_tokens(content),
                "tier": payload.get("backend", "unknown"),
            }
        except Exception as exc:
            return {
                "success": False,
                "content": "",
                "latency_ms": round((time.monotonic() - started) * 1000),
                "tokens": 0,
                "tier": "error",
                "error": str(exc),
            }

    def search(self, query: str, max_results: int = 10) -> dict[str, Any]:
        started = time.monotonic()
        try:
            payload = self._client.call_tool(
                "occam_search",
                {"query": query, "max_results": max_results},
            )
            raw_results = payload.get("results", [])
            results = [
                {
                    "title": str(item.get("title", "")),
                    "url": str(item.get("url", "")),
                    "snippet": str(item.get("snippet", "")),
                }
                for item in raw_results
                if isinstance(item, dict)
            ][:max_results]
            rendered = "\n".join(
                f"{item['title']} {item['url']} {item['snippet']}"
                for item in results
            )
            return {
                "success": payload.get("ok") is True,
                "results": results,
                "latency_ms": round((time.monotonic() - started) * 1000),
                "tokens": _approx_tokens(rendered),
            }
        except Exception as exc:
            return {
                "success": False,
                "results": [],
                "latency_ms": round((time.monotonic() - started) * 1000),
                "tokens": 0,
                "error": str(exc),
            }

    def crawl(
        self, seed_url: str, focus: str, max_pages: int = 30
    ) -> dict[str, Any]:
        """Score Occam's current map capability without claiming content crawl."""
        started = time.monotonic()
        max_links = max(1, min(max_pages, 64))
        try:
            arguments = {
                "url": seed_url,
                "source": "sitemap",
                "max_links": max_links,
                "same_domain": True,
                "focus_query": focus,
                "timeout_ms": 30000,
            }
            payload = self._client.call_tool("occam_map", arguments)
            if payload.get("ok") is not True:
                arguments["source"] = "homepage"
                payload = self._client.call_tool("occam_map", arguments)

            raw_links = payload.get("links", [])
            pages = [
                {
                    "url": str(item.get("url", "")),
                    "title": str(item.get("title", "")),
                    "content": "",
                }
                for item in raw_links
                if isinstance(item, dict) and item.get("url")
            ][:max_links]
            rendered = "\n".join(
                f"{item['title']} {item['url']}" for item in pages
            )
            return {
                "success": payload.get("ok") is True,
                "pages": pages,
                "latency_ms": round((time.monotonic() - started) * 1000),
                "tokens": _approx_tokens(rendered),
                "mode": "occam_map_proxy",
            }
        except Exception as exc:
            return {
                "success": False,
                "pages": [],
                "latency_ms": round((time.monotonic() - started) * 1000),
                "tokens": 0,
                "mode": "occam_map_proxy",
                "error": str(exc),
            }
