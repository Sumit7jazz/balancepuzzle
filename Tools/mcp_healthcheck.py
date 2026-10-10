#!/usr/bin/env python3
"""Read-only MCP health check for the Balance Puzzle toolchain (stdlib only).

Checks, without modifying anything:
  1. Unity Editor process running (Unity.exe).
  2. Unity MCP relay listening on 127.0.0.1:8080 (TCP connect).
  3. Unity MCP relay handshake (initialize + tools/list) + version.
  4. Blender process running (blender.exe).
  5. Blender MCP addon listening on 127.0.0.1:9876 (TCP connect).
  6. Blender MCP *live query*: spawns a transient stdio MCP client
     (uvx mcp-for-blender, same command the configured blenderMCP server
     uses) and performs initialize + a read-only get_addon_status call.
     This proves end-to-end responsiveness, not just an open port.

Distinguishes: editor closed vs relay down vs bridge broken vs
Blender listener-open-but-unresponsive.
Exit code: 0 = all green, 1 = warnings only, 2 = a failure.

Usage:
  python Tools/mcp_healthcheck.py [--unity-url URL] [--blender-port N]
"""
import collections
import json
import os
import queue
import re
import shutil
import socket
import subprocess
import sys
import threading
import time
import urllib.request

UNITY_URL = 'http://127.0.0.1:8080/mcp'
BLENDER_HOST = '127.0.0.1'
BLENDER_PORT = 9876
TIMEOUT = 12
LIVE_STEP_TIMEOUT = 60  # per MCP round trip (uvx cold start can be slow)
LIVE_TOTAL_TIMEOUT = 240  # overall cap for the Blender live query
UVX_PATH = r'C:\Users\balis\.local\bin\uvx.exe'

# Live Blender probe: known zero-arg read-only tool (verified read-only).


def proc_names():
    try:
        out = subprocess.run(['tasklist'], capture_output=True, text=True,
                             timeout=30).stdout.lower()
    except Exception as e:
        return None, 'tasklist failed: %r' % e
    names = set(re.findall(r'^([\w.+-]+\.exe)', out, re.M))
    return names, None


def tcp_open(host, port):
    s = socket.socket()
    s.settimeout(TIMEOUT)
    try:
        s.connect((host, port))
        return True, ''
    except Exception as e:
        return False, repr(e)
    finally:
        s.close()


def unity_handshake(url):
    """Full MCP handshake: initialize -> tools/list. Returns (ok, detail)."""
    def post(payload, sid=None):
        req = urllib.request.Request(
            url, data=json.dumps(payload).encode(),
            headers={'Content-Type': 'application/json',
                     'Accept': 'application/json, text/event-stream'})
        if sid:
            req.add_header('Mcp-Session-Id', sid)
        with urllib.request.urlopen(req, timeout=TIMEOUT) as r:
            return r.headers.get('Mcp-Session-Id'), r.read().decode()

    sid, _ = post({'jsonrpc': '2.0', 'id': 1, 'method': 'initialize',
                   'params': {'protocolVersion': '2025-03-26',
                              'capabilities': {},
                              'clientInfo': {'name': 'mcp-healthcheck',
                                             'version': '1.0'}}})
    if not sid:
        return False, 'initialize returned no session id'
    post({'jsonrpc': '2.0', 'id': 2,
          'method': 'notifications/initialized'}, sid)
    _, body = post({'jsonrpc': '2.0', 'id': 3, 'method': 'tools/list',
                    'params': {}}, sid)
    m = re.search(r'data: (\{.*\})', body, re.S)
    if not m:
        return False, 'no SSE data frame in tools/list response'
    tools = json.loads(m.group(1))['result']['tools']
    return True, '%d tools' % len(tools)


