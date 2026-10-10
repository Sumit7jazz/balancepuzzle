#!/usr/bin/env python3
"""Minimal binary-FBX mesh inspector (stdlib only).

Usage: python Tools/fbx_inspect.py <file.fbx>

Reports per-geometry: vertex/face counts, tri/quad/ngon split,
triangulated-triangle estimate, bounding box + size + center offset,
UV presence/range, normal presence, and edge-manifold analysis
(boundary / non-manifold edge counts). Exit 0 if all PIPELINE.md
gates pass (tris<=800, ~1m bbox, UVs, normals, manifold), else 1.
"""
import struct
import sys
import zlib


def read_node(data, off, fbx_version):
    if fbx_version >= 7500:
        end_off, num_props, prop_len = struct.unpack_from('<QQQ', data, off)
        off += 24
    else:
        end_off, num_props, prop_len = struct.unpack_from('<III', data, off)
        off += 12
    if end_off == 0:
        return None, off + (13 if fbx_version < 7500 else 25)
    name_len = data[off]
    off += 1
    name = data[off:off + name_len].decode('ascii', 'replace')
    off += name_len
    props = []
    for _ in range(num_props):
        t = chr(data[off])
        off += 1
        if t == 'Y':
            props.append(struct.unpack_from('<h', data, off)[0]); off += 2
        elif t == 'C':
            props.append(bool(data[off])); off += 1
        elif t == 'I':
            props.append(struct.unpack_from('<i', data, off)[0]); off += 4
        elif t == 'F':
            props.append(struct.unpack_from('<f', data, off)[0]); off += 4
        elif t == 'D':
            props.append(struct.unpack_from('<d', data, off)[0]); off += 8
        elif t == 'L':
            props.append(struct.unpack_from('<q', data, off)[0]); off += 8
        elif t in 'fdlbi':
            n, enc, clen = struct.unpack_from('<III', data, off); off += 12
            raw = data[off:off + clen]; off += clen
            if enc == 1:
                raw = zlib.decompress(raw)
            fmt = {'f': 'f', 'd': 'd', 'l': 'q', 'i': 'i', 'b': 'B'}[t]
            props.append(list(struct.unpack('<%d%s' % (n, fmt), raw)))
        elif t == 'S':
            ln = struct.unpack_from('<I', data, off)[0]; off += 4
            props.append(data[off:off + ln].decode('ascii', 'replace')); off += ln
        elif t == 'R':
            ln = struct.unpack_from('<I', data, off)[0]; off += 4
            props.append(data[off:off + ln]); off += ln
        else:
            raise ValueError('unknown prop type %r' % t)
    children = []
    list_end = end_off - (13 if fbx_version < 7500 else 25)
    while off < list_end:
        child, off = read_node(data, off, fbx_version)
        if child is None:
            break
        children.append(child)
    off = end_off
    return {'name': name, 'props': props, 'children': children}, off


def kids(node, name):
    return [c for c in node['children'] if c['name'] == name]


def find(node, path):
    cur = [node]
    for p in path:
        nxt = []
        for n in cur:
            nxt.extend(kids(n, p))
        cur = nxt
    return cur


def analyze_geometry(geo):
    rid = geo['props'][0] if geo['props'] else '?'
    verts, pvi, uvs, normals = [], [], [], []
    for v in find(geo, ['Vertices']):
        verts = v['props'][0]
    for p in find(geo, ['PolygonVertexIndex']):
        pvi = p['props'][0]
    for lay in kids(geo, 'LayerElementUV'):
        for u in kids(lay, 'UV'):
            uvs = u['props'][0]
    for lay in kids(geo, 'LayerElementNormal'):
        for nm in kids(lay, 'Normals'):
            normals = nm['props'][0]
    # split polygons
    polys, cur = [], []
    for idx in pvi:
        if idx < 0:
            cur.append(-idx - 1)
            polys.append(cur)
            cur = []
        else:
            cur.append(idx)
    nverts = len(verts) // 3
    sizes = [len(p) for p in polys]
    ntri = sum(1 for s in sizes if s == 3)
    nquad = sum(1 for s in sizes if s == 4)
    nngon = sum(1 for s in sizes if s > 4)
    tri_est = sum(max(s - 2, 1) for s in sizes)
    xs, ys, zs = verts[0::3], verts[1::3], verts[2::3]
    bbox = ((min(xs), max(xs)), (min(ys), max(ys)), (min(zs), max(zs))) if verts else None
    size = tuple(b[1] - b[0] for b in bbox) if bbox else (0, 0, 0)
    center = tuple((b[0] + b[1]) / 2 for b in bbox) if bbox else (0, 0, 0)
    # edge manifold
    edge_count = {}
    for p in polys:
        m = len(p)
        for i in range(m):
            e = tuple(sorted((p[i], p[(i + 1) % m])))
            edge_count[e] = edge_count.get(e, 0) + 1
    boundary = sum(1 for c in edge_count.values() if c == 1)
    nonman = sum(1 for c in edge_count.values() if c > 2)
    uv_count = len(uvs) // 2
    uv_range = None
    if uvs:
        uu, vv = uvs[0::2], uvs[1::2]
        uv_range = ((min(uu), max(uu)), (min(vv), max(vv)))
    return {
        'id': rid, 'nverts': nverts, 'nfaces': len(polys),
        'ntris': ntri, 'nquads': nquad, 'nngons': nngon,
        'tri_est': tri_est, 'bbox': bbox, 'size': size, 'center': center,
        'boundary_edges': boundary, 'nonmanifold_edges': nonman,
        'uv_count': uv_count, 'uv_range': uv_range,
        'normal_count': len(normals) // 3,
    }


