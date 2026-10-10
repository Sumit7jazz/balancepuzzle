# NEXT STAGE — REAL GAME PRESENTATION / ENVIRONMENT: Art Pipeline

Status: LANDED + VERIFIED 2026-10-09 (live Unity + headless Blender 5.2). Stones validated as-is;
Platform_Altar + SpawnPad_Slab integrated via the Blender path below. Full outcomes:
`Docs/STAGE2_MILESTONE_LOG.md`. Original plan preserved below for reference.

## Verified pipeline notes (2026-10-09 — read before regenerating anything)

- Sanctioned path: headless Blender standard FBX export —
  `"Blender 5.2/blender.exe" --background --python Tools/blender_unit_altar.py`
  (altar) and `Tools/blender_pad_slab.py` (pads). In-script gates assert tris/bbox/center/UVs.
- New Blender-exported FBX files REQUIRE `globalScale: 100` in their `.fbx.meta`
  (with `useFileUnits: 1`); Unity auto-generates `globalScale: 1` → mesh imports at 0.01 scale.
  (Matches the existing `make_rock` sidecar convention.)
- Raw `make_rock.py` FBX output is NOT Unity-importable (ImportFBX "File is corrupted", no mesh
  sub-asset). `make_rock` remains the deterministic geometry source (`unit_altar_tris()` shared
  with `clone_fbx.py`); Blender export is the delivery step. Keep `Tools/backup/` recovery copies.
- Built-in Cube meshes render at the origin in this project (identity matrices) — never use them
  for visible scene geometry; always assign an imported mesh (proven by mesh-swap test).

## Iron architecture rule

New assets become the VISUAL representation beneath the existing stone roots ONLY:

- KEEP untouched: stone root GameObjects, Rigidbody, BoxCollider sizes, `StoneState`,
  COM offsets, `StepLockManager`, `StabilityEvaluator`, selectors/draggers/rotators,
  timer/reset/HUD/controller/input, all gameplay events and contracts.
- CHANGE per stone: only `Stone_X/Visual` child's `MeshFilter.sharedMesh` (+ stone
  material assignment on its `MeshRenderer`). Never add a second physics authority,
  never move gameplay state into art assets, never rescale roots or colliders.

## One-stone-first pipeline (do Stone_A end-to-end, verify, then B/C/D)

1. Model ONE rock in Blender, natural silhouette derived from a subdivided cube or
   icosahedron with limited random displacement + flat-ish shading facets.
2. Author NORMALIZED to a unit bounding box (~1 m, origin at volumetric center) so
   existing Visual scales (config sizes) keep working unchanged.
3. Budget: 300–800 triangles per stone (mobile-friendly); clean manifold geometry,
   unwrapped UVs (single 0–1 island set is enough for plain stone).
4. Export FBX (centimeters→meters, -Z forward / +Y up), drop into
   `Assets/Art/Stones/` as `Stone_A_Rock.fbx` (+ material `Stone_A_Rock.mat`,
   URP/Lit, opaque, roughness ~0.9).
5. Integrate: assign mesh to `Stone_A/Visual` MeshFilter, material to its renderer.
6. Verify (editor-side, no gameplay change): Health Check passes, probe shows same
   transforms with corrected bounds after the render-blocker recovery, stone still
   rests/selects/drags. Only then batch B/C/D.

## Four stone specs (sizes from `Stage1Config`, silhouettes distinct)

| Stone | Config size (Visual scale, do not change) | Silhouette direction |
|---|---|---|
| A | (0.8, 0.8, 0.8), mass 1.0 | compact rounded boulder, smoothest |
| B | (1.0, 0.8, 0.8), mass 2.0, COM +x | elongated ridge along X, angular |
| C | (1.2, 0.7, 0.7), mass 3.5 | wide flat slab, stacked look |
| D | (1.4, 0.8, 0.8), mass 5.0 | massive craggy block, heaviest read |

Tint materials subtly per stone (warm grey family, keep beige A readable) so steps
are visually obvious. Locked state keeps working via existing `StoneLockPresenter`
material swap — verify the swap still reads clearly on rock geometry.

## Platform / environment / light / camera

- Platform/altar: replace the 6×0.5×6 cube visual with a carved stone altar slab of
  IDENTICAL footprint (collider untouched); simple trim edge is enough.
- Background: minimal gradient/skydome + soft ground disc far outside play area;
  nothing with colliders, nothing tall near the camera frustum edges.
- Lighting: keep single directional + ambient; keep shadows ON (already `shadow=On`
  everywhere); re-tune intensity only if rocks read too dark.
- Camera: keep transform/FOV (composition already verified by frustum math); revisit
  only after art lands and the render blocker clears.

## Verification gates (static/editor only until blocker clears)

- No root/collider/Rigidbody/COM/prefab-reference edits in the diff (art = Visual
  meshes + materials + environment dress only).
- Tri count per stone ≤ 800; all materials opaque URP/Lit; no new physics layers.
- Health Check 0 fail / 0 warn; regression script exit 0; console clean.
- Play Mode visual sign-off explicitly deferred to post-blocker run.

## Out of scope (do not start)

Login/OTP/social/friends/teams/sharing/monetization/IAP/ads/seasons/backend/online.
