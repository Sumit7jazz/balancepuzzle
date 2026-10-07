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

## 4. Create the Stage 1 config asset (File Group 1)

1. In the Project window, create `Assets/ScriptableObjects/Stage1/`.
2. Right-click that folder → Create → **Balance Puzzle → Stage 1 Config**.
3. Select it and verify the defaults match `Docs/Stage1/plan.md` section H
   (bonuses 2/3/5/7, timer 120/150, stability 2.0 s, stone masses 1/2/3.5/5, …).

## 5. Later groups

- File Group 2: physics scripts (no extra setup).
- File Group 3: interaction + level scripts (no extra setup).
- File Group 4: scene `Stage1_Sandbox`, prefabs, UI wiring (Inspector setup
  documented in that group's report).
