# Stage 1 — Unity Setup (do once)

This repo is a Unity project root. Open it in **Unity 6 LTS (6000.x)**.

## 1. Open the project

1. Clone the repo and check out the working branch.
2. Unity Hub → Add → select the cloned folder → open with a Unity 6000.x editor.
3. Unity generates `ProjectSettings/`, `Packages/`, `Library/` on first open.

## 2. Install packages (File Group 1)

Window → Package Manager → `+` → Add package by name:

- `com.unity.render-pipelines.universal` (17.x — Universal RP)
- `com.unity.inputsystem` (Input System)

After the Input System installs, accept the editor restart prompt.
Then: Edit → Project Settings → Player → Active Input Handling →
**Input System Package (New)** (restart again if prompted).

Do NOT install anything else (no Netcode, Ads, IAP, Cloud Save, analytics).

## 3. Physics project settings (File Group 1)

Edit → Project Settings → Physics:

- **Solver Iterations:** `8` (slightly above default, for stable stacking)
- Everything else: defaults (Gravity −9.81, Fixed Timestep 0.0166667 = 1/60).

## 4. Stage 1 config asset (File Group 4)

The config asset is now pre-created in the repository at
`Assets/ScriptableObjects/Stage1/Stage1Config.asset` with all approved Stage 1
defaults (bonuses 2/3/5/7, timer 120/150, stability 2.0 s, stone masses
1/2/3.5/5, …). Select it and verify the values match `Docs/Stage1/plan.md`
section H. No manual creation needed.

## 5. Finalize Stage 1 assets (File Group 4, one-time)

1. Open the scene `Assets/Scenes/Stage1_Sandbox.unity`.
2. **If you previously ran an older version of the finalizer** (before
   2026-10-08) and `Stage1Input.inputactions` fails to load at startup,
   restore the clean asset first:
   `git checkout -- Assets/Input/Stage1Input.inputactions`
3. Run the menu item **Balance Puzzle → Finalize Stage 1 Assets**.
   This creates standalone `InputActionReference` assets under
   `Assets/Input/References/`, wires `InputReader`'s three action
   references, and ensures the Stage 1 materials use the URP Lit shader.
4. Save the scene (Ctrl/Cmd+S).
5. Verify: 0 console errors; the three materials show **URP/Lit** (not pink);
   entering Play Mode produces no input-asset load failure.

## 6. Later groups

- File Group 2: physics scripts (no extra setup).
- File Group 3: interaction + level scripts (no extra setup).
- File Group 4: scene `Stage1_Sandbox`, prefabs, UI wiring (Inspector setup
  documented in that group's report).
