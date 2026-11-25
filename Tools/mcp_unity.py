#!/usr/bin/env python3
"""Minimal JSON-RPC client for the local "MCP for Unity" HTTP server.

The Unity package (com.coplaydev.unity-mcp) launches
``mcp-for-unity --transport http --http-url http://127.0.0.1:8080`` and the
Unity Editor connects to it as a WebSocket client.  This script talks the
same Streamable-HTTP MCP endpoint so tools can be invoked from a plain
terminal without an MCP host.

Commands
--------
tools
    List every tool the server exposes (name + first description line).
schema <tool>
    Print the full JSON schema of one tool.
call <tool> [json-args]
    Invoke a tool and print the textual result.
raw <method> [json-params]
    Send an arbitrary JSON-RPC method (``initialize``, ``resources/list``...).
"""

from __future__ import annotations

import json
import sys
import urllib.error
import urllib.request

BASE_URL = "http://127.0.0.1:8080"
MCP_URL = BASE_URL + "/mcp"
PROTOCOL_VERSION = "2025-03-26"
CLIENT_INFO = {"name": "drone-simulator-tools", "version": "1.0"}


class McpError(RuntimeError):
    pass


class McpClient:
    def __init__(self, url: str = MCP_URL) -> None:
        self.url = url
        self.session_id = None
        self._next_id = 0

    # -- transport ---------------------------------------------------------
    def _post(self, payload):
        body = json.dumps(payload).encode("utf-8")
        headers = {
            "Content-Type": "application/json",
            "Accept": "application/json, text/event-stream",
            "User-Agent": "drone-simulator-tools/1.0",
        }
        if self.session_id:
            headers["Mcp-Session-Id"] = self.session_id
        request = urllib.request.Request(self.url, data=body, headers=headers, method="POST")
        try:
            with urllib.request.urlopen(request, timeout=180) as response:
                session = response.headers.get("Mcp-Session-Id")
                if session:
                    self.session_id = session
                raw = response.read().decode("utf-8", "replace")
                content_type = response.headers.get("Content-Type", "")
        except urllib.error.HTTPError as exc:
            detail = exc.read().decode("utf-8", "replace")[:2000]
            raise McpError("HTTP {0} {1}: {2}".format(exc.code, exc.reason, detail)) from exc
        except urllib.error.URLError as exc:
            raise McpError("cannot reach {0}: {1}".format(self.url, exc.reason)) from exc

        if not raw.strip():
            return None, {"content-type": content_type, "session": self.session_id}
        if "text/event-stream" in content_type or raw.lstrip().startswith("event:"):
            return _parse_sse(raw), {"content-type": content_type, "session": self.session_id}
        return json.loads(raw), {"content-type": content_type, "session": self.session_id}

    # -- MCP methods -------------------------------------------------------
    def initialize(self):
        payload = {
            "jsonrpc": "2.0",
            "id": self._id(),
            "method": "initialize",
            "params": {
                "protocolVersion": PROTOCOL_VERSION,
                "capabilities": {},
                "clientInfo": CLIENT_INFO,
            },
        }
        result, _meta = self._post(payload)
        self._notify("notifications/initialized")
        return result or {}

    def _notify(self, method, params=None):
        payload = {"jsonrpc": "2.0", "method": method}
        if params is not None:
            payload["params"] = params
        try:
            self._post(payload)
        except McpError:
            # notifications may not get a reply body; ignore transport noise
            pass

    def _id(self):
        self._next_id += 1
        return self._next_id

    def request(self, method, params=None):
        payload = {"jsonrpc": "2.0", "id": self._id(), "method": method}
        if params is not None:
            payload["params"] = params
        result, _meta = self._post(payload)
        if result is None:
            raise McpError("empty reply for {0}".format(method))
        if "error" in result:
            raise McpError(json.dumps(result["error"], ensure_ascii=False))
        return result.get("result") or {}

    def call_tool(self, name, arguments=None):
        return self.request("tools/call", {"name": name, "arguments": arguments or {}})


def _parse_sse(raw):
    """Returns the last JSON-RPC message from an SSE body."""
    message = None
    for line in raw.splitlines():
        line = line.strip()
        if not line.startswith("data:"):
            continue
        chunk = line[5:].strip()
        if not chunk or chunk == "[DONE]":
            continue
        try:
            parsed = json.loads(chunk)
        except json.JSONDecodeError:
            continue
        if isinstance(parsed, dict) and ("result" in parsed or "error" in parsed):
            message = parsed
    return message


def _print_tool_result(result):
    content = result.get("content") or []
    printed = False
    for item in content:
        if isinstance(item, dict) and item.get("type") == "text":
            print(item.get("text", ""))
            printed = True
        elif isinstance(item, dict):
            print(json.dumps(item, ensure_ascii=False, indent=2))
            printed = True
    if not printed:
        print(json.dumps(result, ensure_ascii=False, indent=2))
    if result.get("isError"):
        raise SystemExit(3)


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 2
    command = argv[1].lower()
    client = McpClient()
    try:
        client.initialize()
        if command == "tools":
            result = client.request("tools/list")
            tools = result.get("tools") or []
            for tool in tools:
                description = (tool.get("description") or "").strip().splitlines()
                first = description[0] if description else ""
                print("{0:<32} {1}".format(tool.get("name", "?"), first[:120]))
            print("\n{0} tool(s)".format(len(tools)))
        elif command == "schema":
            result = client.request("tools/list")
            wanted = argv[2]
            for tool in result.get("tools") or []:
                if tool.get("name") == wanted:
                    print(json.dumps(tool, ensure_ascii=False, indent=2))
                    break
            else:
                print("tool not found: {0}".format(wanted), file=sys.stderr)
                return 4
        elif command == "call":
            if len(argv) < 3:
                print("usage: call <tool> [json]", file=sys.stderr)
                return 2
            arguments = json.loads(argv[3]) if len(argv) > 3 else {}
            _print_tool_result(client.call_tool(argv[2], arguments))
        elif command == "raw":
            params = json.loads(argv[3]) if len(argv) > 3 else None
            print(json.dumps(client.request(argv[2], params), ensure_ascii=False, indent=2))
        else:
            print("unknown command: {0}".format(command), file=sys.stderr)
            return 2
    except McpError as exc:
        print("mcp error: {0}".format(exc), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
