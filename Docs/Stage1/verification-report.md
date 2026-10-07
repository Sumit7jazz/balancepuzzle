# Stage 1 — Verification Report

Date: 2026-10-07
Branch: `stage-1-development`
Scope: Core Physics Sandbox (File Groups 1–4 + testing fixes)

This report compares the actual implementation against the approved Stage 1
plan (`Docs/Stage1/plan.md`, the contract). It is written after the testing
phase's static verification. **Unity Play Mode testing remains BLOCKED**
(no Unity Editor in this environment) — see "What remains blocked".

---

## 1. What passed (static verification)

All of the following were verified with real tools, not by inspection alone:

- **C# syntax:** 17/17 scripts parse clean under a real C# grammar. PASS.
- **YAML referential integrity:** 12/12 asset files — every local fileID
  resolves, every GUID has a `.meta`, every `m_Script` maps to a real script,
  no duplicate fileIDs. PASS.
- **Config values:** every `Stage1Config` value matches plan §H (stones,
  stability, timer, bonuses 2/3/5/7, interaction, killY, platform). PASS.
- **Physics materials:** stone 0.6/0.6/0.02, platform 0.9/0.9/0. PASS.
- **Contract compliance:** no future-stage features; InputReader is the sole
  gameplay input consumer; no `FindObjectOfType` in runtime code; no hardcoded
  gameplay numbers; event-driven reset; candidate-only locking; single timer
  authority; presentation decoupled from gameplay. PASS.
- **Secrets scan:** PASS.
- **Git:** working tree clean, history linear, no force-push, no secrets.

## 2. What failed (defects found by static testing)

Three defects were found and fixed. All fixes are committed and pushed.

### BUG-001 — CRITICAL — Timer expired on frame 1 (fixed, commit 2101af9)
- **Symptom:** every fresh level would instantly fail with "Time up."
- **Root cause:** `LevelTimer.TimeLeft` was only assigned in `ResetState()`
  (reset-event only). `LevelController.Start()` called `SetRunning(true)`
  with `TimeLeft == 0`.
- **Fix:** initialize `TimeLeft` from `config.initialTime` in `Awake()`.
- **Files changed:** `Assets/Scripts/Level/LevelTimer.cs` (+8 lines).
- **Risk:** low. **Retest required:** ST-TIME-01, ST-TIME-02, ST-RST-01.

### BUG-002 — CRITICAL — Project would not compile (fixed, commit c0a1977)
- **Symptom:** compile error; rotate buttons dead.
- **Root cause:** in `HudController.AddHoldHandler`, the PointerUp lambda
  invoked `up()` where `up` is the `EventTrigger.Entry` local — `Entry` is
  not invocable. (A brace-balance check cannot catch type errors; the
  tree-sitter parse also cannot — this was caught by careful re-reading.)
- **Fix:** invoke `onUp()` as intended (one line).
- **Files changed:** `Assets/Scripts/UI/HudController.cs` (1 line).
- **Risk:** none. **Retest required:** ST-CMP-01, ST-ROT-03, ST-ROT-04.

### BUG-003 — HIGH — COM offsets distorted by scaled transforms (fixed, commit c248ebc)
- **Symptom:** world-space COM would not match the configured offsets
  (e.g. Stone C: (-0.18, 0.035) instead of (-0.15, 0.05)).
- **Root cause:** stone root GameObjects were scaled to the configured size;
  `Rigidbody.centerOfMass` is expressed in scaled local space.
- **Fix:** roots stay at scale (1,1,1); BoxCollider carries the true size;
  the cube mesh moved to a "Visual" child scaled to the stone size.
- **Files changed:** 4 stone prefabs + `Stage1_Sandbox.unity` (structure only).
- **Risk:** low-medium (hierarchy change). Selector/presenter/dragger
  compatibility verified statically. **Retest required:** ST-PHY-01–04,
  ST-COM-01–03, ST-INT-01.

### Robustness hardening (commit c0a1977, no behavior change when wired correctly)
- `StoneState.ResetState` teleports via `Rigidbody.position`/`rotation`.
- Null-guards in `LevelTimer.Update`, `StoneRotator.Update`,
  `StepLockManager.TryLockCandidate` hot paths.

## 3. What was fixed (summary)

