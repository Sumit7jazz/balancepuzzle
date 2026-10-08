# Stage 1 — Test Checklist

Branch: `stage-1-development`
Status: **TESTING IN PROGRESS — blocked on Unity Editor environment**

All interactive tests below require the Unity Editor (Unity 6 LTS / 6000.x)
with Universal RP + Input System. They were NOT executed in this environment
because no Unity Editor is available here. Each is marked **BLOCKED** with the
reason, so the tester (on a machine with Unity) can run them in order.

Static (non-Unity) verification that WAS performed is listed in
"Static review results" at the end.

Result codes: `PASS` / `FAIL` / `BLOCKED` / `NOT APPLICABLE`

---

## A. Environment

| Test ID | Description | Result | Observed behavior | Notes |
|---|---|---|---|---|
| ST-ENV-01 | Unity 6 LTS / 6000.x | BLOCKED | — | No Unity Editor in this environment. Verify on test machine. |
| ST-ENV-02 | Universal RP installed and active | BLOCKED | — | Install per Docs/Stage1/unity-setup.md |
| ST-ENV-03 | Input System installed and active | BLOCKED | — | Install per Docs/Stage1/unity-setup.md |
| ST-ENV-04 | Physics Solver Iterations = 8 | BLOCKED | — | ProjectSettings must be checked in Editor |
| ST-ENV-05 | No unapproved packages | BLOCKED | — | Check Package Manager in Editor |
| ST-ENV-06 | Stage1Config asset exists and assigned | BLOCKED | — | Asset exists at Assets/ScriptableObjects/Stage1/Stage1Config.asset (verified in repo); assignment check needs Editor |

## B. Compilation

| Test ID | Description | Result | Observed behavior | Notes |
|---|---|---|---|---|
| ST-CMP-01 | Full project compile, 0 errors | BLOCKED | — | Needs Unity compile. Real C# syntax parse of all 17 scripts: PASS (see Static review). |
| ST-CMP-02 | No missing script components | BLOCKED | — | Scene YAML validated structurally; Unity acceptance unverified |
| ST-CMP-03 | No missing serialized references | BLOCKED | — | All fileID/GUID refs resolve statically; Unity acceptance unverified |
| ST-CMP-04 | No runtime exception on entering Play Mode | BLOCKED | — | |

## C. Scene / References

All items below: **BLOCKED** — verify in Editor after running
`Balance Puzzle → Finalize Stage 1 Assets` once and saving the scene.
- Main Camera, Directional Light, Platform, 4 spawn pads, 4 stones,
  Managers, UI, Debug COM visualization, EventSystem (or runtime-created).
- Each stone has Rigidbody, BoxCollider, StoneState, CenterOfMass, ContactTracker.
- No stone uses MeshCollider.
- InputReader action references assigned (via finalizer).

## D. Physics

| Test ID | Description | Result | Observed behavior | Notes |
|---|---|---|---|---|
| ST-PHY-01 | Drop Stone A: falls, collides, settles, no jitter | BLOCKED | — | |
| ST-PHY-02 | Drop Stone B: COM offset affects behavior | BLOCKED | — | |
| ST-PHY-03 | Drop Stone C: configured COM offset applied | BLOCKED | — | |
| ST-PHY-04 | Drop Stone D: mass + COM configuration | BLOCKED | — | |
| ST-PHY-05 | Mass difference push/contact experiment | BLOCKED | — | |
| ST-PHY-06 | Stack stones: no interpenetration, no explosion | BLOCKED | — | |
| ST-PHY-07 | Platform friction feels reasonable | BLOCKED | — | Record feel; do not change values during testing |

## E. Selection

| Test ID | Description | Result | Observed behavior | Notes |
|---|---|---|---|---|
| ST-INT-01 | Tap/click stone selects it | BLOCKED | — | |
| ST-INT-02 | Tap empty space selects nothing | BLOCKED | — | |
| ST-INT-03 | Tap UI does not select stone behind UI | BLOCKED | — | StoneSelector.IsPointerOverUI guards this (static review) |
| ST-INT-04 | Locked stone cannot be selected | BLOCKED | — | `!stepLockManager.IsLocked(hitStone)` guard (static review) |

## F. Dragging

| Test ID | Description | Result | Observed behavior | Notes |
|---|---|---|---|---|
| ST-INT-05 | Drag follows pointer on XZ, smooth, no physics fight | BLOCKED | — | Kinematic + MovePosition in FixedUpdate (static review) |
| ST-INT-06 | Drag clamped to dragRadius (6) | BLOCKED | — | Clamp in FixedUpdate (static review) |
| ST-INT-07 | Release: dynamic again, wakes, settles, evaluation begins | BLOCKED | — | isKinematic=false + WakeUp + BeginEvaluation (static review) |

