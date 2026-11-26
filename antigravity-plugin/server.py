"""Minimal MCP stdio server that serves the Roxy intro animation as an MCP App.

No third-party dependencies: speaks newline-delimited JSON-RPC 2.0 over stdio.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
HTML_PATH = os.environ.get("ROXY_INTRO_HTML") or os.path.join(HERE, "intro.html")
APP_URI = "ui://roxy-intro/intro.html"
MIME = "text/html;profile=mcp-app"

TOOL_META = {
    "ui": {"resourceUri": APP_URI},
    "com.google.antigravity/ui": {
        "entrypoints": [
            {"type": "aux_pane", "title": "洛琪希开场"},
            {"type": "global", "title": "洛琪希开场"},
        ]
    },
}

TOOLS = [
    {
        "name": "open_roxy_intro",
        "title": "洛琪希开场动画",
        "description": "Play the Roxy-themed opening animation (starry sky, water magic circle, "
        "blue-haired mage) that ends with the greeting '欢迎回来'. Takes no arguments.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
        "_meta": TOOL_META,
    }
]


def read_html():
    with open(HTML_PATH, "r", encoding="utf-8") as f:
        return f.read()


def handle(method, params):
    if method == "initialize":
        return {
            "protocolVersion": params.get("protocolVersion") or "2025-06-18",
            "capabilities": {"tools": {}, "resources": {}},
            "serverInfo": {"name": "roxy-intro", "version": "1.0.0"},
        }
    if method == "ping":
        return {}
    if method == "tools/list":
        return {"tools": TOOLS}
    if method == "tools/call":
        if params.get("name") != "open_roxy_intro":
            raise LookupError("Unknown tool: %s" % params.get("name"))
        return {
            "content": [{"type": "text", "text": "已打开洛琪希开场动画（欢迎回来）。"}],
            "structuredContent": {"greeting": "欢迎回来"},
        }
    if method == "resources/list":
        return {
            "resources": [
                {
                    "uri": APP_URI,
                    "name": "洛琪希开场动画",
                    "mimeType": MIME,
                    "_meta": {"ui": {"prefersBorder": False}},
                }
            ]
        }
    if method == "resources/templates/list":
        return {"resourceTemplates": []}
    if method == "resources/read":
        if params.get("uri") != APP_URI:
            raise LookupError("Unknown resource: %s" % params.get("uri"))
        return {
            "contents": [
                {
                    "uri": APP_URI,
                    "mimeType": MIME,
                    "text": read_html(),
                    "_meta": {"ui": {"prefersBorder": False}},
                }
            ]
        }
    if method == "prompts/list":
        return {"prompts": []}
    raise NotImplementedError(method)


def send(obj):
    sys.stdout.write(json.dumps(obj, ensure_ascii=True) + "\n")
    sys.stdout.flush()


def main():
    for raw in sys.stdin.buffer:
        line = raw.decode("utf-8", "replace").strip()
        if not line:
            continue
        try:
            msg = json.loads(line)
        except ValueError:
            send({"jsonrpc": "2.0", "id": None, "error": {"code": -32700, "message": "Parse error"}})
            continue
        if "id" not in msg or "method" not in msg:
            continue  # notification or response: nothing to do
        mid = msg["id"]
        try:
            result = handle(msg["method"], msg.get("params") or {})
            send({"jsonrpc": "2.0", "id": mid, "result": result})
        except NotImplementedError as e:
            send({"jsonrpc": "2.0", "id": mid, "error": {"code": -32601, "message": "Method not found: %s" % e}})
        except LookupError as e:
            send({"jsonrpc": "2.0", "id": mid, "error": {"code": -32602, "message": str(e)}})
        except Exception as e:  # noqa: BLE001
            send({"jsonrpc": "2.0", "id": mid, "error": {"code": -32603, "message": str(e)}})


if __name__ == "__main__":
    main()
