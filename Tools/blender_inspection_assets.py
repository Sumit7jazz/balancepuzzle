"""Stage 3 Milestone 2 — inspection organic assets (headless Blender export).

Overwrite-in-place (GUID-preserving): only .fbx bytes change, .meta untouched
(globalScale:100 stays). Sanctioned Blender standard FBX path per PIPELINE.md.

Usage:
  "/c/Program Files/Blender Foundation/Blender 5.2/blender.exe" \
    --background --python Tools/blender_inspection_assets.py

Gates (asserted in-script per mesh): 400<=tris<=800 (stones), tris<=800 (altar),
unit-ish bbox, centered origin, has UVs. Exports Forward -Z / Up Y / Scale 1.0.
"""
import bpy
import math
import os
import random

ROOT = r"E:\GAME\balancepuzzle"
OUT_STONES = {
    # id: (file, target_tris, scale_xyz, seed, flatten_z, roughness)
    "A": ("Stone_A_Rock.fbx", 600, (1.0, 0.80, 0.72), 101, 0.85, "smoothest compact boulder"),
    "B": ("Stone_B_Rock.fbx", 650, (1.0, 0.72, 0.70), 102, 0.80, "elongated X ridge"),
    "C": ("Stone_C_Rock.fbx", 560, (1.0, 0.55, 0.70), 103, 0.62, "wide flat slab"),
    "D": ("Stone_D_Rock.fbx", 700, (1.0, 0.92, 0.86), 104, 0.90, "massive craggy block"),
}
OUT_ALTAR = "Platform_Altar.fbx"


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_pebble(name, scale_xyz, seed, flatten, target_tris, ridge_axis=False):
    rng = random.Random(seed)
    # Icosphere base gives organic topology; subd 4 (~5120 tris) then decimate down.
    # (Subd 3 + single decimate undershoots: 1280 -> 320 at ratio 0.47, so iterate.)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=4, radius=0.5)
    obj = bpy.context.active_object
    obj.name = name
    mesh = obj.data
    # Displace verts along normal with layered sine + jitter for river-worn look.
    freq = (2.2 + rng.random() * 1.5, 2.0 + rng.random() * 1.5, 2.4 + rng.random() * 1.2)
    phase = (rng.random() * 6.28, rng.random() * 6.28, rng.random() * 6.28)
    amp = 0.055 if "flat" not in name.lower() else 0.035
    for v in mesh.vertices:
        co = v.co
        d = (math.sin(freq[0] * co.x + phase[0])
             * math.sin(freq[1] * co.y + phase[1])
             * math.sin(freq[2] * co.z + phase[2])) * amp
        d += rng.uniform(-0.012, 0.012)
        # Ridge along X for Stone_B
        if ridge_axis:
            d += 0.05 * math.cos(co.y * 6.0) * math.cos(co.z * 6.0)
        n = v.normal
        v.co = (co.x + n.x * d, co.y + n.y * d, co.z + n.z * d)
    # Flatten (river pebble): squash Z then overall scale to target extents.
    for v in mesh.vertices:
        v.co.z *= flatten
    obj.scale = scale_xyz
    bpy.ops.object.transform_apply(scale=True)
    # Smooth + decimate to budget.
    for p in mesh.polygons:
        p.use_smooth = True
    # Decimate ratio from current tri estimate.
    cur_tris = len(mesh.polygons) * 2  # quads/ngons approx; icosphere is all tris
    cur_tris = sum(len(p.vertices) - 2 for p in mesh.polygons)
    ratio = max(0.2, min(1.0, target_tris / max(cur_tris, 1)))
    mod = obj.modifiers.new("Decimate", "DECIMATE")
    mod.ratio = ratio
    mod.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj


def make_altar():
    # Beveled-box river-bed slab, unit bbox, chamfered rim, weathered top.
    bpy.ops.mesh.primitive_cube_add(size=1.0)
    obj = bpy.context.active_object
    obj.name = "Platform_Altar_Mesh"
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.subdivide(number_cuts=6)
    bpy.ops.object.mode_set(mode="OBJECT")
    mesh = obj.data
    rng = random.Random(2001)
    for v in mesh.vertices:
        co = v.co
        # Chamfer: pull rim verts down.
        d = max(abs(co.x), abs(co.y))
        y = co.z
        if d > 0.40:
            y -= (d - 0.40) * 0.35
        # Weathered top noise (only upper verts).
        if co.z > 0:
            y += (math.sin(co.x * 9.0) * math.sin(co.y * 7.0)) * 0.012 + rng.uniform(-0.008, 0.008)
        v.co = (co.x, co.y, y)
    # Bevel modifier for chamfered edges.
    bpy.context.view_layer.objects.active = obj
    mod = obj.modifiers.new("Bevel", "BEVEL")
    mod.width = 0.06
    mod.segments = 2
    bpy.ops.object.modifier_apply(modifier=mod.name)
    for p in mesh.polygons:
        p.use_smooth = True
    # Decimate to <=800.
    tris = sum(len(p.vertices) - 2 for p in mesh.polygons)
    if tris > 600:
        mod2 = obj.modifiers.new("Decimate2", "DECIMATE")
        mod2.ratio = 600.0 / tris
        mod2.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=mod2.name)
    return obj


def finish_obj(obj):
    # Origin to geometric center, Smart UV, triangulate.
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY", center="BOUNDS")
    # Move so origin-centered bbox is at local zero.
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    # Smart UV project.
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=66, island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")
    # Triangulate.
    mod = obj.modifiers.new("Tri", "TRIANGULATE")
    bpy.ops.object.modifier_apply(modifier=mod.name)
    mesh = obj.data
    tris = sum(len(p.vertices) - 2 for p in mesh.polygons)
    # BBox + center check.
    ws = [obj.matrix_world @ v.co for v in mesh.vertices]
    # Local bbox (object at origin, scale applied).
    xs = [c[0] for c in ws]
    ys = [c[1] for c in ws]
    zs = [c[2] for c in ws]
    size = (max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))
    center = ((max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2, (max(zs) + min(zs)) / 2)
    has_uv = len(mesh.uv_layers) > 0
    print("%s tris=%d size=(%.3f,%.3f,%.3f) center=(%.3f,%.3f,%.3f) uvs=%s" % (
        obj.name, tris, size[0], size[1], size[2], center[0], center[1], center[2], has_uv))
    return tris, size, center, has_uv


def export(obj, outpath):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=outpath,
        use_selection=True,
        global_scale=1.0,
        apply_unit_scale=True,
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=False,
        object_types={"MESH"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        use_subsurf=False,
        add_leaf_bones=False,
        use_custom_props=False,
    )
    print(" exported %s bytes=%d" % (outpath, os.path.getsize(outpath)))


def main():
    stone_dir = os.path.join(ROOT, "Assets", "Art", "Stones")
    plat_dir = os.path.join(ROOT, "Assets", "Art", "Platform")
    for sid, (fname, target, scale_xyz, seed, flatten, _desc) in OUT_STONES.items():
        clear_scene()
        ridge = (sid == "B")
        obj = make_pebble("Stone_%s_Rock_Mesh" % sid, scale_xyz, seed, flatten, target, ridge_axis=ridge)
        tris, size, center, has_uv = finish_obj(obj)
        assert 400 <= tris <= 800, (sid, tris)
        assert all(0.4 <= s <= 1.6 for s in size), (sid, size)
        assert all(abs(c) <= 0.05 for c in center), (sid, center)
        assert has_uv, sid
        export(obj, os.path.join(stone_dir, fname))
    clear_scene()
    altar = make_altar()
    tris, size, center, has_uv = finish_obj(altar)
    assert tris <= 800, tris
    assert all(0.5 <= s <= 1.5 for s in size), size
    assert all(abs(c) <= 0.08 for c in center), center
    assert has_uv
    export(altar, os.path.join(plat_dir, OUT_ALTAR))
    print("BLENDER_INSPECTION_ASSETS PASS")


main()