## G. Rotation

| Test ID | Description | Result | Observed behavior | Notes |
|---|---|---|---|---|
| ST-ROT-01 | Q rotates around Y only | BLOCKED | — | Requires finalizer input wiring first |
| ST-ROT-02 | E rotates opposite direction | BLOCKED | — | |
| ST-ROT-03 | Rotate Left UI button = same path as Q | BLOCKED | — | Both feed InputReader.SetRotateAxis (static review) |
| ST-ROT-04 | Rotate Right UI button = same path as E | BLOCKED | — | |
| ST-ROT-05 | Keyboard and UI rotation identical speed/direction | BLOCKED | — | Single RotateAxis path (static review) |

## H. Stability

| Test ID | Description | Result | Observed behavior | Notes |
|---|---|---|---|---|
| ST-STB-01 | Stable placement: calm 2s → PASS | BLOCKED | — | |
| ST-STB-02 | Disturb during evaluation → calm timer resets | BLOCKED | — | Note: selection/drag are gated to Placing state, so disturb by releasing the stone onto the structure |
| ST-STB-03 | Thresholds 0.08 m/s linear, 0.20 rad/s angular | BLOCKED | — | Do not change during testing |
| ST-STB-04 | Hard impact (>1.5 m/s) resets calm evaluation | BLOCKED | — | |
| ST-STB-05 | Stone below killY (-3) → LEVEL FAILED / Stone Fell | BLOCKED | — | |
| ST-STB-06 | COM overhang, still but unsupported → NO PASS (mandatory) | BLOCKED | — | |
| ST-STB-07 | Valid supported placement → PASS (no false rejection) | BLOCKED | — | |
| ST-STB-08 | Whole-structure calmness; only candidate locks | BLOCKED | — | |

## I. Step Lock

| Test ID | Description | Result | Observed behavior | Notes |
|---|---|---|---|---|
| ST-STEP-01 | Pass → candidate kinematic+locked, StoneLocked fires, visual | BLOCKED | — | |
| ST-STEP-02 | Locked stone cannot be moved | BLOCKED | — | |
| ST-STEP-03 | Candidate-only lock (A locks, B stays unlocked) | BLOCKED | — | |
| ST-STEP-04 | Bonuses exactly 2/3/5/7 from config | BLOCKED | — | No hard-coded replacement acceptable |
| ST-STEP-05 | Timer stacks above 120, never exceeds 150 | BLOCKED | — | |
| ST-STEP-06 | Four locks → LEVEL COMPLETE, no 5th step | BLOCKED | — | |

## J. Timer

| Test ID | Description | Result | Observed behavior | Notes |
|---|---|---|---|---|
| ST-TIME-01 | Fresh level starts at 120s | BLOCKED | — | **BUG-001 (timer started at 0) found and fixed in static review** — must verify in Play Mode |
| ST-TIME-02 | Timer decreases in real seconds | BLOCKED | — | |
| ST-TIME-03 | Step completion adds correct bonus | BLOCKED | — | |
| ST-TIME-04 | timeLeft > 120 possible, <= 150 enforced | BLOCKED | — | |
| ST-TIME-05 | Timer reaches 0 → LEVEL FAILED / Time Up | BLOCKED | — | |
| ST-TIME-06 | Timer stops after Complete/Failed | BLOCKED | — | |
| ST-TIME-07 | Exactly one countdown authority | BLOCKED | — | LevelTimer only (static review: no other countdown code) |

## K. Reset

| Test ID | Description | Result | Observed behavior | Notes |
|---|---|---|---|---|
| ST-RST-01 | Reset restores everything (spawns, dynamics, locks, visuals, timer 120, steps 0, HUD) | BLOCKED | — | |
| ST-RST-02 | Reset during evaluation clears it; no delayed pass | BLOCKED | — | |
| ST-RST-03 | Reset after fail → fresh level | BLOCKED | — | |
| ST-RST-04 | Reset after complete → fresh level | BLOCKED | — | |
| ST-RST-05 | Immediate replay works normally | BLOCKED | — | |

## L. Lock Presentation

| Test ID | Description | Result | Observed behavior | Notes |
|---|---|---|---|---|
| ST-PRES-01 | Lock → tint appears | BLOCKED | — | |
| ST-PRES-02 | Reset → normal material returns | BLOCKED | — | |
| ST-PRES-03 | Presenter disabled → gameplay locking still works | BLOCKED | — | Decoupling proof |

## M. COM Visualization