| Bug | Severity | Commit | Retest needed |
|---|---|---|---|
| BUG-001 timer init | CRITICAL | 2101af9 | ST-TIME-01/02, ST-RST-01 |
| BUG-002 compile error | CRITICAL | c0a1977 | ST-CMP-01, ST-ROT-03/04 |
| BUG-003 COM distortion | HIGH | c248ebc | ST-PHY-01–04, ST-COM-01–03, ST-INT-01 |
| Robustness hardening | LOW | c0a1977 | regression (reset suite) |

No open defects remain from static review.

## 4. What remains blocked

**All Unity-dependent verification** — compile in the real Unity compiler,
Play Mode, the full test checklist (ST-ENV, ST-CMP, ST-PHY, ST-INT, ST-ROT,
ST-STB, ST-STEP, ST-TIME, ST-RST, ST-PRES, ST-COM, HUD, ST-DEV Android).
These are marked BLOCKED in `Docs/Stage1/test-checklist.md`, honestly —
none is reported as PASS.

The exact manual procedure for the tester is in `Docs/Stage1/unity-setup.md`
(§5 Finalize step) plus the checklist. Priority retests after the fixes:
ST-CMP-01 (compile), ST-TIME-01 (timer starts at 120), ST-ROT-03/04
(rotate buttons), ST-PHY-02/03 (COM behavior), ST-COM-02 (COM gizmos).

## 5. Approved deviations (documented, behavior-preserving)

1. UI is built at runtime by `HudController` (not scene YAML) — uGUI YAML
   script references are not verifiable without Unity; runtime construction
   is exact, reviewable C#. Same for the EventSystem fallback.
2. Scene stones are inline GameObjects, not prefab instances — avoids
   unverifiable `PrefabInstance` YAML; prefabs exist as canonical templates.
3. `InputReader` action references are assigned by the one-time Editor menu
   item (`Balance Puzzle → Finalize Stage 1 Assets`) — same YAML reason.
4. `Stage1Config.asset` is pre-created in-repo (was a manual Editor step).
5. Rigidbody drag/angularDrag (0.05/0.3, plan §H) live in prefab/scene YAML,
   not in `Stage1Config` — avoided modifying the approved Group 1 file.
6. `HudController` touches `UnityEngine.InputSystem.UI` solely for the
   EventSystem's `InputSystemUIInputModule` (required UI infrastructure);
   all gameplay input remains isolated in `InputReader`.
7. Asset GUIDs are now deterministic (UUIDv5) instead of random — stable
   across generator runs; one-time identity change, no content impact.

## 6. Unexpected deviations

None. The BUG-003 structural change (Visual child) was required to honor the
configured COM offsets exactly; it does not alter the plan's observable
behavior — it corrects the implementation toward it.

## 7. Known limitations

- Unity compile / Play Mode / Android testing not performed here.
- Material shader GUID in YAML is best-effort; the Finalize menu item
  corrects it via `Shader.Find("Universal Render Pipeline/Lit")`.
- `Packages/manifest.json` does not exist yet — Unity creates it on first
  open; the user installs Universal RP + Input System per `unity-setup.md`.
- Physics Solver Iterations = 8 must be confirmed in Project Settings.
- The AABB support approximation may pass exotic layouts (plan §O, accepted).

## 8. Definition of Done audit (plan §N)

| Requirement | Static status | Play Mode status |
|---|---|---|
| Per-stone physics, mass/COM differences | Implemented, values verified | BLOCKED |
| Select/drag/rotate/release | Implemented, logic verified | BLOCKED |
| Locked immovable | Implemented, logic verified | BLOCKED |
| Unstable fails; motion resets timer | Implemented, logic verified | BLOCKED |
| Pass at calmTime ≥ 2.0 s with support validation | Implemented, logic verified | BLOCKED |
| Candidate-only locking | Implemented, logic verified | BLOCKED |
| 2/3/5/7 bonuses from config | Implemented, values verified | BLOCKED |
| 120/150 timer, stacking bonuses | Implemented (BUG-001 fixed) | BLOCKED |
| Event-driven full reset, replay works | Implemented, logic verified | BLOCKED |

**Definition of Done: NOT SATISFIED** — the right column must be demonstrated
in Unity before lock. Nothing is marked PASS that was not actually verified.

## 9. Recommendation

The implementation is complete, internally consistent, and free of known
static defects. **Do not lock yet.** The remaining work is exactly the Unity
Play Mode pass defined in `Docs/Stage1/test-checklist.md`, with priority on
the retests listed in §4 above. After that pass, this report plus the filled
checklist form the basis for the lock decision.
