# STAGE 1 — DEVELOPMENT CHECKPOINT (not a lock)

Date (UTC): 2026-10-08
Project: `balancepuzzle` (Unity 6000.6.4f1, URP 17.6.0, branch `stage-1-development`)
Scene: `Assets/Scenes/Stage1_Sandbox.unity`

## Status lines

- DEVELOPMENT CHECKPOINT CREATED
- STAGE 1: CORE SYSTEMS DEVELOPED, RUNTIME VERIFICATION BLOCKED BY OPEN EDITOR RENDER ISSUE
- NEXT STAGE: REAL GAME PRESENTATION / ENVIRONMENT (see `Assets/Art/PIPELINE.md`)
- Stage 1 is NOT declared COMPLETE and NOT LOCKED. No commit / push / tag performed.
- UPDATE 2026-10-09 (Stage 2 milestone session, live-verified): the render-state anomaly is
  RESOLVED for all 9 scene renderers — stones, Platform_Altar, and (after fix) spawn pads all
  report bounds == transforms in Edit and Play, confirmed by probe telemetry + Game view
  screenshots (`Assets/Screenshots/screenshot-20261009-053624.png` Play, `-054407.png` final).
  Root sub-case found and fixed: built-in-Cube renderers drew at identity (dark cube on altar);
  pads now use imported `SpawnPad_Slab`. Probe + stale screenshot removed (see §Cleanup).
  Stage 1 REMAINS NOT LOCKED: input-driven flows (select/drag/rotate/release/lock/bonus/reset/
  expiry) still need a manual Play pass. Details: `Docs/STAGE2_MILESTONE_LOG.md`.

## Implemented Stage 1 systems (all present, verified by file read)

| Area | Files |
|---|---|
| Config (single source of truth) | `Assets/Scripts/Config/Stage1Config.cs`, `Assets/ScriptableObjects/Stage1/*` |
| Core coordination | `Assets/Scripts/Core/LevelController.cs` |
| Input (sole reader) | `Assets/Scripts/Input/InputReader.cs`, `Assets/Input/Stage1Input.inputactions`, `Assets/Input/References/Stage1Input_{Select,PointerPosition,Rotate}.asset` |
| Interaction | `Interaction/StoneSelector.cs`, `Interaction/StoneDragger.cs`, `Interaction/StoneRotator.cs` |
| Physics/gameplay state | `Physics/StoneState.cs`, `Physics/ContactTracker.cs`, `Physics/CenterOfMass.cs`, `Physics/StabilityEvaluator.cs` |
| Level flow | `Level/StepLockManager.cs`, `Level/LevelTimer.cs`, `Level/LevelReset.cs` |
| UI | `UI/HudController.cs`, `UI/StoneLockPresenter.cs` |
| Debug viz | `Debug/ComVisualizer.cs` (gizmos only) |
| Editor tools | `Stage1AssetFinalizer`, `Stage1HealthCheck`, `Bug007Diagnostic` (per handoff) |
| Render pipeline | `Assets/Settings/Stage1_URP.asset` (+ renderer wired), `Assets/Materials/Stage1/{Platform,Stone_Default,Stone_Locked}.mat` (opaque URP/Lit) |

## Contract consistency (verified, not assumed)

`Stage1Config` values and their in-code consumers match: stability 2.0 s / 0.08 m/s /
0.20 rad/s / impact 1.5 / margin 0.02 / killY −3 (`StabilityEvaluator`, calm +=
fixedDeltaTime, pass on `>=`); timer init 120 / cap 150 (`LevelTimer`, bonus clamps
only to max); bonuses [2,3,5,7] (`StepLockManager` reads `stepTimeBonuses`, validates
length vs stones); masses 1/2/3.5/5 + COM offsets + sizes A(0.8³) B(1.0×0.8²)
C(1.2×0.7²) D(1.4×0.8²) — sizes exactly match live `visScale` from the v2 probe.
Scene colliders present: platform + 4 stones + 4 spawn pads. Prior fixes intact in
tree: timestep 1/60 (`ProjectSettings/TimeManager.asset:8`), URP renderer slot
(`Stage1_URP.asset:18-19`), HUD landscape 1920×1080 (`HudController.cs:175`),
input wiring saved (`Stage1_Sandbox.unity:1003`).

## Runtime evidence on record (v2 probe, `Logs/Editor.log`, t=0–18)

Camera perfect; `pipeline=Stage1_URP` live; 9/9 renderers `isVisible=true` from t=2;
transforms settled at pads; meshes/materials/layers/shadows correct; console clean
(no script/shader errors). Sole anomaly: every `Renderer.bounds` = (0,0,0)/(1,1,1)
while transforms sit at pads — renderers submitting identity matrices ("origin
pile": 4 stones collapse to one ~90 px beige cube at center, platform hidden
inside). Same-run Game view confirmed: one beige cube, no platform/arrangement.

## OPEN ENVIRONMENT/RUNTIME BLOCKER (not a content bug)

Unity Editor render-state anomaly: renderer world matrices/bounds not following
transforms in this session. No project file is wrong; no gameplay/physics/material/
camera/spawn edit is licensed (all would be speculative). Recovery: restart Unity →
Play Mode → expect `boundsC≈visPos` per object + full scene visible → then resume
Stage 1 runtime verification (interaction → stability → locking → timer → reset →
HUD/COM → regression exit 0 → Health 0/0 → clean Console).

## Cleanup pending (tool-blocked, safe to do manually)

- DELETED 2026-10-09 (findings resolved, evidence in `Docs/STAGE2_MILESTONE_LOG.md` + kept screenshots):
  `Assets/Scripts/Debug/StoneVisibilityProbeTEMP.cs` (+`.meta`),
  `Assets/Screenshots/screenshot-20261008-111157.png` (+`.meta`) and intermediate
  diagnostic shots. Kept: `screenshot-20261009-053624.png`, `screenshot-20261009-054407.png`.