def main():
    path = sys.argv[1]
    # optional: --size W H D [TOL] overrides the ~1m stone bbox gate
    # (e.g. the 6x0.5x6 platform altar).
    exp_size, tol = None, 1e-3
    if '--size' in sys.argv:
        k = sys.argv.index('--size')
        exp_size = tuple(float(v) for v in sys.argv[k + 1:k + 4])
        rest = sys.argv[k + 4:]
        if rest and not rest[0].startswith('--'):
            tol = float(rest[0])
    data = open(path, 'rb').read()
    assert data[:20] == b'Kaydara FBX Binary  ', 'not a binary FBX'
    version = struct.unpack_from('<I', data, 23)[0]
    print('FBX version: %d, bytes: %d' % (version, len(data)))
    off = 27
    top = []
    while off < len(data):
        node, off = read_node(data, off, version)
        if node is None:
            if off >= len(data):
                break
            continue
        top.append(node)
    root = {'name': '', 'props': [], 'children': top}
    for gs in find(root, ['GlobalSettings', 'Properties70', 'P']):
        if gs['props'] and gs['props'][0] == 'UnitScaleFactor':
            print('UnitScaleFactor: %s' % gs['props'][-1])
    geos = find(root, ['Objects', 'Geometry'])
    print('geometries: %d' % len(geos))
    ok_all = True
    for g in geos:
        a = analyze_geometry(g)
        print('--- Geometry %s ---' % a['id'])
        print('verts=%d faces=%d (tri=%d quad=%d ngon=%d) tri_est=%d' % (
            a['nverts'], a['nfaces'], a['ntris'], a['nquads'], a['nngons'], a['tri_est']))
        print('bbox_min=(%.4f, %.4f, %.4f) bbox_max=(%.4f, %.4f, %.4f)' % (
            a['bbox'][0][0], a['bbox'][1][0], a['bbox'][2][0],
            a['bbox'][0][1], a['bbox'][1][1], a['bbox'][2][1]))
        print('size=(%.4f, %.4f, %.4f) center=(%.4f, %.4f, %.4f)' % (
            a['size'] + a['center']))
        print('boundary_edges=%d nonmanifold_edges=%d' % (
            a['boundary_edges'], a['nonmanifold_edges']))
        print('uvs=%d range=%s normals=%d' % (
            a['uv_count'], a['uv_range'], a['normal_count']))
        if exp_size is None:
            bbox_ok = all(0.5 <= s <= 2.0 for s in a['size'])
            bbox_label = '~1m bbox (all axes 0.5..2.0)'
        else:
            bbox_ok = all(abs(s - e) <= tol for s, e in zip(a['size'], exp_size))
            bbox_label = 'bbox==(%s) tol=%s' % (', '.join(str(e) for e in exp_size), tol)
        checks = [
            ('tris<=800', a['tri_est'] <= 800),
            (bbox_label, bbox_ok),
            ('centered (|c|<=0.15 each)', all(abs(c) <= 0.15 for c in a['center'])),
            ('has UVs', a['uv_count'] > 0),
            ('UVs in 0..1', bool(a['uv_range']) and 0.0 <= a['uv_range'][0][0]
             and a['uv_range'][0][1] <= 1.0 and 0.0 <= a['uv_range'][1][0]
             and a['uv_range'][1][1] <= 1.0),
            ('has normals', a['normal_count'] > 0),
            ('closed manifold (boundary=0, nonman=0)',
             a['boundary_edges'] == 0 and a['nonmanifold_edges'] == 0),
        ]
        for label, passed in checks:
            print('[%s] %s' % ('PASS' if passed else 'FAIL', label))
            ok_all &= passed
    print('ALL GATES PASS' if ok_all else 'GATES FAILING')
    return 0 if ok_all else 1


if __name__ == '__main__':
    sys.exit(main())
