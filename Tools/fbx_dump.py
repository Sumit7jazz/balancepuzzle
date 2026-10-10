#!/usr/bin/env python3
"""Dump binary-FBX node tree (names, prop-type summary, depth).

Usage: python Tools/fbx_dump.py <file.fbx> [--deep]
"""
import struct
import sys

sys.path.insert(0, "E:/GAME/balancepuzzle/Tools")
from fbx_inspect import read_node


def show(node, depth, deep):
    typs = []
    for p in node['props']:
        if isinstance(p, list):
            typs.append('arr[%d]' % len(p))
        elif isinstance(p, bytes):
            typs.append('raw[%d]' % len(p))
        else:
            r = repr(p)
            typs.append(r if len(r) < 28 else r[:27] + '…')
    print('  ' * depth + node['name'] + '  ' + str(typs))
    if deep or depth < 2 or node['name'] in ('Objects', 'Connections', 'GlobalSettings'):
        for c in node['children']:
            show(c, depth + 1, deep)


def main():
    path = sys.argv[1]
    deep = '--deep' in sys.argv
    data = open(path, 'rb').read()
    version = struct.unpack_from('<I', data, 23)[0]
    off, top = 27, []
    while off < len(data):
        node, off = read_node(data, off, version)
        if node is None:
            if off >= len(data):
                break
            continue
        top.append(node)
    for t in top:
        show(t, 0, deep)


if __name__ == '__main__':
    main()
