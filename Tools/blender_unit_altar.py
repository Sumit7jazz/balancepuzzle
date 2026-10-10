"""Headless-Blender unit-altar export (run with blender --background --python).

Sanctioned Stage 2 art path (preferred order #1: Blender bpy standard FBX
export): imports the known-good full-scale altar FBX, normalizes it to a
unit bounding box (scene Platform root carries scale (6, 0.5, 6) x
BoxCollider (1,1,1) — the same unit-mesh convention as the stones), and
exports with standard FBX settings over the asset path. Unity reimports in
place (same path, .meta GUID untouched).

Usage (Git Bash):
  "/c/Program Files/Blender Foundation/Blender 5.2/blender.exe" \
    --background --python Tools/blender_unit_altar.py

Gates (asserted in-script): tris <= 800, bbox == (1,1,1) +/- 1e-3,
centered, has UVs. Runs in an isolated headless process; never touches the
live Blender GUI session.
"""
import bpy
import os

SRC = r"E:\GAME\balancepuzzle\Tools\backup\Platform_Altar.fbx.preunit"
OUT = r"E:\GAME\balancepuzzle\Assets\Art\Platform\Platform_Altar.fbx"

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC)

meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
assert len(meshes) == 1, "expected 1 mesh, got %d" % len(meshes)
obj = meshes[0]
print("imported: %s verts=%d" % (obj.name, len(obj.data.vertices)))

# Authored extents: X +-3 (6), Y +-0.25 (0.5), Z +-3 (6) -> unit box.
obj.scale = (1.0 / 6.0, 1.0 / 0.5, 1.0 / 6.0)
bpy.context.view_layer.objects.active = obj
obj.select_set(True)
bpy.ops.object.transform_apply(scale=True)

ws = [obj.matrix_world @ v.co for v in obj.data.vertices]
xs = [c[0] for c in ws]
ys = [c[1] for c in ws]
zs = [c[2] for c in ws]
size = (max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))
center = ((max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2,
          (max(zs) + min(zs)) / 2)
tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
print("unit size=(%.4f, %.4f, %.4f) center=(%.4f, %.4f, %.4f) tris=%d" %
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
print("BLENDER_UNIT_ALTAR PASS")
