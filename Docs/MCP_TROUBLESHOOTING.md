# MCP Troubleshooting — Balance Puzzle toolchain

State: **MCP CONNECTIVITY RESTORED — RESTART RESILIENCE NOT YET VERIFIED.**
(Live-verified 2026-10-09. Restarting Claude Code / Unity to prove reconnect
resilience has NOT been tested — do not claim it until it is.)
Related: Stage 1 work is untouched by this document (diagnostics only, no game changes).

## Verified working configuration

| Link | Type | Endpoint / command | Verified by |
|---|---|---|---|
| Claude Code → Blender MCP | stdio | `uvx.exe mcp-for-blender` (global `mcpServers.blenderMCP` in `~/.claude.json`, `BLENDER_HOST=localhost`, `BLENDER_PORT=9876`) | live `get_addon_status` (addon 1.8, Blender 5.2.2 LTS) + live scene query |
| Blender addon → Blender | TCP | `127.0.0.1:9876` (Blender MCP addon, "Start MCP Server") | `netstat` LISTENING on 9876 + established session |
| Claude Code → Unity MCP relay | HTTP (streamable) | `http://127.0.0.1:8080/mcp` (project `mcpServers.UnityMCP` in `~/.claude.json` for `E:/GAME`, `E:/GAME/balancepuzzle`) | live `initialize` + `tools/list` (50 tools, `mcp-for-unity-server` 3.4.8) |
| Unity MCP relay → Unity Editor | relay | `mcp-for-unity.exe --transport http --http-url http://127.0.0.1:8080 --pidfile Library/MCPForUnity/RunState/mcp_http_8080.pid` → in-Editor `unity-ai-relay` (ports 9001/9002) | live `read_console` tool call returned Editor console text |

Full chain (per process tree at verification):

```text
claude.exe ─┬─ uvx.exe ─ python.exe (mcp-for-blender) ─TCP 9876→ blender.exe
            └─ (earlier session's cmd.exe) ─ uvx.exe ─ python.exe (mcp-for-unity, :8080)
                                                          ↕ relay ports 9001/9002
                                              Unity.exe (balancepuzzle) ─┬─ AssetImportWorkerHW1
                                                                         └─ AssetImportWorkerHW2
```

Source of truth for endpoints: `~/.claude.json` (`mcpServers` global + per-project
`projects["E:/GAME"].mcpServers` / `projects["E:/GAME/balancepuzzle"].mcpServers`).
There are no project-level `.mcp.json` files; nothing else configures these servers.

## Prerequisites and startup order

1. Open the project with Unity Hub → `E:/GAME/balancepuzzle` (Editor 6000.6.4f1).
   The MCPForUnity package starts the in-Editor relay and spawns the HTTP bridge
   (`mcp_http_8080.pid` appears under `Library/MCPForUnity/RunState/`).
2. Open Blender → enable *Interface: Blender MCP* → **Start MCP Server** (port 9876).
3. Start Claude Code in `E:/GAME`. Both `claude mcp list` entries should show Connected
   (**list status alone is not proof** — run the health check below).

## Repeatable health check

```bash
cd E:/GAME/balancepuzzle
python Tools/mcp_healthcheck.py
```

Expected (healthy):

```text
[PASS] Unity Editor process running
[PASS] Unity MCP relay listening on 127.0.0.1:8080
[PASS] Unity MCP handshake OK (50 tools)
[PASS] Blender process running
[PASS] Blender MCP addon listening on 127.0.0.1:9876
[...] Blender MCP live query (transient client, up to ~240s)...
[PASS] Blender MCP responsive (live get_addon_status OK)
RESULT: ALL GREEN
```

Check levels (what each line actually proves):

- *process running* — the OS process exists. Says nothing about MCP.
- *listening* — a TCP listener accepts on the port. Says nothing about protocol.
- *handshake OK / responsive* — a full MCP round trip succeeded
  (Unity: initialize → tools/list; Blender: initialize → read-only
  `get_addon_status` over a transient stdio client).
  **Only these lines prove MCP responsiveness.**
