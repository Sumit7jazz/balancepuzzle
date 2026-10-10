#!/usr/bin/env python3
"""Regenerate Stone B/C/D + Platform_Altar FBX by cloning Stone_A's proven
binary payload and replacing ONLY the mesh buffer arrays.

Preserved bit-for-bit from Stone_A_Rock.fbx (working Blender 5.2 export):
headers, doc props, templates, connections, node framing, L-typed object IDs,
and the 171-byte footer. Replaced per target: Geometry/Model names, FileId,
Vertices, PolygonVertexIndex, Edges, Smoothing, Normals, NormalsIndex, UV,
UVIndex (all computed deterministically by make_rock).

Usage:
  python Tools/clone_fbx.py [B] [C] [D] [PLATFORM]   (default: all four)

Backs up existing targets to Tools/backup/ before overwriting.
Unity .meta GUIDs are untouched (same file paths), so Unity reimports in place.
"""
import copy
import os
import shutil
import struct
import sys
import uuid
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from fbx_write import ser_node  # noqa: E402
import make_rock  # noqa: E402

ROOT = make_rock.ROOT
ART = make_rock.ART
BACKUP = os.path.join(HERE, 'backup')
NS = uuid.NAMESPACE_URL

STONE_A = os.path.join(ART, 'Stones', 'Stone_A_Rock.fbx')

# Footer layout reverse-engineered from Stone_A (171 bytes total):
#   pad zeros to 16-align | 16-byte code | 14 reserved zeros |
#   uint32 version | 120 zeros | 16-byte closing code.
# The fixed tail (everything after the pad byte) is derived from Stone_A
# itself at runtime, so no magic bytes are hardcoded here.
FBX_VERSION = 7400


def split_footer(base_footer, version):
    assert len(base_footer) == 171, len(base_footer)
    assert base_footer[31:35] == struct.pack('<I', version), 'template footer layout changed'
    return base_footer[1:]  # fixed 170-byte tail (pad byte excluded)


def read_node(data, off, ver):
    null = 25 if ver >= 7500 else 13
    if ver >= 7500:
        end_off, np_, _pl = struct.unpack_from('<QQQ', data, off)
        off += 24
    else:
        end_off, np_, _pl = struct.unpack_from('<III', data, off)
        off += 12
    if end_off == 0:
        return None, off
    nl = data[off]
    off += 1
    name = data[off:off + nl].decode('ascii', 'replace')
    off += nl
    props = []
    for _ in range(np_):
        t = chr(data[off])
        off += 1
        if t == 'Y':
            props.append(('Y', struct.unpack_from('<h', data, off)[0]))
            off += 2
        elif t == 'C':
            props.append(('C', bool(data[off])))
            off += 1
        elif t == 'I':
            props.append(('I', struct.unpack_from('<i', data, off)[0]))
            off += 4
        elif t == 'F':
            props.append(('F', struct.unpack_from('<f', data, off)[0]))
            off += 4
        elif t == 'D':
            props.append(('D', struct.unpack_from('<d', data, off)[0]))
            off += 8
        elif t == 'L':
            props.append(('L', struct.unpack_from('<q', data, off)[0]))
            off += 8
        elif t in 'fdlbi':
            n, enc, cl = struct.unpack_from('<III', data, off)
            off += 12
            raw = data[off:off + cl]
            off += cl
            if enc == 1:
                raw = zlib.decompress(raw)
            fmt = {'f': 'f', 'd': 'd', 'l': 'q', 'i': 'i', 'b': 'B'}[t]
            props.append((t, list(struct.unpack('<%d%s' % (n, fmt), raw))))
        elif t == 'S':
            ln = struct.unpack_from('<I', data, off)[0]
            off += 4
            props.append(('S', data[off:off + ln]))
            off += ln
        elif t == 'R':
            ln = struct.unpack_from('<I', data, off)[0]
            off += 4
            props.append(('R', data[off:off + ln]))
            off += ln
        else:
            raise ValueError('bad prop type %r' % t)
    kids = []
    le = end_off - null
    while off < le:
        c, off = read_node(data, off, ver)
        if c is None:
            break
        kids.append(c)
    return {'n': name, 'p': props, 'c': kids}, end_off


def parse_fbx(path):
    data = open(path, 'rb').read()
    assert data[:20] == b'Kaydara FBX Binary  ', 'not a binary FBX'
    ver = struct.unpack_from('<I', data, 23)[0]
    off = 27
    top = []
    while off < len(data):
        node, off = read_node(data, off, ver)
        if node is None:
            break
        top.append(node)
    return ver, top, data[off:]  # trailing bytes = footer


def to_wnode(node):
    """Typed tree -> fbx_write node tuples (S bytes decoded latin-1)."""
    props = []
    for t, v in node['p']:
        if t == 'S':
            props.append((t, v.decode('latin-1')))
        else:
            props.append((t, v))
    return (node['n'], props, [to_wnode(c) for c in node['c']])


def find_kids(node, name):
    return [c for c in node['c'] if c['n'] == name]


def find_path(root, path):
    cur = [root]
    for p in path:
        nxt = []
        for n in cur:
            nxt.extend(find_kids(n, p))
        cur = nxt
    return cur


def set_array(geo, layer, arr, values):
    nodes = find_path(geo, [layer, arr]) if layer else find_kids(geo, arr)
    assert len(nodes) == 1, 'expected 1 %s/%s, found %d' % (layer, arr, len(nodes))
    t = nodes[0]['p'][0][0]
    assert t in 'fdil', 'unexpected array type %r' % t
    nodes[0]['p'] = [(t, list(values))]


