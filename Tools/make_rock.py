#!/usr/bin/env python3
"""Procedural Stage-2 rock/altar generator → Blender-compatible FBX 7400.

Usage:
  python Tools/make_rock.py B|C|D        # rock into Assets/Art/Stones/Stone_X_Rock.fbx
  python Tools/make_rock.py PLATFORM     # altar into Assets/Art/Platform/Platform_Altar.fbx

Deterministic per spec (fixed seeds). Topology is a deformed closed box
grid -> guaranteed manifold. Normals are flat (faceted), UVs box-projected.
Also writes the .mat + .meta sidecars (mat YAML cloned from Stone_A_Rock.mat
with per-stone tint; deterministic uuid5 GUIDs per repo convention).
"""
import math
import os
import random
import sys
import uuid

sys.path.insert(0, "E:/GAME/balancepuzzle/Tools")
from fbx_write import N, P, write_fbx
from fbx_blender_defs import DOC_PROPS70, MODEL_PT, GEOMETRY_PT

ROOT = "E:/GAME/balancepuzzle"
ART = os.path.join(ROOT, "Assets/Art")
NS = uuid.NAMESPACE_URL


# ---------------------------------------------------------------- mesh core
def build_box_grids(segs):
    """Yield (normal, u, v, center, nu, nv) faces of a unit box. u x v = n."""
    nx, ny, nz = segs
    X, Y, Z = (1, 0, 0), (0, 1, 0), (0, 0, 1)
    nx_ = (-1, 0, 0)
    return [
        ((1, 0, 0), (0, 0, -1), (0, 1, 0), (0.5, 0, 0), nz, ny),
        ((-1, 0, 0), (0, 0, 1), (0, 1, 0), (-0.5, 0, 0), nz, ny),
        ((0, 1, 0), (1, 0, 0), (0, 0, -1), (0, 0.5, 0), nx, nz),
        ((0, -1, 0), (1, 0, 0), (0, 0, 1), (0, -0.5, 0), nx, nz),
        ((0, 0, 1), (1, 0, 0), (0, 1, 0), (0, 0, 0.5), nx, ny),
        ((0, 0, -1), (-1, 0, 0), (0, 1, 0), (0, 0, -0.5), nx, ny),
    ]


def add(a, b, s=1.0):
    return (a[0] + b[0] * s, a[1] + b[1] * s, a[2] + b[2] * s)