- The Blender live query spawns a transient `uvx mcp-for-blender` stdio client
  (the same command as the configured server), runs read-only calls, then kills
  its own child. It installs nothing, edits nothing, and never touches the
  running session's MCP servers.
- `Blender port open but unresponsive` (WARN) means the addon listener accepts
  TCP but the MCP round trip failed — treat as addon/Blender-side fault, restart
  Blender's MCP Server rather than the whole machine.

Deeper live probes (harmless, read-only) are documented in this file's history via
`curl` MCP handshakes; the script covers the standard cases.

## Error-specific recovery

| Symptom | Meaning | Recovery |
|---|---|---|
| `Unity Editor NOT running` | Editor closed | Open project via Unity Hub, wait for import to finish, re-run check |
| `relay port 8080 refused` + Editor running | In-Editor relay/bridge not started | Wait 1–2 min (first run installs deps), check Editor console for `MCP-FOR-UNITY: Server ready on http://127.0.0.1:8080`; restart Editor if absent |
| `relay handshake broken/error` | Bridge up but MCP protocol failing | Restart Editor; do **not** start a second `mcp-for-unity` manually (pidfile pins one instance) |
| `Blender NOT running` (WARN) | Blender closed | Start Blender, Start MCP Server; safe to continue Unity-only work |
| `Blender addon port refused` + Blender running | Addon server off | In Blender: re-enable addon / Start MCP Server |
| Harness reports `ECONNREFUSED` for UnityMCP but check is green | Stale session-start snapshot | Trust the live check; harness state was captured before the relay listened |

## Safe retry limits — when to stop

- Health script: at most **3 runs, 30 s apart** (covers Editor/relay startup races).
- If 8080 still refuses with the Editor open after 3 tries → read the Editor console
  (`read_console` won't work; look in `Logs/Editor.log` + relay log lines) instead of
  restarting things repeatedly.
- Never start duplicate relays/servers to "fix" a port refuse; one bridge per project
  (enforced by the pidfile). Extra `Unity.exe` processes beyond the main editor +
  `AssetImportWorkerHW*` children, or extra `mcp-for-unity` `python.exe` instances,
  warrant investigation before any restart.
- Do not kill processes, edit `~/.claude.json`, reinstall packages, or reinstall
  Claude Code without evidence pointing at that component.

## What can be checked without Unity running

- `claude --version`, `claude doctor`, `claude mcp list` (config-level only).
- Blender chain end-to-end (Blender MCP is independent of Unity).
- `Tools/mcp_healthcheck.py` still runs; expect Unity FAIL lines (this is the
  designed "editor stopped" signature, not a broken server).

## What requires Unity Editor open

- Anything on port 8080 (relay listen, handshake, tool calls, console reads).
- Unity-side verification (imports, health checks, Play Mode).

## Known limitations / manual approval boundaries

- Unity MCP tool availability inside a Claude session reflects session-start state;
  a relay that starts *after* the session began is still fully usable over HTTP
  (as proven here) but may not appear as harness tools until session restart.
- Entering Play Mode, importing/deleting assets, running tests, and any
  project modification via MCP tools require explicit approval per action.
- `ANTHROPIC_BASE_URL` is customized in this environment, so Remote Control /
  api.anthropic.com features (per `claude doctor`) are unavailable — unrelated to MCP.

## Separately diagnosed (not MCP issues)

- **Claude Code auto-update `claude.exe in use`** (2026-10-08, `update_apply_exe_locked`,
  2.1.294→2.1.295): the running version is now **2.1.295**, so the update completed
  on a later attempt/restart. Transient file-lock, currently resolved. No action.
- **Unity render-state/bounds anomaly**: untouched by this checkpoint; needs its own
  runtime verification (reconnecting MCP does not fix it).
- **Many `TIME_WAIT`s toward 127.0.0.1:8080**: normal short-lived MCP polling churn,
  not an error.