def blender_live_query():
    """End-to-end Blender MCP check via a transient stdio client.

    Returns (ok, detail). Spawns only its own child process (killed on exit),
    sends read-only calls, modifies nothing.
    """
    uvx = UVX_PATH if os.path.exists(UVX_PATH) else shutil.which('uvx')
    if not uvx:
        return False, 'uvx client not found; cannot run live query'
    env = dict(os.environ, BLENDER_HOST='localhost',
               BLENDER_PORT=str(BLENDER_PORT))
    try:
        proc = subprocess.Popen(
            [uvx, 'mcp-for-blender'], stdin=subprocess.PIPE,
            stdout=subprocess.PIPE, stderr=subprocess.PIPE,
            text=True, bufsize=1, env=env)
    except Exception as e:
        return False, 'could not spawn client: %r' % e

    lines = queue.Queue()
    errlines = collections.deque(maxlen=20)

    def reader():
        try:
            for line in proc.stdout:
                lines.put(line)
        except Exception:
            pass

    def errdrain():
        try:
            for line in proc.stderr:
                errlines.append(line.strip())
        except Exception:
            pass

    rthread = threading.Thread(target=reader, daemon=True)
    rthread.start()
    threading.Thread(target=errdrain, daemon=True).start()
    start = time.time()

    def send(payload):
        proc.stdin.write(json.dumps(payload) + '\n')
        proc.stdin.flush()

    def rpc(method, params=None, call_id=1):
        t0 = time.time()
        payload = {'jsonrpc': '2.0', 'id': call_id, 'method': method}
        if params is not None:
            payload['params'] = params
        send(payload)
        while time.time() - t0 < LIVE_STEP_TIMEOUT:
            if time.time() - start > LIVE_TOTAL_TIMEOUT:
                raise TimeoutError('overall live-check budget exceeded at %s'
                                   % method)
            try:
                line = lines.get(timeout=1)
            except queue.Empty:
                if not rthread.is_alive():
                    raise RuntimeError('stdout reader died at %s' % method)
                if proc.poll() is not None:
                    raise RuntimeError('client exited early at %s' % method)
                continue
            try:
                msg = json.loads(line)
            except ValueError:
                continue  # non-JSON log line; keep waiting
            if isinstance(msg, dict) and msg.get('id') == call_id:
                if 'error' in msg:
                    raise RuntimeError('MCP error at %s: %s'
                                       % (method, msg['error']))
                return msg.get('result', {})
        raise TimeoutError('timed out waiting for %s (%.0fs)'
                           % (method, time.time() - t0))

    def stderr_tail():
        tail = [l for l in errlines][-5:]
        return ' / '.join(tail)[:500] if tail else ''

    try:
        # Pipeline initialize + a known zero-arg read-only call immediately
        # (get_addon_status is proven read-only); then wait for the reply.
        send({'jsonrpc': '2.0', 'id': 1, 'method': 'initialize',
              'params': {'protocolVersion': '2025-03-26',
                         'capabilities': {},
                         'clientInfo': {'name': 'mcp-healthcheck',
                                        'version': '1.0'}}})
        send({'jsonrpc': '2.0', 'method': 'notifications/initialized'})
        res = rpc('tools/call',
                  {'name': 'get_addon_status', 'arguments': {}}, 4)
        content = (res.get('content') or [{}])[0].get('text', '')
        if 'blender_version' not in content and 'addon_version' not in content:
            return False, 'unexpected live reply: %s' % content[:200]
        return True, 'live get_addon_status OK'
    except Exception as e:
        detail = 'live query failed: %r' % e
        tail = stderr_tail()
        if tail:
            detail += ' | client stderr: %s' % tail
        return False, detail
    finally:
        try:
            proc.kill()
        except Exception:
            pass


def main():
    url = UNITY_URL
    bport = BLENDER_PORT
    args = sys.argv[1:]
    for i, a in enumerate(args):
        if a == '--unity-url' and i + 1 < len(args):
            url = args[i + 1]
        if a == '--blender-port' and i + 1 < len(args):
            bport = int(args[i + 1])

    fails, warns = [], []
    procs, err = proc_names()
    if procs is None:
        fails.append('processes: ' + err)
        procs = set()

    # --- Unity chain ---
    if 'unity.exe' in procs:
        print('[PASS] Unity Editor process running')
    else:
        fails.append('Unity Editor NOT running (Unity.exe absent) -> open the project via Unity Hub')

    relay_open, relay_detail = tcp_open('127.0.0.1', 8080)
    if relay_open:
        print('[PASS] Unity MCP relay listening on 127.0.0.1:8080')
    else:
        fails.append('relay port 8080 refused (%s) -> %s'
                     % (relay_detail, 'start relay via Unity Editor (MCPForUnity package), editor must be open'
                        if 'unity.exe' in procs else 'open Unity first'))

    if relay_open:
        try:
            good, detail = unity_handshake(url)
            if good:
                print('[PASS] Unity MCP handshake OK (%s)' % detail)
            else:
                fails.append('relay handshake broken: ' + detail)
        except Exception as e:
            fails.append('relay handshake error: %r' % e)

    # --- Blender chain ---
    if 'blender.exe' in procs:
        print('[PASS] Blender process running')
    else:
        warns.append('Blender NOT running (blender.exe absent) -> start Blender + Start MCP Server')

    port_open, port_detail = tcp_open(BLENDER_HOST, bport)
    if port_open:
        print('[PASS] Blender MCP addon listening on %s:%d'
              % (BLENDER_HOST, bport))
    else:
        (fails if 'blender.exe' in procs else warns).append(
            'Blender addon port %d refused (%s)' % (bport, port_detail))

    # End-to-end responsiveness, not just process+port.
    if port_open:
        print('[...] Blender MCP live query (transient client, up to ~%ds)...'
              % LIVE_TOTAL_TIMEOUT)
        good, detail = blender_live_query()
        if good:
            print('[PASS] Blender MCP responsive (%s)' % detail)
        else:
            warns.append('Blender port open but unresponsive: %s' % detail)
    else:
        warns.append('Blender live query skipped (port closed)')

    for w in warns:
        print('[WARN]', w)
    for f in fails:
        print('[FAIL]', f)
    if fails:
        print('RESULT: FAIL')
        return 2
    if warns:
        print('RESULT: OK with warnings')
        return 1
    print('RESULT: ALL GREEN')
    return 0


if __name__ == '__main__':
    sys.exit(main())
