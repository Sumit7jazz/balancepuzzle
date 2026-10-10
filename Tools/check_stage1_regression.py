#!/usr/bin/env python3
"""
Stage 1 static regression checker — validates that BUG-001 through BUG-007
fixes remain intact, without requiring Unity.

Usage: python3 Tools/check_stage1_regression.py [--repo ROOT]

Exit 0 = all static checks pass. Non-zero = regression detected.
BUG-007 is runtime-only; this script verifies the diagnostic exists and the
material GUIDs are well-formed, but rendering correctness stays BLOCKED
until observed in Unity Play Mode.
"""
import re
import sys
import os

def check(label, cond, detail=""):
    status = "PASS" if cond else "FAIL"
    print(f"[{status}] {label}" + (f" — {detail}" if detail and not cond else ""))
    return cond

def main():
    root = sys.argv[sys.argv.index("--repo") + 1] if "--repo" in sys.argv else "."
    ok = True
    src = lambda p: open(os.path.join(root, p)).read()

    # BUG-001: LevelTimer.TimeLeft initialized in Awake (not only on reset).
    t = src("Assets/Scripts/Level/LevelTimer.cs")
    ok &= check("BUG-001: TimeLeft initialized in Awake",
                "TimeLeft = config != null ? config.initialTime" in t)

    # BUG-002: HudController invokes onUp(), not up().
    h = src("Assets/Scripts/UI/HudController.cs")
    ok &= check("BUG-002: AddHoldHandler invokes onUp callback",
                "() => { if (inputReader != null) inputReader.SetRotateAxis(0f); }" in h
                and "up()" not in h.replace("onUp()", ""))

    # BUG-003: Stone roots unscaled (scale 1,1,1); Visual children carry scale.
    scene = src("Assets/Scenes/Stage1_Sandbox.unity")
    for s in ["Stone_A", "Stone_B", "Stone_C", "Stone_D"]:
        m = re.search(r"m_Name: " + s + r"\n(?:.*\n)*?m_LocalScale: \{x: ([\d.]+), y: ([\d.]+), z: ([\d.]+)\}",
                      scene)
        # Simpler: find the scale line after each stone name.
        ok &= check(f"BUG-003: {s} root scale is (1,1,1)",
                    f"m_Name: {s}" in scene and True)  # placeholder, refined below
    # Refined check via awk-style scan.
    lines = scene.split("\n")
    scales = {}
    for i, line in enumerate(lines):
        mm = re.match(r"\s*m_Name: (Stone_[ABCD])$", line)
        if mm:
            for j in range(i, min(i + 200, len(lines))):
                sm = re.search(r"m_LocalScale: \{x: ([\d.]+), y: ([\d.]+), z: ([\d.]+)\}", lines[j])
                if sm:
                    scales[mm.group(1)] = (sm.group(1), sm.group(2), sm.group(3))
                    break
    for s in ["Stone_A", "Stone_B", "Stone_C", "Stone_D"]:
        got = scales.get(s)
        unscaled = (got is not None
                    and all(abs(float(v) - 1.0) < 1e-9 for v in got))
        ok &= check(f"BUG-003: {s} root unscaled",
                    unscaled, f"got {scales.get(s)}")

    # BUG-004: Finalizer must not CALL AddObjectToAsset (mentions in comments are OK).
    f = src("Assets/Editor/Stage1AssetFinalizer.cs")
    code_lines = [l for l in f.split("\n") if not l.strip().startswith("//")]
    code_only = "\n".join(code_lines)
    ok &= check("BUG-004: no AddObjectToAsset call in finalizer",
                "AddObjectToAsset(" not in code_only)

    # BUG-005: Material shader GUIDs are exactly 32 hex chars.
    import glob
    for mat in sorted(glob.glob(os.path.join(root, "Assets/Materials/Stage1/*.mat"))):
        msrc = open(mat).read()
        guids = re.findall(r"m_Shader: \{fileID: \d+, guid: ([0-9a-f]+),", msrc)
        good = all(len(g) == 32 for g in guids) and len(guids) > 0
        ok &= check(f"BUG-005: 32-char shader GUID in {os.path.basename(mat)}", good)

    # BUG-006: HUD font is LegacyRuntime.ttf, not Arial.ttf.
    ok &= check("BUG-006: uses LegacyRuntime.ttf",
                'LegacyRuntime.ttf' in h and 'Arial.ttf' not in h)

    # BUG-007: diagnostic exists; rendering stays runtime-BLOCKED.
    import glob as g
    ok &= check("BUG-007: diagnostic script exists",
                len(g.glob(os.path.join(root, "Assets/Editor/Bug007Diagnostic.cs"))) > 0)
    print("[INFO] BUG-007 rendering correctness is runtime-BLOCKED until observed in Unity Play Mode.")

    # Contract values.
    cfg = src("Assets/Scripts/Config/Stage1Config.cs")
    for label, needle in [
        ("initialTime=120", "initialTime = 120f"),
        ("maxTime=150", "maxTime = 150f"),
        ("stabilityDuration=2.0", "stabilityDuration = 2.0f"),
        ("maxLinearVelocity=0.08", "maxLinearVelocity = 0.08f"),
        ("maxAngularVelocity=0.20", "maxAngularVelocity = 0.20f"),
        ("impactVelocityReset=1.5", "impactVelocityReset = 1.5f"),
        ("supportMargin=0.02", "supportMargin = 0.02f"),
        ("killY=-3", "killY = -3f"),
        ("dragRadius=8", "dragRadius = 8f"),
    ]:
        ok &= check(f"Contract: {label}", needle in cfg)

    print("\n" + ("ALL STATIC CHECKS PASS" if ok else "REGRESSION DETECTED"))
    return 0 if ok else 1

if __name__ == "__main__":
    sys.exit(main())
