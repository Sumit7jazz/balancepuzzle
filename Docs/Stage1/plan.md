# Stage 1 — Core Physics Sandbox (LOCKED PLAN, amended 2026-10-07)

Goal: prove the fundamental 3D balancing mechanic is fun, predictable, stable,
and technically reliable. Nothing more.

Process per stage: PLANNING → DEVELOPMENT → TESTING → VERIFICATION → LOCK.
Do not start the next stage until the current one is LOCKED.

## A. Working agreements

- No future-stage features. No login, backend, cloud save, lives, ads, IAP,
  social, friends, teams, clans, Battle Pass, store, GIF, leaderboards,
  analytics, multiplayer — unless explicitly required (they are not).
- Modular architecture; no God-object manager. Physics, input, UI, level
  state, timer, stability evaluation, progression are separate responsibilities.
- All important gameplay values live in `Stage1Config`; never hard-coded.
- Locked behavior is a contract. Changing a shared file requires:
  FILE CHANGED / WHY / WHAT CHANGED / RISK / TEST REQUIRED — and approval.
- Git: local commits only. No push, force-push, branch deletion, permission
  change, or destructive operation without explicit approval. Never commit
  secrets anywhere (source, assets, docs, logs). Auth via transient in-memory
  credentials only.
- Branches: `main` → `stage-1-development` → `stage-1-testing` →
  `stage-1-locked`; tag `v0.1.0-stage1-locked`.

## B. Architecture

Thin orchestrator + independent systems over C# events.

- `InputReader → StoneSelector → StoneDragger / StoneRotator → Rigidbody`
- `Release → LevelController → StabilityEvaluator → (pass) → StepLockManager → LevelTimer.AddBonus → HudController`
- `LevelReset.RequestReset() → OnResetRequested → each system resets itself`
- `StepLockManager --OnStoneLocked/OnStoneUnlocked--> StoneLockPresenter` (visuals only)
- `LevelController` owns only the `LevelState` machine
  (`Placing → Evaluating → Complete | Failed`) plus `CandidateStone`.
  No physics, input, or UI logic inside it.
- `InputReader` is the ONLY script touching UnityEngine.InputSystem.
- Inspector wiring; no `FindObjectOfType` in hot paths.

## C–E. Layout

- Folders: `Assets/Scenes`, `Assets/Scripts/{Config,Core,Input,Interaction,Physics,Level,UI,Debug}`,
  `Assets/Prefabs`, `Assets/Materials`, `Assets/PhysicsMaterials`,
  `Assets/ScriptableObjects/Stage1`, `Assets/Input`, `Docs/Stage1`.
- Packages: Universal RP, Input System. Nothing else.
- Scene `Stage1_Sandbox`: Main Camera, Directional Light, Platform (6×0.5×6,
  top at y=0), 4 SpawnPads, Stone_A–D, Managers, UI Canvas
  (TimerText, StepText, MessageText, Rotate/Reset buttons, ResultPanel), EventSystem.

## F–G. Scripts (16)

| File | Responsibility |
|---|---|
| `Config/Stage1Config.cs` | All tunables. Data only, no logic. |
| `Core/LevelController.cs` | State machine + candidate tracking + event routing. |
| `Input/InputReader.cs` | Sole Input System owner; Select/Drag/Release/Rotate events. |
| `Interaction/StoneSelector.cs` | Raycast select of unlocked stones only. |
| `Interaction/StoneDragger.cs` | Kinematic XZ drag on grab-height plane, clamped to dragRadius. |
| `Interaction/StoneRotator.cs` | Y-axis rotation from InputReader.RotateAxis. |
| `Physics/StabilityEvaluator.cs` | Calm + support validation; raises Passed / EvaluationFailed. |
| `Physics/CenterOfMass.cs` | Applies configured COM offset in Awake. |
| `Physics/ContactTracker.cs` | Per-stone contact-point collection each FixedUpdate. |
| `Physics/StoneState.cs` | Records/restores initial transform + body settings. |
| `Level/StepLockManager.cs` | Lock registry, lock/unlock events, progressive bonus lookup. |
| `Level/LevelTimer.cs` | Countdown, AddBonus clamped to maxTime, TimerExpired. |
| `Level/LevelReset.cs` | Raises `OnResetRequested`; holds no restoration logic. |
| `UI/HudController.cs` | Display only: timer, steps, messages, result panel. |
| `UI/StoneLockPresenter.cs` | Locked visual from lock events. Presentation only. |
| `Debug/ComVisualizer.cs` | COM gizmo. Debug only, removable. |

## H. Configuration (Stage1Config defaults)

