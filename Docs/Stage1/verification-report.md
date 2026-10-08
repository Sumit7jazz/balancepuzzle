# Stage 1 — Verification Report

Date: 2026-10-07 (static) / 2026-10-08 (Unity handover update)
Branch: `stage-1-development`
Scope: Core Physics Sandbox (File Groups 1–4 + testing fixes)

This report compares the actual implementation against the approved Stage 1
plan (`Docs/Stage1/plan.md`, the contract).

> **2026-10-08 update:** The user ran the project in Unity 6000.0.4f1.
> Environment, packages (Input System, URP, UI), Active Input Handling,
> Solver Iterations (8), gravity, fixed timestep, initial compile (0 errors,
> 0 warnings), scene load, finalizer execution, InputReader wiring, and
> Stage1Config values all PASS. Three runtime blockers were discovered
> (BUG-004/005/006) and fixed. Full Play Mode verification still pending.

---

## 1. What passed

**Static verification (all with real tools):**
- C# syntax: 17/17 parse clean. PASS.
- YAML referential integrity: 12/12 files. PASS.
- Config values match plan §H. PASS.
- Physics materials correct. PASS.
- Contract compliance (no future-stage features, input isolation, no
  FindObjectOfType in runtime, no hardcoded numbers, event-driven reset).
  PASS.
- Secrets scan. PASS.

**Unity verification (user, 2026-10-08, Unity 6000.0.4f1):**
- Unity version, packages, input handling, physics settings. PASS.
- Initial compile: 0 errors, 0 warnings. PASS.
- Scene loads. PASS.
- Finalizer executes; InputReader references assigned. PASS.
- Stage1Config values correct. PASS.
- Font issue (Arial → LegacyRuntime) fixed and verified. PASS.

## 2. What failed (defects found)

Three defects were found by static review and fixed (commits 2101af9,
c0a1977, c248ebc). Three more were found by Unity runtime testing and fixed
(commit 120f46a). All six fixes are committed and pushed.

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
  not invocable.
- **Fix:** invoke `onUp()` as intended (one line).
- **Files changed:** `Assets/Scripts/UI/HudController.cs` (1 line).
- **Risk:** none. **Retest required:** ST-CMP-01, ST-ROT-03, ST-ROT-04.

### BUG-003 — HIGH — COM offsets distorted by scaled transforms (fixed, commit c248ebc)
- **Symptom:** world-space COM would not match configured offsets.
- **Root cause:** stone root GameObjects were scaled to the configured size;
  `Rigidbody.centerOfMass` is expressed in scaled local space.
- **Fix:** roots stay at scale (1,1,1); BoxCollider carries the true size;
  the cube mesh moved to a "Visual" child scaled to the stone size.
- **Files changed:** 4 stone prefabs + `Stage1_Sandbox.unity` (structure only).
- **Risk:** low-medium. **Retest required:** ST-PHY-01–04, ST-COM-01–03.

### BUG-004 — CRITICAL — Input actions fail at startup (fixed, commit 120f46a)
- **Symptom (Unity 6000.0.4f1):** "Failed to load
  '.../Stage1Input.inputactions'. File may be corrupted or was serialized
  with a newer version of Unity." Asset opens fine in the Input Actions
  editor but fails at startup deserialization.
- **Root cause:** `Stage1AssetFinalizer` used `AssetDatabase.AddObjectToAsset`
  to inject `InputActionReference` sub-assets into the `.inputactions` file,
  corrupting Unity 6 Input System serialization.
- **Fix:** finalizer now creates standalone `InputActionReference` .asset
  files under `Assets/Input/References/`; the `.inputactions` JSON is never
  modified.
- **Files changed:** `Assets/Editor/Stage1AssetFinalizer.cs` (rewritten).
- **User action required:** `git checkout -- Assets/Input/Stage1Input.inputactions`
  (restore corrupted file), then re-run Finalize.
- **Risk:** low. **Retest required:** ST-CMP-04, ST-INT-01–07, ST-ROT-01–05.

### BUG-005 — CRITICAL — Materials pink, shader GUID invalid (fixed, commit 120f46a)
- **Symptom (Unity 6000.0.4f1):** "Could not extract GUID in text file ...
  at line 11"; materials show `Hidden/InternalErrorShader` (pink).