def rock_tris(spec):
    # Stage 1: undisplaced grid quads (shared edge verts are bitwise identical).
    quads = []
    for n, u, v, c, nu, nv in build_box_grids(spec['segs']):
        grid = []
        for j in range(nv + 1):
            row = []
            fv = j / nv - 0.5
            for i in range(nu + 1):
                fu = i / nu - 0.5
                row.append(add(add(c, u, fu), v, fv))
            grid.append(row)
        for j in range(nv):
            for i in range(nu):
                quads.append((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    # Stage 2: weld base positions, then displace along averaged normals so
    # shared edge/corner verts move identically (keeps the mesh manifold).
    ids, verts = {}, []
    faces = []  # welded quads
    for a, b, c, d in quads:
        face = []
        for p in (a, b, c, d):
            k = (round(p[0], 9), round(p[1], 9), round(p[2], 9))
            if k not in ids:
                ids[k] = len(verts)
                verts.append(p)
            face.append(ids[k])
        faces.append(tuple(face))
    navg = [[0.0, 0.0, 0.0] for _ in verts]
    for f in faces:
        A, B, C = (verts[i] for i in (f[0], f[1], f[2]))
        u = (B[0] - A[0], B[1] - A[1], B[2] - A[2])
        v = (C[0] - A[0], C[1] - A[1], C[2] - A[2])
        n = cross(u, v)
        for i in f:
            navg[i][0] += n[0]
            navg[i][1] += n[1]
            navg[i][2] += n[2]
    for i, n in enumerate(navg):
        l = math.sqrt(sum(c * c for c in n)) or 1.0
        navg[i] = (n[0] / l, n[1] / l, n[2] / l)
    rng = random.Random(spec['seed'])
    modes = []
    for _ in range(spec['modes']):
        modes.append({
            'f': (rng.uniform(2.0, 4.5), rng.uniform(2.0, 4.5), rng.uniform(2.0, 4.5)),
            'p': (rng.uniform(0, 2 * math.pi), rng.uniform(0, 2 * math.pi), rng.uniform(0, 2 * math.pi)),
            'a': spec['amp'] / spec['modes'],
        })
    jit = random.Random(spec['seed'] * 7 + 1)
    moved = []
    for i, p in enumerate(verts):
        d = sum(m['a'] * math.sin(m['f'][0] * p[0] + m['p'][0]) *
                math.sin(m['f'][1] * p[1] + m['p'][1]) *
                math.sin(m['f'][2] * p[2] + m['p'][2]) for m in modes)
        d += jit.uniform(-spec['jitter'], spec['jitter'])
        n = navg[i]
        moved.append((p[0] + n[0] * d, p[1] + n[1] * d, p[2] + n[2] * d))
    tris = []
    for f in faces:
        A, B, C, D = (moved[i] for i in f)
        tris.append((A, B, C))
        tris.append((A, C, D))
    return tris


def cross(s, t):
    return (s[1] * t[2] - s[2] * t[1], s[2] * t[0] - s[0] * t[2], s[0] * t[1] - s[1] * t[0])


def oriented_quad(a, b, c, d, n):
    """Tris for cyclic quad a->b->c->d with normal facing n."""
    ux, uy, uz = (b[0] - a[0], b[1] - a[1], b[2] - a[2])
    vx, vy, vz = (c[0] - a[0], c[1] - a[1], c[2] - a[2])
    w = cross((ux, uy, uz), (vx, vy, vz))
    if w[0] * n[0] + w[1] * n[1] + w[2] * n[2] >= 0:
        return [(a, b, c), (a, c, d)]
    return [(a, c, b), (a, d, c)]


def altar_tris():
    H = 3.0       # half footprint (matches 6x0.5x6 collider footprint)
    TOP = 0.25    # top plane
    BOT = -0.25   # bottom plane
    N = 10        # top grid segments per side

    def h(x, z):
        y = TOP
        d = max(abs(x), abs(z))
        if d > 2.80:                      # 45-degree chamfer at rim
            y -= (d - 2.80) * 1.0
        r = max(abs(x), abs(z))
        t = min(max((2.30 - r) / 0.15, 0.0), 1.0)
        y -= 0.035 * (t * t * (3 - 2 * t))  # recessed center panel
        return y

    def gx(i):
        return -H + 2 * H * i / N

    top = [[(gx(i), h(gx(i), gx(j)), gx(j)) for i in range(N + 1)] for j in range(N + 1)]
    tris = []
    for j in range(N):
        for i in range(N):
            a, b = top[j][i], top[j][i + 1]
            c, d = top[j + 1][i + 1], top[j + 1][i]
            tris.append((a, c, b))
            tris.append((a, d, c))
    # sides: u = screen-right viewed from outside, v = +Y up
    sides = [(0, 0, -1), (0, 0, 1), (1, 0, 0), (-1, 0, 0)]
    for n in sides:
        pts = []
        for i in range(N + 1):
            if n == (0, 0, -1):
                x, z = H - 2 * H * i / N, -H
            elif n == (0, 0, 1):
                x, z = -H + 2 * H * i / N, H
            elif n == (1, 0, 0):
                x, z = H, H - 2 * H * i / N
            else:
                x, z = -H, -H + 2 * H * i / N
            pts.append((x, h(x, z), z))
        bots = [(x, BOT, z) for x, _, z in pts]
        for i in range(N):
            tris += oriented_quad(bots[i], bots[i + 1], pts[i + 1], pts[i], n)
    # bottom grid (tessellated to weld with the side strips; outward -Y)
    bot = [[(gx(i), BOT, gx(j)) for i in range(N + 1)] for j in range(N + 1)]
    for j in range(N):
        for i in range(N):
            a, b = bot[j][i], bot[j][i + 1]
            c, d = bot[j + 1][i + 1], bot[j + 1][i]
            tris.append((a, b, c))
            tris.append((a, c, d))
    return tris


def unit_altar_tris():
    """Altar triangles normalized to a unit bounding box.

    The scene/prefab Platform root carries scale (6, 0.5, 6) x BoxCollider
    (1,1,1) — the same unit-mesh convention as the stones. A unit-local
    altar x world scale restores the exact authored 6x0.5x6 footprint with
    zero transform/collider churn. (Authored extents: X +-3, Y +-0.25,
    Z +-3, centered.) Scale here — BEFORE build_mesh — so normals/UVs
    derive from final geometry.
    """
    raw = altar_tris()
    return [tuple((p[0] / 6.0, p[1] / 0.5, p[2] / 6.0) for p in tri)
            for tri in raw]
    ids, verts = {}, []
    out = []
    for t in tris:
        tri = []
        for p in t:
            k = (round(p[0], 9), round(p[1], 9), round(p[2], 9))
            if k not in ids:
                ids[k] = len(verts)
                verts.append(p)
            tri.append(ids[k])
        out.append(tuple(tri))
    return verts, out


def signed_volume(verts, tris):
    v = 0.0
    for a, b, c in tris:
        A, B, C = verts[a], verts[b], verts[c]
        v += A[0] * (B[1] * C[2] - B[2] * C[1]) - A[1] * (B[0] * C[2] - B[2] * C[0]) \
            + A[2] * (B[0] * C[1] - B[1] * C[0])
    return v / 6.0


def normalize(verts, target):
    xs = [p[0] for p in verts]
    ys = [p[1] for p in verts]
    zs = [p[2] for p in verts]
    mn, mx = (min(xs), min(ys), min(zs)), (max(xs), max(ys), max(zs))
    out = []
    for p in verts:
        out.append(tuple(
            (p[k] - (mn[k] + mx[k]) / 2) / (mx[k] - mn[k]) * target[k]
            for k in range(3)))
    return out


def face_normal(verts, tri):
    A, B, C = (verts[i] for i in tri)
    u = (B[0] - A[0], B[1] - A[1], B[2] - A[2])
    v = (C[0] - A[0], C[1] - A[1], C[2] - A[2])
    n = (u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0])
    l = math.sqrt(sum(c * c for c in n)) or 1.0
    return (n[0] / l, n[1] / l, n[2] / l)


def build_mesh(tris, target=None, flatten_top=None):
    verts, itris = weld(tris)
    if signed_volume(verts, itris) < 0:
        itris = [(c, b, a) for a, b, c in itris]
    if target is not None:
        verts = normalize(verts, target)
    if flatten_top is not None:
        cy = sum(p[1] for p in verts) / len(verts)
        verts = [(x, cy + (y - cy) * flatten_top if y > cy else y, z)
                 for x, y, z in verts]
        verts = normalize(verts, target)
    xs = [p[0] for p in verts]
    ys = [p[1] for p in verts]
    zs = [p[2] for p in verts]
    bbox = ((min(xs), max(xs)), (min(ys), max(ys)), (min(zs), max(zs)))
    normals, nindex = [], []
    uvmap, uvindex = {}, []
    uvs = []
    for t in itris:
        n = face_normal(verts, t)
        normals.append(n)
        ni = len(normals) - 1
        ax = max(range(3), key=lambda k: abs(n[k]))
        for vi in t:
            nindex.append(ni)
            x, y, z = verts[vi]
            # box projection on the two non-dominant axes
            other = [a for a in range(3) if a != ax]
            c0 = (x, y, z)[other[0]]
            c1 = (x, y, z)[other[1]]
            u = 0.02 + 0.96 * (c0 - bbox[other[0]][0]) / (bbox[other[0]][1] - bbox[other[0]][0])
            v = 0.02 + 0.96 * (c1 - bbox[other[1]][0]) / (bbox[other[1]][1] - bbox[other[1]][0])
            k = (round(u, 6), round(v, 6))
            if k not in uvmap:
                uvmap[k] = len(uvs)
                uvs.append(k)
            uvindex.append(uvmap[k])
    # edges
    eset = set()
    for a, b, c in itris:
        for e in ((a, b), (b, c), (c, a)):
            eset.add(tuple(sorted(e)))
    pvi = []
    for a, b, c in itris:
        pvi += [a, b, -c - 1]
    return {
        'verts': verts, 'tris': itris,
        'flat': [c for t in verts for c in t],
        'pvi': pvi, 'edges': [i for e in sorted(eset) for i in e],
        'normals': [c for n in normals for c in n],
        'nindex': nindex,
        'uvs': [c for uv in uvs for c in uv],
        'uvindex': uvindex,
    }


# ---------------------------------------------------------------- FBX assembly
def assemble(mesh, mesh_name, model_name, geo_id, model_id, doc_id):
    vert = mesh['flat']
    geo = N('Geometry', [('I', geo_id), ('S', mesh_name + '\x00\x01Geometry'), ('S', 'Mesh')], [
        N('Properties70'),
        N('GeometryVersion', [('I', 124)]),
        N('Vertices', [('d', vert)]),
        N('PolygonVertexIndex', [('i', mesh['pvi'])]),
        N('Edges', [('i', mesh['edges'])]),
        N('LayerElementSmoothing', [('I', 0)], [
            N('Version', [('I', 102)]),
            N('Name', [('S', '')]),
            N('MappingInformationType', [('S', 'ByPolygon')]),
            N('ReferenceInformationType', [('S', 'Direct')]),
            N('Smoothing', [('i', [0] * (len(mesh['pvi']) // 3))]),
        ]),
        N('LayerElementNormal', [('I', 0)], [
            N('Version', [('I', 101)]),
            N('Name', [('S', '')]),
            N('MappingInformationType', [('S', 'ByPolygonVertex')]),
            N('ReferenceInformationType', [('S', 'IndexToDirect')]),
            N('Normals', [('d', mesh['normals'])]),
            N('NormalsIndex', [('i', mesh['nindex'])]),
        ]),
        N('LayerElementUV', [('I', 0)], [
            N('Version', [('I', 101)]),
            N('Name', [('S', 'UVMap')]),
            N('MappingInformationType', [('S', 'ByPolygonVertex')]),
            N('ReferenceInformationType', [('S', 'IndexToDirect')]),
            N('UV', [('d', mesh['uvs'])]),
            N('UVIndex', [('i', mesh['uvindex'])]),
        ]),
        N('Layer', [('I', 0)], [
            N('Version', [('I', 100)]),
            N('LayerElement', [], [
                N('Type', [('S', 'LayerElementNormal')]),
                N('TypedIndex', [('I', 0)]),
            ]),
            N('LayerElement', [], [
                N('Type', [('S', 'LayerElementSmoothing')]),
                N('TypedIndex', [('I', 0)]),
            ]),
            N('LayerElement', [], [
                N('Type', [('S', 'LayerElementUV')]),
                N('TypedIndex', [('I', 0)]),
            ]),
        ]),
    ])
    model = N('Model', [('I', model_id), ('S', model_name + '\x00\x01Model'), ('S', 'Mesh')], [
        N('Version', [('I', 232)]),
        N('Properties70', [], [
            P('Lcl Rotation', 'Lcl Rotation', '', 'A',
              ('D', -90.0), ('D', 0.0), ('D', 0.0)),
            P('Lcl Scaling', 'Lcl Scaling', '', 'A',
              ('D', 100.0), ('D', 100.0), ('D', 100.0)),
            P('DefaultAttributeIndex', 'int', 'Integer', '', ('I', 0)),
            P('InheritType', 'enum', '', '', ('I', 1)),
        ]),
        N('MultiLayer', [('I', 0)]),
        N('MultiTake', [('I', 0)]),
        N('Shading', [('C', True)]),
        N('Culling', [('S', 'CullingOff')]),
    ])
    fileid = uuid.uuid5(NS, 'balancepuzzle:fileid:' + mesh_name).bytes
    top = [
        N('FBXHeaderExtension', [], [
            N('FBXHeaderVersion', [('I', 1003)]),
            N('FBXVersion', [('I', 7400)]),
            N('EncryptionType', [('I', 0)]),
            N('CreationTimeStamp', [], [
                N('Version', [('I', 1000)]),
                N('Year', [('I', 2026)]),
                N('Month', [('I', 1)]),
                N('Day', [('I', 1)]),
                N('Hour', [('I', 0)]),
                N('Minute', [('I', 0)]),
                N('Second', [('I', 0)]),
                N('Millisecond', [('I', 0)]),
            ]),
            N('Creator', [('S', 'BalancePuzzle procedural rock generator (FBX 7400)')]),
            N('SceneInfo', [('S', 'GlobalInfo\x00\x01SceneInfo'), ('S', 'UserData')], [
                N('Type', [('S', 'UserData')]),
                N('Version', [('I', 100)]),
                N('MetaData', [], [
                    N('Version', [('I', 100)]),
                    N('Title', [('S', '')]),
                    N('Subject', [('S', '')]),
                    N('Author', [('S', '')]),
                    N('Keywords', [('S', '')]),
                    N('Revision', [('S', '')]),
                    N('Comment', [('S', '')]),
                ]),
                N('Properties70', [], [
                    P('DocumentUrl', 'KString', '', '', ('S', '')),
                    P('SrcDocumentUrl', 'KString', '', '', ('S', '')),
                    P('Original', 'Compound', '', '', ),
                    P('Original|ApplicationVendor', 'KString', '', '', ('S', '')),
                    P('Original|ApplicationName', 'KString', '', '', ('S', '')),
                    P('Original|ApplicationVersion', 'KString', '', '', ('S', '')),
                    P('Original|DateTime_GMT', 'DateTime', '', '', ('S', '')),
                    P('Original|FileName', 'KString', '', '', ('S', '')),
                    P('LastSaved', 'Compound', '', '', ),
                    P('LastSaved|ApplicationVendor', 'KString', '', '', ('S', '')),
                    P('LastSaved|ApplicationName', 'KString', '', '', ('S', '')),
                    P('LastSaved|ApplicationVersion', 'KString', '', '', ('S', '')),
                    P('LastSaved|DateTime_GMT', 'DateTime', '', '', ('S', '')),
                ]),
            ]),
        ]),
        N('FileId', [('R', fileid)]),
        N('CreationTime', [('S', '1970-01-01 10:00:00:000')]),
        N('Creator', [('S', 'BalancePuzzle procedural rock generator (FBX 7400)')]),
        N('GlobalSettings', [], [
            N('Version', [('I', 1000)]),
            N('Properties70', [], [
                P('UpAxis', 'int', 'Integer', '', ('I', 1)),
                P('UpAxisSign', 'int', 'Integer', '', ('I', 1)),
                P('FrontAxis', 'int', 'Integer', '', ('I', 2)),
                P('FrontAxisSign', 'int', 'Integer', '', ('I', 1)),
                P('CoordAxis', 'int', 'Integer', '', ('I', 0)),
                P('CoordAxisSign', 'int', 'Integer', '', ('I', 1)),
                P('OriginalUpAxis', 'int', 'Integer', '', ('I', -1)),
                P('OriginalUpAxisSign', 'int', 'Integer', '', ('I', 1)),
                P('UnitScaleFactor', 'double', 'Number', '', ('D', 1.0)),
                P('OriginalUnitScaleFactor', 'double', 'Number', '', ('D', 1.0)),
                P('AmbientColor', 'ColorRGB', 'Color', '', ('F', 0.0), ('F', 0.0), ('F', 0.0)),
                P('DefaultCamera', 'KString', '', '', ('S', 'Producer Perspective')),
                P('TimeMode', 'enum', '', '', ('I', 11)),
                P('TimeSpanStart', 'KTime', 'Time', '', ('L', 0)),
                P('TimeSpanStop', 'KTime', 'Time', '', ('L', 46186158000)),
                P('CustomFrameRate', 'double', 'Number', '', ('D', 24.0)),
            ]),
        ]),
        N('Documents', [], [
            N('Count', [('I', 1)]),
            N('Document', [('L', doc_id), ('S', 'Scene'), ('S', 'Scene')], [
                N('Properties70', [], DOC_PROPS70),
                N('RootNode', [('L', 0)]),
            ]),
        ]),
        N('References'),
        N('Definitions', [], [
            N('Version', [('I', 100)]),
            N('Count', [('I', 3)]),
            N('ObjectType', [('S', 'GlobalSettings')], [
                N('Count', [('I', 1)]),
            ]),
            N('ObjectType', [('S', 'Model')], [
                N('Count', [('I', 1)]),
                N('PropertyTemplate', [('S', 'FbxNode')], MODEL_PT),
            ]),
            N('ObjectType', [('S', 'Geometry')], [
                N('Count', [('I', 1)]),
                N('PropertyTemplate', [('S', 'FbxMesh')], GEOMETRY_PT),
            ]),
        ]),
        N('Objects', [], [geo, model]),
        N('Connections', [], [
            N('C', [('S', 'OO'), ('L', model_id), ('L', 0)]),
            N('C', [('S', 'OO'), ('L', geo_id), ('L', model_id)]),
        ]),
        N('Takes', [], [
            N('Current', [('S', '')]),
        ]),
    ]
    return top


# ---------------------------------------------------------------- sidecars
def guid_for(name):
    return uuid.uuid5(NS, 'balancepuzzle:' + name).hex


def write_sidecars(out_fbx, asset_name, tint):
    base = os.path.splitext(out_fbx)[0]
    fbx_guid = guid_for(os.path.basename(out_fbx))
    with open(os.path.join(ART, 'Stones', 'Stone_A_Rock.fbx.meta')) as f:
        meta = f.read()
    meta = meta.replace('93c85199cb7fba745a417882c7522e39', fbx_guid)
    with open(out_fbx + '.meta', 'w') as f:
        f.write(meta)
    with open(os.path.join(ART, 'Stones', 'Stone_A_Rock.mat')) as f:
        mat = f.read()
    mat_guid = guid_for(os.path.basename(base) + '.mat')
    import re
    mat = mat.replace('Stone_A_Rock', asset_name)
    mat = re.sub(r'm_Shader: \{fileID: \d+, guid: [0-9a-f]+,',
                 'm_Shader: {fileID: 4800000, guid: 933532a4fcc9baf4fa0491de14d08ed7,', mat)
    mat = re.sub(r'- _BaseColor: \{r: [\d.]+, g: [\d.]+, b: [\d.]+, a: 1\}',
                 '- _BaseColor: {r: %.2f, g: %.2f, b: %.2f, a: 1}' % tint, mat)
    mat = re.sub(r'- _Color: \{r: [\d.]+, g: [\d.]+, b: [\d.]+, a: 1\}',
                 '- _Color: {r: %.2f, g: %.2f, b: %.2f, a: 1}' % tint, mat)
    with open(base + '.mat', 'w') as f:
        f.write(mat)
    with open(os.path.join(ART, 'Stones', 'Stone_A_Rock.mat.meta')) as f:
        mmeta = f.read()
    # file meta guid is on the `guid: ` line: replace with the new mat guid
    lines = mmeta.split('\n')
    for i, ln in enumerate(lines):
        if ln.startswith('guid: '):
            lines[i] = 'guid: ' + mat_guid
            break
    with open(base + '.mat.meta', 'w') as f:
        f.write('\n'.join(lines))
    return fbx_guid, mat_guid


SPECS = {
    # segs, target extents, sine modes, amplitude, jitter, seed, top-flatten
    'B': {'segs': (6, 5, 5), 'target': (1.0, 0.72, 0.72), 'modes': 3,
          'amp': 0.10, 'jitter': 0.010, 'seed': 1202, 'flatten': None,
          'tint': (0.72, 0.68, 0.62), 'geo': 310000011, 'model': 410000011, 'doc': 110000011},
    'C': {'segs': (7, 4, 5), 'target': (1.0, 0.55, 0.72), 'modes': 3,
          'amp': 0.05, 'jitter': 0.006, 'seed': 1203, 'flatten': 0.92,
          'tint': (0.80, 0.77, 0.71), 'geo': 310000012, 'model': 410000012, 'doc': 110000012},
    'D': {'segs': (6, 6, 6), 'target': (1.0, 0.92, 0.88), 'modes': 5,
          'amp': 0.14, 'jitter': 0.014, 'seed': 1204, 'flatten': None,
          'tint': (0.66, 0.62, 0.57), 'geo': 310000013, 'model': 410000013, 'doc': 110000013},
}


def main():
    what = sys.argv[1]
    if what == 'PLATFORM':
        # NOTE: raw make_rock FBX output is NOT reliably imported by Unity
        # 6000.6 (ImportFBX Errors, mesh sub-asset missing — observed
        # 2026-10-09). The sanctioned path is Blender standard FBX export
        # (headless bpy roundtrip of this unit geometry); see PIPELINE.md.
        # This branch preserves the deterministic generator for that input.
        unit = unit_altar_tris()
        mesh = build_mesh(unit, target=None)
        out = os.path.join(ART, 'Platform', 'Platform_Altar.fbx')
        os.makedirs(os.path.dirname(out), exist_ok=True)
        top = assemble(mesh, 'Platform_Altar_Mesh', 'Platform_Altar', 310000021, 410000021, 110000021)
        write_fbx(out, top)
        fg, mg = write_sidecars(out, 'Platform_Altar', (0.70, 0.69, 0.66))
        # folder meta for Platform/
        fmeta = os.path.join(ART, 'Platform.meta')
        if not os.path.exists(fmeta):
            with open(os.path.join(ART, 'Stones.meta')) as f:
                fm = f.read()
            lines = fm.split('\n')
            for i, ln in enumerate(lines):
                if ln.startswith('guid: '):
                    lines[i] = 'guid: ' + guid_for('Platform/')
                    break
            with open(fmeta, 'w') as f:
                f.write('\n'.join(lines))
        print('wrote %s tris=%d fbx_guid=%s mat_guid=%s' %
              (out, len(mesh['pvi']) // 3, fg, mg))
        return
    spec = SPECS[what]
    mesh = build_mesh(rock_tris(spec), target=spec['target'], flatten_top=spec['flatten'])
    out = os.path.join(ART, 'Stones', 'Stone_%s_Rock.fbx' % what)
    top = assemble(mesh, 'Stone_%s_Rock_Mesh' % what, 'Stone_%s_Rock' % what,
                   spec['geo'], spec['model'], spec['doc'])
    write_fbx(out, top)
    fg, mg = write_sidecars(out, 'Stone_%s_Rock' % what, spec['tint'])
    print('wrote %s verts=%d tris=%d fbx_guid=%s mat_guid=%s' %
          (out, len(mesh['verts']), len(mesh['pvi']) // 3, fg, mg))


if __name__ == '__main__':
    main()