| Test ID | Description | Result | Observed behavior | Notes |
|---|---|---|---|---|
| ST-COM-01 | Stone A marker at configured center | BLOCKED | — | Scene view gizmos |
| ST-COM-02 | B/C/D markers match offsets | BLOCKED | — | |
| ST-COM-03 | Marker moves with Rigidbody while dragging | BLOCKED | — | |
| ST-COM-04 | Visualizer removed → gameplay unaffected | BLOCKED | — | |

## N. HUD

**Result: BLOCKED** — verify in Play Mode:
- Timer shows LevelTimer.TimeLeft; Steps show "Locked: N / 4" and update.
- Messages communicate placement / evaluation / lock / failure / completion.
- Result panel hidden during play, visible after Complete/Failed.
- Reset button calls only LevelReset.RequestReset().
- HUD never becomes a second game-state authority (static review: it only
  subscribes to events and writes text/buttons).

## O. Android Touch Smoke Test

| Test ID | Description | Result | Observed behavior | Notes |
|---|---|---|---|---|
| ST-DEV-01 | Touch-select | BLOCKED | — | Requires Android build from test machine |
| ST-DEV-02 | Touch-drag | BLOCKED | — | |
| ST-DEV-03 | Touch-release | BLOCKED | — | |
| ST-DEV-04 | Rotate Left button | BLOCKED | — | |
| ST-DEV-05 | Rotate Right button | BLOCKED | — | |
| ST-DEV-06 | Reset | BLOCKED | — | |

---

## Static review results (performed without Unity)

These do not replace Play Mode tests, but they are real checks with real tools:

1. **BUG-001 (CRITICAL, fixed):** `LevelTimer.TimeLeft` was never initialized
   outside `ResetState()` (reset-event only). `LevelController.Start()` called
   `SetRunning(true)` with `TimeLeft == 0`, so the timer would expire on the
   first frame — every fresh level would instantly fail with "Time up."
   Fix: initialize `TimeLeft` from `config.initialTime` in `LevelTimer.Awake()`
   (commit 2101af9). No gameplay numbers changed.
   Retest required in Play Mode: ST-TIME-01, ST-TIME-02, ST-RST-01.

2. **BUG-002 (CRITICAL, fixed):** `HudController.AddHoldHandler` had a compile
   error — the PointerUp lambda invoked `up()` where `up` is the
   `EventTrigger.Entry` local, not the `onUp` callback. `Entry` is not
   invocable, so the entire project would not compile and the rotate buttons
   were dead. Fix: invoke `onUp()` as intended (commit c0a1977, one line).
   Retest required in Play Mode: ST-ROT-03, ST-ROT-04, ST-CMP-01.

3. **BUG-003 (HIGH, fixed):** Stone root GameObjects were scaled to the
   configured size, so `Rigidbody.centerOfMass` (expressed in scaled local
   space) produced distorted world-space COM offsets — e.g. Stone C would have
   had COM at (-0.18, 0.035) instead of the configured (-0.15, 0.05). COM
   accuracy is the core balancing mechanic; ST-PHY-02/03/04 and ST-STB-06
   depend on it.
   Fix: root GameObjects now stay at scale (1,1,1); the BoxCollider carries
   the true configured size and the cube mesh lives on a "Visual" child
   scaled to the stone size (commit c248ebc). No gameplay numbers changed.
   Verified compatible: selector raycasts the root collider, the presenter
   finds the child MeshRenderer via GetComponentInChildren.
   Retest required in Play Mode: ST-PHY-01–04, ST-COM-01–03.

> **Update 2026-10-08 — Unity runtime handover (Unity 6000.0.4f1):**
> Environment, packages, compile (0 errors), scene load, and Stage1Config all
> PASS. Three new runtime blockers found and fixed below.