- **Root cause:** generated `.mat` YAML contained a 33-character shader GUID
  (valid Unity GUIDs are exactly 32 hex chars). The malformed YAML failed
  import, so the finalizer's `LoadAssetAtPath` returned null and could not
  repair them.
- **Fix:** corrected to valid 32-char placeholder GUIDs in all three `.mat`
  files; finalizer now logs clear errors if a material fails to load.
- **Files changed:** 3 `.mat` files; `Stage1AssetFinalizer.cs`.
- **Risk:** low. **Retest required:** ST-CMP-02/03, re-run Finalize, confirm
  URP/Lit in Inspector.

### BUG-006 — HIGH — Arial font throws in Unity 6 (fixed, commit 120f46a)
- **Symptom (Unity 6000.0.4f1):** `ArgumentException: Arial.ttf is no longer
  a valid built in font.`
- **Root cause:** `HudController` used
  `Resources.GetBuiltinResource<Font>("Arial.ttf")`.
- **Fix:** use `"LegacyRuntime.ttf"` (verified in user's environment).
- **Files changed:** `Assets/Scripts/UI/HudController.cs` (1 line).
- **Risk:** none. **Retest required:** ST-CMP-04, HUD suite.

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
| BUG-004 input serialization | CRITICAL | 120f46a | ST-CMP-04, ST-INT-01–07, ST-ROT-01–05 |
| BUG-005 material GUID | CRITICAL | 120f46a | ST-CMP-02/03, Finalize re-run |
| BUG-006 Arial font | HIGH | 120f46a | ST-CMP-04, HUD suite |
| Robustness hardening | LOW | c0a1977 | regression (reset suite) |

No open defects remain. Six bugs found, six fixed.

## 4. What remains

**Unity Play Mode verification** — the full gameplay test matrix
(ST-PHY, ST-INT, ST-ROT, ST-STB, ST-STEP, ST-TIME, ST-RST, ST-PRES, ST-COM,
HUD, ST-DEV Android). These are marked BLOCKED in
`Docs/Stage1/test-checklist.md` pending the retest pass.

**Required user steps before retesting** (in order):
1. Pull latest (`git pull origin stage-1-development`).
2. Restore the corrupted input asset:
   `git checkout -- Assets/Input/Stage1Input.inputactions`
3. In Unity: run `Balance Puzzle → Finalize Stage 1 Assets`, save scene.
4. Confirm: 0 console errors, materials show URP/Lit (not pink), no
   inputactions load failure on Play Mode start.
5. Execute `Docs/Stage1/test-checklist.md` in order, priority on the retests
   in §3 above.

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

## 10. Final pre-MCP hardening batch (2026-10-08, commit pending)

**State-machine audit:** `LevelController.Fail()` now stops the
`StabilityEvaluator` (previously, a timer-expiry during `Evaluating` left the
evaluator running its FixedUpdate until reset — wasteful, though the
controller's state guard prevented incorrect locks).

**Hardening (no gameplay behavior change in normal flow):**
- `StoneDragger.HandleRelease`: null-guard `levelController` before
  `BeginEvaluation`.
- `LevelController.OnStabilityPassed`: `TryLockCandidate == false` now logs
  and fails the evaluation instead of soft-locking in `Evaluating`.
- `HudController`: null-guards in Reset/Play-Again/Rotate button lambdas;
  timer text cached to avoid per-frame string allocation on mobile.
- New editor tool: `Balance Puzzle → Check Stage 1 Health` (read-only
  wiring + config contract validation).
- New static regression: `Tools/check_stage1_regression.py` validates
  BUG-001–006 fixes and contract values; BUG-007 stays runtime-BLOCKED.

**Static verification:** all C# files structurally sound; all material GUIDs
32-char; all stone roots at scale (1,1,1); no secrets; contract values match.

**BUG-007:** still OPEN. Root cause not proven. The hardened diagnostic
(`Balance Puzzle → Diagnose BUG-007 Materials`, sections A–F) is the
instrument for the Unity MCP pass. No speculative fix applied.

**Recommendation unchanged: NOT READY FOR LOCK.** The Unity MCP runtime pass
(health check → BUG-007 diagnostic → full Play Mode checklist) is the next
required step.
