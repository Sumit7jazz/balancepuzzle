# STAGE 2 ART FOUNDATION — Milestone Log (2026-10-09, autonomous session)

Project: `balancepuzzle` (Unity 6000.6.4f1, URP 17.6.0). Scene: `Assets/Scenes/Stage1_Sandbox.unity`.
Method: live Unity MCP/HTTP (`Tools/unity_call.py`, `Tools/unity_exec.py`) + headless Blender 5.2 bpy.
Evidence standard: no PASS claimed without executed check + inspected output.

## Baseline (Phase 1, all executed)

- `Tools/mcp_healthcheck.py`: ALL GREEN, exit 0 (Unity Editor + relay :8080/50 tools, Blender + :9876 live).
- Console (`read_console` + `Logs/Editor.log`): no asset/script/material/render/physics errors.
  Only benign: licensing 404s, relay lifecycle, Unity-AI-assistant connection failure, Account-API notice.
- Disk gates (`Tools/fbx_inspect.py`): Stone A/B/C/D (320/340/332/432 tris) + old altar (480 tris)
  ALL PASS (manifold, UVs, normals, centered, ≤800 tris).
- `Tools/check_stage1_regression.py`: 4 false FAILs on BUG-003 (script compared `'1'` to `"1.0"`;
  substance PASS). Fixed numeric comparison → **ALL STATIC CHECKS PASS, exit 0**.
- Stage 1 Health Check (live menu): **0 FAIL, 0 WARN** (before and after all changes).
- `StoneVisibilityProbeTEMP`: stones/platform origin-pile (bounds=(0,0,0)/(1,1,1)) NO LONGER
  reproduces for imported meshes; persisted only for built-in-Cube users (spawn pads) — investigated
  below. Probe kept until fix verified, then removed (see §Cleanup).

## Mesh import pipeline (Phase 2) — findings

- All 5 requested FBXs import as `ModelImporter` with mesh sub-assets, correct verts/tris/bounds/UVs/normals.
- **Raw `make_rock.py` FBX output is NOT reliably imported by Unity 6000.6**: regenerated altar in raw
  format produced `ImportFBX Errors: ... File is corrupted` + missing mesh sub-asset (scene ref → NULL).
  Recovery: restored from `Tools/backup/`, re-exported via Blender (sanctioned path). No data loss.
- **Blender-exported FBX needs `globalScale: 100` in its `.meta`** (with `useFileUnits: 1`): Unity
  auto-generates `globalScale: 1` for new files → mesh imports at 0.01 scale. Fixed in
  `Assets/Art/Platform/SpawnPad_Slab.fbx.meta` (matches proven `make_rock` sidecar convention).
- `Tools/make_rock.py`: added `unit_altar_tris()` (unit convention, shared with `clone_fbx.py`);
  raw-PLATFORM branch now documents that it is generator-only, not Unity-verified.
- New helpers: `Tools/unity_exec.py` (quoting-safe C# posting), `Tools/blender_unit_altar.py`,
  `Tools/blender_pad_slab.py` (headless bpy exports with in-script gates).

## Stones (Phase 3) — validated, not regenerated

Existing assets satisfy every requirement; no regeneration (per milestone rule).
A(320) compact rounded boulder; B(340) elongated X-ridge; C(332) flat slab; D(432) craggy block.
Unit bbox × config Visual scales; origins centered; 0 colliders/RBs on Visuals; warm-grey URP/Lit
palette (A .78/.74/.67 B .72/.68/.62 C .80/.77/.71 D .66/.62/.57); shadows on; 1 draw call each.

## Platform / pads (Phase 4) — integrated

- `Platform_Altar` (Blender unit export, 480 tris, mat 0.70/0.69/0.66): assigned to Platform
  MeshFilter + material. Transform (0,-0.25,0)/(6,0.5,6), BoxCollider (1,1,1)+Platform_PhysMat
  untouched → world footprint exactly (6,0.5,6). `Platform.prefab` synced (visual only), scene saved.
- **Built-in-Cube identity-matrix anomaly**: spawn-pad renderers (built-in Cube) rendered a unit cube
  at the origin ("dark cube" on altar, all pad bounds=(0,0,0)/(1,1,1)) instead of at their transforms.
  Isolated by hide-each-renderer + mesh-swap test (imported mesh on a pad → bounds instantly correct).
  Fix: new `SpawnPad_Slab` (Blender beveled unit box, 108 tris, triangulated) assigned to all 4 pads;
  pads keep dark `Platform.mat`. Post-fix every pad bounds == transform (0.6,0.1,0.6 at corners).
  No camera/lighting/URP changes (evidence-based minimalism). Background stays default sky (simple,
  non-distracting — no change needed). Lock contrast intact (blue `Stone_Locked` vs warm greys).

## Architecture (Phase 5) — intact

Roots unscaled; Visual scales per config; RBs dynamic, masses 1/2/3.5/5; BoxCollider sizes per config;
InputReader refs wired (select/pointer/rotate GUIDs non-zero — prior BUG-008 resolved); timer/bonus/
reset/HUD refs present; no new physics on visuals; no monolith; no gameplay value changed.

## Runtime (Phase 6) — verified as far as tools permit

- Play loads with no relevant errors; all 9 visuals render assigned correctly (Edit + Play screenshots).
- Origin-pile gone everywhere (probe + screenshots, Edit + Play); HUD (Time/Locked/message/Left/Right/
  Reset) visible and legible; timer counts live (120.0 → 104.0 over ~16 s); stones settle on pads
  (probe y 0.42→0.50 etc., velocities → ~0.05); LevelController state=Placing.
- NOT VERIFIED (need manual pointer input in Play): selection exclusivity, drag, rotation, release/
  stability/lock flow, lock material swap on screen, bonuses, reset button, timer expiry, success/fail.
  Smallest verification: hand-play one stone (click → drag to altar → release → 2 s calm → lock + bonus).
- Environment flakiness (NOT a game bug): ~4/6 Play sessions stalled at frame 0–1 (Time.time frozen,
  isPlaying=true, unpaused, timeScale=1); one 30 s session ran perfectly (timer + physics + probe).
  All-or-nothing, content-independent, main thread responsive. Suspect MCP `execute_code` interplay
  and/or Play-enter race; passive runs (no in-Play executes) behave best. Repro: enter Play via MCP,
  poll `Time.frameCount` with distinct snippets.

## Cleanup

- Deleted (findings resolved, evidence captured): `StoneVisibilityProbeTEMP.cs` (+`.meta`).
- Deleted stale/obsolete shots (incl. `screenshot-20261008-111157.png`): superseded by
  `screenshot-20261009-053624.png` (Play, timer 104, HUD) and `screenshot-20261009-054407.png`
  (final Edit). Both kept.
- Scratch C# under `balancepuzzle/Temp/` is diagnostic-only (never compiled into the game).

## Stage status (distinctions preserved)

- MCP connectivity: RESTORED + verified (this session).
- Asset import: VERIFIED (all 5 + pad slab, live importer queries).
- Visuals integrated: DONE (stones validated, altar + pads integrated).
- Runtime behavior: PARTIALLY verified (load/render/HUD/timer-count/settle proven; input-driven flows NOT VERIFIED).
- **Stage 1: NOT complete, NOT locked** (input-driven verification gates outstanding — see test-checklist
  mapping in final report). No commit/push/tag performed (no git per instructions).
- Recommended next milestone: manual Play pass (one-stone lock loop + reset + expiry) → formal Stage 1 lock.