def build_target_mesh(what):
    if what == 'PLATFORM':
        # Unit-normalized: Platform root carries scale (6, 0.5, 6).
        return make_rock.build_mesh(make_rock.unit_altar_tris(), target=None)
    spec = make_rock.SPECS[what]
    return make_rock.build_mesh(make_rock.rock_tris(spec),
                                target=spec['target'],
                                flatten_top=spec['flatten'])


TARGETS = {
    'B': ('Stone_B_Rock', os.path.join(ART, 'Stones', 'Stone_B_Rock.fbx')),
    'C': ('Stone_C_Rock', os.path.join(ART, 'Stones', 'Stone_C_Rock.fbx')),
    'D': ('Stone_D_Rock', os.path.join(ART, 'Stones', 'Stone_D_Rock.fbx')),
    'PLATFORM': ('Platform_Altar', os.path.join(ART, 'Platform', 'Platform_Altar.fbx')),
}


def serialize(tree, version, fixed_tail):
    out = bytearray()
    out += b'Kaydara FBX Binary  ' + b'\x00\x1a\x00'
    out += struct.pack('<I', version)
    for node in tree:
        out += ser_node(to_wnode(node), len(out))
    out += b'\x00' * 13
    out += b'\x00' * ((16 - (len(out) % 16)) % 16)
    out += fixed_tail
    return bytes(out)


def clone_target(what, base_tree):
    asset_name, out_path = TARGETS[what]
    mesh = build_target_mesh(what)
    nverts = len(mesh['verts'])
    npolys = len(mesh['pvi']) // 3
    assert max(mesh['pvi']) < nverts, 'pvi out of range'
    assert min(mesh['pvi']) >= -nverts, 'negative pvi out of range'

    tree = copy.deepcopy(base_tree)
    root = {'n': '', 'p': [], 'c': tree}
    geo = find_path(root, ['Objects', 'Geometry'])[0]
    model = find_path(root, ['Objects', 'Model'])[0]

    geo['p'][1] = ('S', (asset_name + '_Mesh\x00\x01Geometry').encode('latin-1'))
    model['p'][1] = ('S', (asset_name + '\x00\x01Model').encode('latin-1'))
    for f in [n for n in tree if n['n'] == 'FileId']:
        f['p'] = [('R', uuid.uuid5(NS, 'balancepuzzle:fileid:' + asset_name).bytes)]

    set_array(geo, None, 'Vertices', mesh['flat'])
    set_array(geo, None, 'PolygonVertexIndex', mesh['pvi'])
    set_array(geo, None, 'Edges', mesh['edges'])
    set_array(geo, 'LayerElementSmoothing', 'Smoothing', [0] * npolys)
    set_array(geo, 'LayerElementNormal', 'Normals', mesh['normals'])
    set_array(geo, 'LayerElementNormal', 'NormalsIndex', mesh['nindex'])
    set_array(geo, 'LayerElementUV', 'UV', mesh['uvs'])
    set_array(geo, 'LayerElementUV', 'UVIndex', mesh['uvindex'])
    return tree, out_path, nverts, npolys


def verify(path, nverts, npolys, fixed_tail):
    ver, top, footer = parse_fbx(path)
    names = [t['n'] for t in top]
    assert 'Objects' in names and 'Connections' in names, names
    assert footer == fixed_tail[-(len(footer)):] or footer.endswith(fixed_tail[-16:]), \
        'footer mismatch'
    assert footer[-16:] == fixed_tail[-16:], 'closing code missing'
    root = {'n': '', 'p': [], 'c': top}
    geo = find_path(root, ['Objects', 'Geometry'])[0]
    assert geo['p'][0][0] == 'L', 'geo id not int64'
    for arr, expect in (('Vertices', nverts * 3),
                        ('PolygonVertexIndex', npolys * 3)):
        got = find_kids(geo, arr)[0]['p'][0][1]
        assert len(got) == expect, (arr, len(got), expect)
    pvi = find_kids(geo, 'PolygonVertexIndex')[0]['p'][0][1]
    assert max(pvi) < nverts
    return len(open(path, 'rb').read()), len(footer)


def main():
    whats = sys.argv[1:] or ['B', 'C', 'D', 'PLATFORM']
    ver, base_tree, base_footer = parse_fbx(STONE_A)
    fixed_tail = split_footer(base_footer, ver)
    print('template: Stone_A version=%d nodes=%d footer=%d bytes'
          % (ver, len(base_tree), len(base_footer)))
    os.makedirs(BACKUP, exist_ok=True)
    for what in whats:
        assert what in TARGETS, what
        tree, out_path, nverts, npolys = clone_target(what, base_tree)
        if os.path.exists(out_path):
            shutil.copy2(out_path, os.path.join(BACKUP, os.path.basename(out_path) + '.bak'))
        with open(out_path, 'wb') as f:
            f.write(serialize(tree, ver, fixed_tail))
        size, footer_len = verify(out_path, nverts, npolys, fixed_tail)
        print('wrote %s verts=%d polys=%d bytes=%d footer=%d'
              % (out_path, nverts, npolys, size, footer_len))
    print('ALL CLONES VERIFIED (structure + footer + index ranges)')


if __name__ == '__main__':
    main()
