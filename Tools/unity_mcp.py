"""Local Streamable HTTP client for the project's existing Unity MCP server."""
import json
import sys
import urllib.request
from pathlib import Path

URL = "http://127.0.0.1:8099/mcp"
session = None

def request(method, params=None, notification=False):
    global session
    body = {"jsonrpc": "2.0", "method": method}
    if not notification:
        body["id"] = 1
    if params is not None:
        body["params"] = params
    headers = {"Content-Type": "application/json", "Accept": "application/json, text/event-stream"}
    if session:
        headers["Mcp-Session-Id"] = session
    req = urllib.request.Request(URL, json.dumps(body).encode(), headers)
    with urllib.request.urlopen(req, timeout=180) as response:
        session = response.headers.get("Mcp-Session-Id", session)
        raw = response.read().decode()
    if not raw:
        return None
    if any(line.startswith("data:") for line in raw.splitlines()):
        messages = [json.loads(line[5:].strip()) for line in raw.splitlines() if line.startswith("data:")]
        return messages[-1] if messages else raw
    return json.loads(raw)

request("initialize", {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "loongdum-scene-authoring", "version": "1.0"}})
request("notifications/initialized", notification=True)
method = sys.argv[1]
argument = sys.argv[2] if len(sys.argv) > 2 else None
params = json.loads(Path(argument[1:]).read_text(encoding="utf-8") if argument and argument.startswith("@") else argument) if argument else None
if method == "schema":
    result = [t for t in request("tools/list")["result"]["tools"] if t["name"] in params]
else:
    result = request(method, params)
def compact(value):
    if isinstance(value, dict):
        if value.get("type") == "image":
            return {"type": "image", "mimeType": value.get("mimeType"), "omitted": True}
        if "structuredContent" in value:
            return compact(value["structuredContent"])
        return {k: compact(v) for k,v in value.items()}
    if isinstance(value, list):
        return [compact(v) for v in value]
    return value
print(json.dumps(compact(result), ensure_ascii=True))
