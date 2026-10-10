#!/usr/bin/env python3
"""Minimal FBX 7400 binary writer (Blender-compatible node layout).

Node = (name: str, props: [(typechar, value)], children: [node]).
Type chars: Y=int16 C=bool I=int32 F=float D=double L=int64
S=str R=bytes f/d/i/l=numeric arrays (zlib-compressed).
"""
import struct
import zlib


def pack_props(props):
    out = bytearray()
    for t, v in props:
        out += t.encode('ascii')
        if t == 'Y':
            out += struct.pack('<h', v)
        elif t == 'C':
            out += struct.pack('<B', 1 if v else 0)
        elif t == 'I':
            out += struct.pack('<i', v)
        elif t == 'F':
            out += struct.pack('<f', v)
        elif t == 'D':
            out += struct.pack('<d', v)
        elif t == 'L':
            out += struct.pack('<q', v)
        elif t == 'S':
            b = v.encode('latin-1')
            out += struct.pack('<I', len(b)) + b
        elif t == 'R':
            out += struct.pack('<I', len(v)) + v
        elif t in 'fdil':
            fmt = {'f': 'f', 'd': 'd', 'i': 'i', 'l': 'q'}[t]
            raw = struct.pack('<%d%s' % (len(v), fmt), *v)
            comp = zlib.compress(raw, 6)
            out += struct.pack('<III', len(v), 1, len(comp)) + comp
        else:
            raise ValueError('bad prop type %r' % t)
    return bytes(out)


def ser_node(node, base):
    """Serialize one node; base = absolute file offset of its first byte."""
    name, props, children = node
    nb = name.encode('ascii')
    pb = pack_props(props)
    hdr_len = 4 + 4 + 4 + 1 + len(nb)
    cb = bytearray()
    cbase = base + hdr_len + len(pb)
    for c in children:
        cbytes = ser_node(c, cbase)
        cb += cbytes
        cbase += len(cbytes)
    abs_end = cbase + 13
    return (struct.pack('<III', abs_end, len(props), len(pb)) +
            bytes([len(nb)]) + nb + pb + bytes(cb) + b'\x00' * 13)


def write_fbx(path, top_nodes, version=7400):
    out = bytearray()
    out += b'Kaydara FBX Binary  ' + b'\x00\x1a\x00'
    out += struct.pack('<I', version)
    for node in top_nodes:
        out += ser_node(node, len(out))
    out += b'\x00' * 13  # file-level NULL record
    with open(path, 'wb') as f:
        f.write(bytes(out))


def P(name, vtype, sub, flag, *vals):
    """GlobalSettings/Properties70 P entry. vals are (typechar, value)."""
    return ('P', [('S', name), ('S', vtype), ('S', sub), ('S', flag)] +
            [(t, v) for t, v in vals], [])


def N(name, props=None, children=None):
    return (name, props or [], children or [])
