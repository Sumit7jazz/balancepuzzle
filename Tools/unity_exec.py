#!/usr/bin/env python3
"""Post a C# snippet file to Unity MCP execute_code over HTTP (stdlib only).

Usage:
  python Tools/unity_exec.py <snippet.cs> [--timeout SEC]

Reads the whole snippet file as the `code` argument (action=execute),
so shell quoting can never corrupt string literals. Prints the
result text payload (or the raw error text on failure).
"""
import json
import re
import sys
import urllib.request

URL = 'http://127.0.0.1:8080/mcp'


def post(payload, sid=None, timeout=120):
    req = urllib.request.Request(
        URL, data=json.dumps(payload).encode(),
        headers={'Content-Type': 'application/json',
                 'Accept': 'application/json, text/event-stream'})
    if sid:
        req.add_header('Mcp-Session-Id', sid)
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return r.headers.get('Mcp-Session-Id'), r.read().decode()


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    code = open(sys.argv[1]).read()
    timeout = 120
    if '--timeout' in sys.argv:
        timeout = int(sys.argv[sys.argv.index('--timeout') + 1])
    sid, _ = post({'jsonrpc': '2.0', 'id': 1, 'method': 'initialize',
                   'params': {'protocolVersion': '2025-03-26',
                              'capabilities': {},
                              'clientInfo': {'name': 'unity-exec',
                                             'version': '1.0'}}}, timeout=timeout)
    post({'jsonrpc': '2.0', 'id': 2,
          'method': 'notifications/initialized'}, sid, timeout)
    _, body = post({'jsonrpc': '2.0', 'id': 3, 'method': 'tools/call',
                    'params': {'name': 'execute_code',
                               'arguments': {'action': 'execute',
                                             'code': code}}}, sid, timeout)
    m = re.search(r'data: (\{.*\})', body, re.S)
    if not m:
        print(body)
        return 1
    d = json.loads(m.group(1))
    print(d['result']['content'][0]['text'])
    return 0


if __name__ == '__main__':
    sys.exit(main())