4. **BUG-004 (CRITICAL, fixed 2026-10-08, commit 120f46a):**
   `Stage1AssetFinalizer` used `AssetDatabase.AddObjectToAsset` to inject
   `InputActionReference` sub-assets directly into `Stage1Input.inputactions`.
   In Unity 6 this corrupts Input System serialization: the asset opens fine
   in the Input Actions editor but fails at startup deserialization ("File
   may be corrupted or was serialized with a newer version of Unity").
   Fix: the finalizer now creates standalone `InputActionReference` .asset
   files under `Assets/Input/References/` and never modifies the
   `.inputactions` JSON.
   User action required: restore the corrupted file from git
   (`git checkout -- Assets/Input/Stage1Input.inputactions`), then re-run
   `Balance Puzzle → Finalize Stage 1 Assets`.
   Retest required: ST-CMP-04 (no startup errors), ST-INT-01–07, ST-ROT-01–05.

5. **BUG-005 (CRITICAL, fixed 2026-10-08, commit 120f46a):** Material `.mat`
   YAML contained a 33-character shader GUID (valid Unity GUIDs are exactly
   32 hex chars), causing "Could not extract GUID in text file ... at line
   11" import failure. Materials fell back to `Hidden/InternalErrorShader`
   (pink), and the finalizer could not repair them because
   `LoadAssetAtPath` failed on the malformed YAML.
   Fix: corrected to valid 32-char placeholder GUIDs in all three `.mat`
   files. The finalizer now reports clear errors if a material still fails
   to load.
   Retest required: ST-CMP-02/03, then re-run Finalize and confirm URP/Lit.

6. **BUG-006 (HIGH, fixed 2026-10-08, commit 120f46a):** `HudController`
   used `Resources.GetBuiltinResource<Font>("Arial.ttf")`, which throws
   `ArgumentException` in Unity 6 ("Arial.ttf is no longer a valid built in
   font"). Fix: use `"LegacyRuntime.ttf"` (verified in Unity 6000.0.4f1).
   Retest required: ST-CMP-04, HUD suite.

7. **BUG-007 (HIGH, OPEN as of 2026-10-08):** Central platform mesh renders
   magenta/pink in Play Mode despite 0 console errors/warnings. The malformed
   shader GUID (BUG-005) was corrected and automatic repair hooks were added
   (`Stage1MaterialPostprocessor`, `Stage1PlayModeMaterialFixer`, commit
   5b4791e), but the pink mesh persisted in actual Play Mode testing.
   Root cause NOT YET PROVEN — no speculative fix until evidence exists.
   Diagnostic instrumentation provided (commit 9bf5cfb, hardened in 98300e4):
   run **Balance Puzzle → Diagnose BUG-007 Materials** in Unity and return
   the `[DIAG-*]` output. It reports Shader.Find result, pipeline
   configuration, each material's loaded shader + raw YAML, the Platform
   renderer's actual materials/shaders, all four stone Visuals, and whether
   the auto-fixers ever ran. Hypotheses (unproven): stale Library/import
   cache, URP pipeline asset not assigned, Shader.Find null, fixer not
   executing, wrong material reference.
   Retest required: full material/visual suite after root cause is proven
   and fixed. Status: **BLOCKED** on diagnostic output.

7. **Robustness hardening (2026-10-08):**
   - `StoneDragger.HandleRelease`: null-guard `levelController` before
     `BeginEvaluation` (HandleSelect already guarded; HandleRelease did not).
   - `LevelController.OnStabilityPassed`: handle `TryLockCandidate` returning
     false — log and fail the evaluation instead of silently soft-locking in
     `Evaluating` state.
   - `HudController` button callbacks: null-guard `levelReset`/`inputReader`
     in Reset/Play-Again/Rotate lambdas so a missing wire logs instead of
     throwing on click.
   - `HudController.RefreshTimerText`: cache the displayed string; only
     assign `Text.text` when the tenth-of-a-second value actually changes
     (reduces per-frame string allocation on mobile).
   - `Stage1AssetFinalizer.LastFixReport`: records whether/when the material
     auto-fixers ran, for the BUG-007 diagnostic.
   No gameplay behavior changes in the normal flow.

5. **C# syntax:** all 17 scripts parsed with a real C# grammar (tree-sitter):
   0 syntax errors.

6. **Scene/prefab YAML:** all 12 asset files — every local fileID resolves,
   every GUID has a .meta, every script reference maps to a real script,
   no duplicate fileIDs. Stone hierarchy verified: root at scale 1,
   BoxCollider sized per config, Visual child scaled per config.

7. **Logic review:** selection/drag/rotation gating (Placing-only, locked-stone
   exclusion, UI pointer rejection), drag plane + radius clamp, release →
   dynamic + BeginEvaluation, calm-time + velocity + impact + killY + COM-support
   evaluation, candidate-only lock, 2/3/5/7 bonuses from config, 150 cap,
   event-driven reset across all systems, single countdown authority
   (LevelTimer), presentation decoupled from locking — all match the approved
   Stage 1 contract. No additional defects found.

8. **Contract compliance:** no future-stage features in code; InputReader is
   the sole gameplay input consumer (HudController touches
   `UnityEngine.InputSystem.UI` only for the EventSystem's UI input module —
   required UI infrastructure, not gameplay input); no `FindObjectOfType` in
   runtime scripts (editor-only finalizer excepted); no hardcoded gameplay
   numbers (all from Stage1Config); all config values match plan §H.

9. **Secrets scan:** PASS (no secrets in new/changed files).
