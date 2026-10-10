#!/usr/bin/env python3
"""Procedural granite PBR textures for the Balance Puzzle zen riverbed (stdlib only).

Generates per-stone Albedo + Normal (from heightfield) PNGs with organic
crevices, grain speckle and veins. Deterministic per seed.

Usage:
  python Tools/gen_zen_rock_textures.py
Output:
  balancepuzzle/Assets/Art/Textures/<Name>_Albedo.png / <Name>_Normal.png
"""
import math
import os
import random
import struct
import zlib

SIZE = 256
OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                   "Assets", "Art", "Textures")

SETS = {
    # name: (tint_rgb, seed, crevice_strength)
    "Stone_A": ((0.78, 0.74, 0.67), 101, 0.55),
    "Stone_B": ((0.72, 0.68, 0.62), 102, 0.70),
    "Stone_C": ((0.80, 0.77, 0.71), 103, 0.60),
    "Stone_D": ((0.66, 0.62, 0.57), 104, 0.85),
    "Altar":   ((0.70, 0.69, 0.66), 105, 0.75),
}


def make_lattice(rng, n):
    return [[rng.random() for _ in range(n)] for _ in range(n)]


def noise2(lat, x, y):
    n = len(lat)
    xi = int(math.floor(x)) % n
    yi = int(math.floor(y)) % n
    xf = x - math.floor(x)
    yf = y - math.floor(y)
    u = xf * xf * (3 - 2 * xf)
    v = yf * yf * (3 - 2 * yf)
    a = lat[yi][xi]
    b = lat[yi][(xi + 1) % n]
    c = lat[(yi + 1) % n][xi]
    d = lat[(yi + 1) % n][(xi + 1) % n]
    return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v


def fbm(lat, x, y, octaves, lac=2.03, gain=0.5):
    total, amp, freq, norm = 0.0, 1.0, 1.0, 0.0
    for _ in range(octaves):
        total += amp * noise2(lat, x * freq, y * freq)
        norm += amp
        amp *= gain
        freq *= lac
    return total / norm


def write_png(path, pixels):
    """pixels: list of rows of (r,g,b) 0-255 tuples."""
    h = len(pixels)
    w = len(pixels[0])
    raw = b"".join(b"\x00" + b"".join(struct.pack("3B", *px) for px in row)
                   for row in pixels)

    def chunk(typ, data):
        c = struct.pack(">I", len(data)) + typ + data
        c += struct.pack(">I", zlib.crc32(typ + data) & 0xFFFFFFFF)
        return c

    ihdr = struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0)
    png = (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr)
           + chunk(b"IDAT", zlib.compress(raw, 6)) + chunk(b"IEND", b""))
    with open(path, "wb") as f:
        f.write(png)


def gen_set(name, tint, seed, crev):
    rng = random.Random(seed)
    lat1 = make_lattice(rng, 32)
    lat2 = make_lattice(rng, 32)
    lat3 = make_lattice(rng, 16)
    s = SIZE / 8.0  # base frequency in lattice cells
    height = [[0.0] * SIZE for _ in range(SIZE)]
    for y in range(SIZE):
        for x in range(SIZE):
            u, v = x / SIZE * s, y / SIZE * s
            base = fbm(lat1, u, v, 5)
            ridge = 1.0 - abs(2.0 * fbm(lat2, u * 1.7 + 9.0, v * 1.7 + 3.0, 4) - 1.0)
            height[y][x] = base * (1.0 - crev * 0.5) + ridge * ridge * crev * 0.5

    alb = [[(0, 0, 0)] * SIZE for _ in range(SIZE)]
    for y in range(SIZE):
        for x in range(SIZE):
            h = height[y][x]
            u, v = x / SIZE * s, y / SIZE * s
            # Large-scale tonal patches + fine grain.
            patch = 0.92 + 0.16 * fbm(lat3, u * 0.5, v * 0.5, 3)
            grain = rng.uniform(-0.045, 0.045)
            shade = (0.78 + 0.42 * h) * patch + grain
            # Dark speckle + pale quartz flecks.
            r0 = rng.random()
            if r0 > 0.965:
                shade *= 0.80
            elif r0 < 0.012:
                shade *= 1.12
            # Thin dark veins where the second field crosses a band.
            vein = abs(fbm(lat2, u * 0.9 + 40.0, v * 0.9, 3) - 0.5)
            if vein < 0.018:
                shade *= 0.86
            # Crevice darkening in low areas.
            if h < 0.38:
                shade *= 0.82 + 0.47 * (h / 0.38)
            r = max(0, min(255, int(tint[0] * shade * 255)))
            g = max(0, min(255, int(tint[1] * shade * 255)))
            b = max(0, min(255, int(tint[2] * shade * 255)))
            alb[y][x] = (r, g, b)

    # Normal map from heightfield gradient (strength tuned for 256px rock).
    strength = 2.4
    nrm = [[(0, 0, 0)] * SIZE for _ in range(SIZE)]
    for y in range(SIZE):
        for x in range(SIZE):
            dx = height[y][(x + 1) % SIZE] - height[y][(x - 1) % SIZE]
            dy = height[(y + 1) % SIZE][x] - height[(y - 1) % SIZE][x]
            nx, ny, nz = -dx * strength, -dy * strength, 1.0
            inv = 1.0 / math.sqrt(nx * nx + ny * ny + nz * nz)
            nrm[y][x] = (int((nx * inv * 0.5 + 0.5) * 255),
                         int((ny * inv * 0.5 + 0.5) * 255),
                         int((nz * inv * 0.5 + 0.5) * 255))

    write_png(os.path.join(OUT, name + "_Albedo.png"), alb)
    write_png(os.path.join(OUT, name + "_Normal.png"), nrm)
    print("wrote", name, "albedo+normal")


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, (tint, seed, crev) in SETS.items():
        gen_set(name, tint, seed, crev)
    print("done:", OUT)


if __name__ == "__main__":
    main()
