#!/usr/bin/env python3
"""Call a Unity MCP tool over HTTP and print the result (read-only friendly).

Usage:
  python Tools/unity_call.py <tool_name> '<json_arguments>' [--timeout SEC]

Example:
  python Tools/unity_call.py read_console '{"action":"get","types":["error"],"count":20}'
"""
import json
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
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    tool, args = sys.argv[1], json.loads(sys.argv[2])
    timeout = 120
    if '--timeout' in sys.argv:
        timeout = int(sys.argv[sys.argv.index('--timeout') + 1])
    sid, _ = post({'jsonrpc': '2.0', 'id': 1, 'method': 'initialize',
                   'params': {'protocolVersion': '2025-03-26',
                              'capabilities': {},
                              'clientInfo': {'name': 'unity-call',
                                             'version': '1.0'}}}, timeout=timeout)
    post({'jsonrpc': '2.0', 'id': 2,
          'method': 'notifications/initialized'}, sid, timeout)
    _, body = post({'jsonrpc': '2.0', 'id': 3, 'method': 'tools/call',
                    'params': {'name': tool, 'arguments': args}}, sid, timeout)
    print(body)


if __name__ == '__main__':
    sys.exit(main())