- Stones: A 0.8³/1.0kg/COM(0,0,0); B 1.0×0.8×0.8/2.0kg/COM(0.12,0,0);
  C 1.2×0.7×0.7/3.5kg/COM(−0.15,0.05,0); D 1.4×0.8×0.8/5.0kg/COM(0,0.10,0).
- Stability: duration 2.0 s; linVel ≤ 0.08 m/s; angVel ≤ 0.20 rad/s;
  impact reset > 1.5 m/s; supportMargin 0.02 m.
- Timer: initial 120 s, max 150 s. Step bonuses: [2, 3, 5, 7] s.
- Interaction: dragRadius 8 m; rotate 90°/s (Y axis only).
- World: killY −3.0; platform 6×0.5×6.
- Physics project settings: gravity −9.81, fixed timestep 1/60, solver iterations 8.
- Bodies: drag 0.05, angularDrag 0.3, interpolate on, Discrete CD.
- Materials: stone↔stone friction 0.6/bounce 0.02; stone↔platform 0.9/0.

## I. Input

Actions: Select (`<Pointer>/press`), PointerPosition (`<Pointer>/position`),
Rotate (1D axis Q/E). UI rotate buttons call
`InputReader.SetRotateAxis(±1)` → same axis. One rotation system.

## J. Stability algorithm

- On release, `LevelController` enters Evaluating with that stone as candidate.
- Each FixedUpdate, calm requires for every unlocked stone:
  1. linear velocity ≤ maxLinearVelocity,
  2. angular velocity ≤ maxAngularVelocity,
  3. no impact with relative velocity > impactVelocityReset (resets calmTime),
  4. support validation: mass-weighted combined COM (XZ) of unlocked stones
     inside the XZ AABB of their contact points, expanded by supportMargin.
     Checked only when (1)–(3) pass and ≥1 contact exists.
- `calmTime += fixedDeltaTime` while calm, else 0.
- Pass when `calmTime >= stabilityDuration`. Locked stones excluded.
- Fail: unlocked stone below killY → "Stone fell"; timer expiry → "Time up".
- Full support-polygon is a deferred later-stage expansion.

## K. Step-lock algorithm

- Only the candidate stone can be locked. Selector rejects locked stones.
- On pass: `TryLockCandidate(candidate)` → verifies unlocked → kinematic,
  registry update, `OnStoneLocked` → bonus `stepTimeBonuses[lockedCount]`
  → `LevelTimer.AddBonus`. 4th lock → `AllStepsLocked` → Complete.

## L. Timer algorithm

- Starts at initialTime on level start; ticks in Placing/Evaluating only.
- `AddBonus(s)`: `timeLeft = min(timeLeft + s, maxTime)` — stacks above 120.
- Reaching 0 → Failed ("Time up").

## M. Test plan (manual, Editor Play Mode + one Android smoke build)

- ST-PHY-01–04: each stone behaves per mass/size. 05: heavy vs light differ.
  06: COM offsets change resting behavior. 07: collision/friction correct.
- ST-INT-01–04: select / drag / rotate / release. 05: locked stone immovable.
  06: buttons and Q/E rotate identically.
- ST-STB-01: unstable fails. 02: motion resets timer. 03: pass at calmTime ≥ 2.0 s.
  04: fall → fail. 05: COM overhang never passes. 06: supported calm passes.
- ST-STEP-01: pass → lock (kinematic). 02: kinematic immovable. 03: bonuses
  exactly 2/3/5/7 from config. 04: 4th lock → complete. 05: only candidate locks.
  06: presenter decoupled (disable it → locking still works).
- ST-TIME-01–03: starts, counts, zero → fail. 04: bonuses stack above 120, cap 150.
- ST-RST-01: full restore + replay. 02: every system self-restores via event.
- ST-DEV-01: Android touch smoke test.

## N. Definition of Done

All of the above demonstrated (not "code finished"): per-stone physics,
mass/COM differences, select/drag/rotate/release, locked immovable,
unstable fails, motion resets timer, pass at calmTime ≥ 2.0 s with support
validation, candidate-only locking, 2/3/5/7 bonuses from config, 120/150 timer
with stacking bonuses, event-driven full reset, replay works.
Lock gate: all critical tests pass, Verification Report written, no open
critical bugs, tag `v0.1.0-stage1-locked`.

## O. Known risks

- PhysX not bit-deterministic across devices (tune on Editor + one Android).
- Fast-drag tunneling → kinematic drag + Discrete CD; escalate if observed.
- Sleeping bodies read as calm (correct here); dragger wakes on grab.
- AABB support approximation may pass exotic layouts — accepted; full
  support-polygon is a named later expansion and must not change Stage 1
  contracts without approval.
- Y-axis rotation only — deliberate scope cut.
