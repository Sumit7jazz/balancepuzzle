"""Headless-Blender spawn-pad slab export (run with blender --background --python).

Stage 2 fix for the built-in-Cube identity-matrix rendering anomaly
(spawn-pad MeshRenderers submit the built-in Cube mesh at the origin
instead of their transforms; imported meshes render correctly — proven
2026-10-09 by mesh-swap test). A unit beveled box follows the project's
unit-mesh convention: pad roots carry scale (0.6, 0.1, 0.6), so a unit
slab yields the exact 0.6x0.1x0.6 footprint with zero transform/collider
churn. Pads keep the existing dark Platform.mat (no new material).

Usage (Git Bash):
  "/c/Program Files/Blender Foundation/Blender 5.2/blender.exe" \
    --background --python Tools/blender_pad_slab.py

Gates (asserted in-script): tris <= 800, bbox == (1,1,1) +/- 1e-3,
centered, has UVs. Isolated headless process; never touches the live
Blender GUI session.
"""
import bpy
import os

OUT = r"E:\GAME\balancepuzzle\Assets\Art\Platform\SpawnPad_Slab.fbx"

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.0, 0.0))
obj = bpy.context.active_object
assert obj is not None and obj.type == 'MESH'
obj.name = "SpawnPad_Slab"
obj.data.name = "SpawnPad_Slab"
bev = obj.modifiers.new("PadBevel", "BEVEL")
bev.width = 0.06
bev.segments = 2
bev.limit_method = 'ANGLE'
bpy.ops.object.modifier_apply(modifier=bev.name)
# Triangulate before export: Unity 6000.6 FBX import rejects this file in
# quad form ("File is corrupted"); the proven altar file is all-tris.
tri = obj.modifiers.new("PadTriangulate", "TRIANGULATE")
bpy.ops.object.modifier_apply(modifier=tri.name)

ws = [obj.matrix_world @ v.co for v in obj.data.vertices]
xs = [c[0] for c in ws]
ys = [c[1] for c in ws]
zs = [c[2] for c in ws]
size = (max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))
center = ((max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2,
          (max(zs) + min(zs)) / 2)
tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
print("pad size=(%.4f, %.4f, %.4f) center=(%.4f, %.4f, %.4f) tris=%d" %
      (size + center + (tris,)))
assert tris <= 800, tris
assert all(abs(s - 1.0) <= 1e-3 for s in size), size
assert all(abs(c) <= 1e-3 for c in center), center
assert len(obj.data.uv_layers) > 0, "no UVs"

bpy.ops.export_scene.fbx(
    filepath=OUT,
    use_selection=False,
    global_scale=1.0,
    apply_unit_scale=True,
    axis_forward='-Z',
    axis_up='Y',
    bake_space_transform=False,
    object_types={'MESH'},
    use_mesh_modifiers=True,
    mesh_smooth_type='FACE',
    use_subsurf=False,
    use_armature_deform_only=False,
    add_leaf_bones=False,
    use_custom_props=False,
)
print(" exported bytes=%d" % os.path.getsize(OUT))
print("BLENDER_PAD_SLAB PASS")
