"""
T-MINUS part models (Blender 5.x). Run inside Blender:
    exec(open(r"<repo>/tools/blender/build_parts.py").read())

Builds the Pathfinder rocket parts from the game's own data (parts.json / rockets.json), exports one FBX per
part id into Assets/_TMinus/Art/Models/Parts/, then lays the parts out side by side for viewing.

Conventions (see CLAUDE.md): part origin = bottom centre, +Z up in Blender (= +Y in Unity), metres.
Models are visual only: the game generates SNAP_*, the bolt flange and BOLT_## holes from data.
Material names match the game's MaterialLibrary, so Unity swaps in its own textured materials.
Objects named INT_* are internals that only show in X-ray.
Only objects inside the "TMINUS_Parts" collection are touched; rerunning rebuilds them.
"""
import bpy, bmesh, json, math, os

ROOT = globals().get("TMINUS_ROOT", r"C:\Users\AYAAN KASHIF\OneDrive\Desktop\NASA PROJECT")
DATA = os.path.join(ROOT, "Assets", "_TMinus", "Resources", "Data")
OUT = os.path.join(ROOT, "Assets", "_TMinus", "Art", "Models", "Parts")
SEG = 64

COLORS = {
    "Body_White": (0.92, 0.92, 0.9, 1), "Band_Black": (0.03, 0.03, 0.035, 1), "Metal_Grey": (0.55, 0.57, 0.6, 1),
    "Engine_Dark": (0.1, 0.095, 0.09, 1), "Insulation_Orange": (0.85, 0.42, 0.12, 1), "Board_Green": (0.08, 0.38, 0.2, 1),
}


def mat(name):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
    m.diffuse_color = COLORS.get(name, (0.8, 0.8, 0.8, 1))
    m.metallic = 0.8 if name in ("Metal_Grey", "Engine_Dark") else 0.0
    m.roughness = 0.45
    return m


def u2b(r, theta):
    """Unity plan-view angle (x = cos, z = sin) -> Blender (x, y). The FBX axis conversion turns the model
    180 degrees about the vertical, so build it pre-turned."""
    return (-r * math.cos(theta), -r * math.sin(theta))


class Part:
    def __init__(self, pid):
        self.pid = pid
        self.coll = bpy.data.collections.new(pid)
        bpy.data.collections["TMINUS_Parts"].children.link(self.coll)
        self.objects = []

    def add(self, name, bm, material, internal=False):
        mesh = bpy.data.meshes.new(name + "_" + self.pid)
        bm.normal_update()
        bm.to_mesh(mesh)
        bm.free()
        for p in mesh.polygons:
            p.use_smooth = True
        mesh.set_sharp_from_angle(angle=math.radians(40))
        mesh.materials.append(mat(material))
        obj = bpy.data.objects.new(("INT_" if internal else "") + name + "_" + self.pid, mesh)
        self.coll.objects.link(obj)
        self.objects.append(obj)
        return obj


def lathe(bm, profile, seg=SEG, start=0.0, sweep=2 * math.pi, flip=False, offset=(0.0, 0.0)):
    """Revolve (r, z) points about Z. Profile going up = outward normals. UV: u around, v = metres along."""
    uv = bm.loops.layers.uv.verify()
    full = abs(sweep - 2 * math.pi) < 1e-6
    cols = seg if full else seg + 1
    rings, vs, run = [], [0.0], 0.0
    for i in range(1, len(profile)):
        run += math.hypot(profile[i][0] - profile[i - 1][0], profile[i][1] - profile[i - 1][1])
        vs.append(run)
    ox, oy = u2b(math.hypot(*offset), math.atan2(offset[1], offset[0])) if offset != (0.0, 0.0) else (0.0, 0.0)
    for r, z in profile:
        ring = []
        for j in range(cols):
            x, y = u2b(max(r, 1e-4), start + sweep * j / seg)
            ring.append(bm.verts.new((x + ox, y + oy, z)))
        rings.append(ring)
    for i in range(len(profile) - 1):
        for j in range(seg):
            j2 = (j + 1) % cols if full else j + 1
            quad = [rings[i][j], rings[i][j2], rings[i + 1][j2], rings[i + 1][j]]
            if flip:
                quad.reverse()
            try:
                f = bm.faces.new(quad)
            except ValueError:
                continue
            us = [j / seg, (j + 1) / seg, (j + 1) / seg, j / seg]
            vv = [vs[i], vs[i], vs[i + 1], vs[i + 1]]
            if flip:
                us.reverse(); vv.reverse()
            for loop, u, v in zip(f.loops, us, vv):
                loop[uv].uv = (u, v)
    return bm


def box(bm, size, centre, theta=0.0, radius=0.0):
    """Box of size (radial, tangential, vertical) placed at `radius` along Unity angle theta, centre height z."""
    geo = bmesh.ops.create_cube(bm, size=1.0, calc_uvs=True)
    verts = geo["verts"]
    bmesh.ops.scale(bm, vec=size, verts=verts)
    x, y = u2b(radius, theta) if radius else (centre[0], centre[1])
    ang = math.atan2(y, x) if radius else theta
    bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=__import__("mathutils").Matrix.Rotation(ang, 3, "Z"), verts=verts)
    bmesh.ops.translate(bm, vec=(x, y, centre[2]), verts=verts)
    return bm


def capsule(r, z0, z1, dome=0.32, n=8):
    h = r * dome
    pts = [(r * math.sin(math.radians(90 * i / n)), z0 + h * (1 - math.cos(math.radians(90 * i / n)))) for i in range(n + 1)]
    pts += [(r * math.cos(math.radians(90 * i / n)), z1 - h + h * math.sin(math.radians(90 * i / n))) for i in range(n + 1)]
    return pts


def bell(bm, exit_r, length, offset=(0.0, 0.0), seg=32):
    """Engine: chamber + throat + parabolic bell below z = 0, with an inner surface."""
    n = 10
    outer = [(exit_r * (0.3 + 0.7 * math.sqrt(1 - i / n)), -length + length * 0.82 * i / n) for i in range(n + 1)]
    outer += [(exit_r * 0.46, -length * 0.1), (exit_r * 0.46, 0.0), (0.0, 0.0)]
    lathe(bm, outer, seg, offset=offset)
    inner = [(r * 0.93, z) for r, z in outer[:n + 1]] + [(0.0, -length * 0.2)]
    lathe(bm, inner, seg, flip=True, offset=offset)
    lathe(bm, [(exit_r * 0.93, -length), (exit_r, -length)], seg, flip=True, offset=offset)  # lip


def ring(bm, r, z0, z1, lip=0.004):
    lathe(bm, [(r, z0), (r + lip, z0), (r + lip, z1), (r, z1)])


def stage(pid, r, L, engines):
    p = Part(pid)
    p.add("Shell", lathe(bmesh.new(), [(0, 0), (r, 0), (r, L), (0, L)]), "Body_White")
    bands = bmesh.new()
    skirt = min(0.45, L * 0.12)
    ring(bands, r, 0.0, skirt); ring(bands, r, L - min(0.45, L * 0.1), L)
    if L > 6:
        ring(bands, r, L * 0.42, L * 0.42 + 0.5)
    p.add("Bands", bands, "Band_Black")
    ribs = bmesh.new()
    zs = [skirt, L - min(0.45, L * 0.1)] + ([L * 0.42, L * 0.42 + 0.5] if L > 6 else [])
    zs += [z for z in (2.0, 3.5, 7.0, 8.5, 10.0) if z < L - 1] if L > 6 else [L * 0.5]
    for z in zs:
        ring(ribs, r, z - 0.012, z + 0.012, 0.007)
    for th in (math.radians(45), math.radians(225)):   # cable raceways
        box(ribs, (0.03, 0.08, L - skirt - 0.6), (0, 0, skirt + 0.1 + (L - skirt - 0.6) / 2), th, r + 0.012)
    if engines == 1:                                  # upper stage: cold-gas thruster pods
        for i in range(4):
            box(ribs, (0.05, 0.1, 0.1), (0, 0, L * 0.8), math.radians(90 * i), r + 0.02)
    p.add("Details", ribs, "Metal_Grey")
    if engines > 1:                                   # first stage fins
        fins = bmesh.new()
        for i in range(4):
            th = math.radians(90 * i)
            for k in range(6):                         # stepped taper, root to tip
                t = k / 6
                box(fins, (0.06, 0.028, 1.0 - 0.6 * t), (0, 0, 0.08 + (1.0 - 0.6 * t) / 2), th, r + 0.03 + 0.06 * k)
        p.add("Fins", fins, "Band_Black")
    eng = bmesh.new()
    exit_r = r * 0.42 if engines == 1 else r * 0.2
    length = exit_r * 2.4
    if engines == 1:
        bell(eng, exit_r, length, seg=48)
    else:
        ringn = engines - 1 if engines in (5, 9) else engines
        for i in range(ringn):
            a = 2 * math.pi * i / ringn + math.pi / 4
            bell(eng, exit_r, length, (math.cos(a) * r * 0.65, math.sin(a) * r * 0.65))
        if engines in (5, 9):
            bell(eng, exit_r, length)
    lathe(eng, [(0, -0.03), (r * 0.97, -0.03), (r * 0.97, 0.0)])            # heat shield
    p.add("Engines", eng, "Engine_Dark")
    p.add("Tank_Fuel", lathe(bmesh.new(), capsule(r * 0.88, L * 0.06, L * 0.42), 32), "Metal_Grey", internal=True)
    p.add("Tank_Oxidiser", lathe(bmesh.new(), capsule(r * 0.88, L * 0.47, L * 0.93), 32), "Insulation_Orange", internal=True)
    return p


def interstage(pid, r, L):
    p = Part(pid)
    t = 0.018
    p.add("Shell", lathe(bmesh.new(), [(r - t, 0), (r, 0), (r, L), (r - t, L), (r - t, 0)]), "Band_Black")
    st = bmesh.new()
    for i in range(24):
        box(st, (0.014, 0.022, L * 0.84), (0, 0, L / 2), 2 * math.pi * i / 24, r + 0.007)
    p.add("Stringers", st, "Band_Black")
    fr = bmesh.new()
    ring(fr, r, 0.04, 0.075, 0.012); ring(fr, r, L - 0.075, L - 0.04, 0.012)
    p.add("Frames", fr, "Metal_Grey")
    return p


def avionics_ring(pid, r, L):
    p = Part(pid)
    ro = r * 1.01
    p.add("Ring", lathe(bmesh.new(), [(ro - 0.035, 0), (ro, 0), (ro, L), (ro - 0.035, L), (ro - 0.035, 0)]), "Band_Black")
    d = bmesh.new()
    n = 10
    lathe(d, [(ro + 0.022 * math.sin(math.pi * i / n), L * 0.5 - 0.03 * math.cos(math.pi * i / n)) for i in range(n + 1)])
    for i in range(3):                                   # access panels
        box(d, (0.008, 0.2, L * 0.42), (0, 0, L * 0.5), math.radians(60 + 120 * i), ro + 0.003)
    box(d, (0.02, 0.02, 0.16), (0, 0, L * 0.75), math.radians(0), ro + 0.03)   # telemetry antenna
    p.add("Details", d, "Metal_Grey")
    for i in range(6):
        b = box(bmesh.new(), (r * 0.18, r * 0.25, L * 0.6), (0, 0, L * 0.5), 2 * math.pi * i / 6, r * 0.7)
        p.add("Avionics_%d" % i, b, "Board_Green", internal=True)
    return p


def adapter(pid, L):
    p = Part(pid)
    rb, rt = 0.6, 0.27
    p.add("Cone", lathe(bmesh.new(), [(0, 0), (rb, 0), (rt, L), (0, L)], 48), "Metal_Grey")
    rings = bmesh.new()
    ring(rings, rb - 0.004, 0.0, 0.03, 0.01); ring(rings, rt - 0.004, L - 0.025, L, 0.01)
    for i in range(12):                                   # stiffeners along the cone
        th = 2 * math.pi * i / 12
        for k in range(5):
            t = (k + 0.5) / 5
            box(rings, (0.012, 0.014, L / 5 * 1.05), (0, 0, L * t), th, rb + (rt - rb) * t + 0.006)
    p.add("Rings", rings, "Band_Black")
    disp = bmesh.new()
    box(disp, (0.13, 0.13, 0.38), (0, 0, L + 0.19))
    p.add("Dispenser", disp, "Metal_Grey")
    rails = bmesh.new()
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(rails, (0.018, 0.018, 0.385), (sx * 0.066, sy * 0.066, L + 0.1925))
    box(rails, (0.15, 0.15, 0.012), (0, 0, L + 0.006))
    p.add("Rails", rails, "Band_Black")
    return p


def fairing(pid, r, L, left):
    p = Part(pid)
    cyl, nose = L * 0.45, L * 0.55
    rho = (r * r + nose * nose) / (2 * r)
    prof = [(r, 0.0), (r, cyl)] + [(max(math.sqrt(rho * rho - (nose * i / 14) ** 2) - (rho - r), 0.0005), cyl + nose * i / 14) for i in range(1, 15)]
    start = math.radians(90 if left else -90)
    p.add("Shell", lathe(bmesh.new(), prof, 40, start, math.pi), "Body_White")
    inner = [(max(a - 0.02, 0.0004), z) for a, z in prof]
    p.add("Inner", lathe(bmesh.new(), inner, 40, start, math.pi, flip=True), "Metal_Grey")
    band = bmesh.new()
    lathe(band, [(r, 0.0), (r + 0.004, 0.0), (r + 0.004, 0.14), (r, 0.14)], 40, start, math.pi)
    lathe(band, [(prof[-4][0] + 0.003, prof[-4][1])] + [(a + 0.003, z) for a, z in prof[-3:]], 40, start, math.pi)   # nose cap
    p.add("Band", band, "Band_Black")
    return p


def export(p):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in p.objects:
        o.hide_set(False); o.select_set(True)
    bpy.context.view_layer.objects.active = p.objects[0]
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, p.pid + ".fbx"), use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True,
                             axis_forward="-Z", axis_up="Y", mesh_smooth_type="EDGE", use_mesh_modifiers=True,
                             add_leaf_bones=False, bake_anim=False, use_custom_props=False)


def build_all():
    root = bpy.data.collections.get("TMINUS_Parts")
    if root is None:
        root = bpy.data.collections.new("TMINUS_Parts")
        bpy.context.scene.collection.children.link(root)
    for c in list(root.children):
        for o in list(c.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.collections.remove(c)

    parts = {p["id"]: p for p in next(v for v in json.load(open(os.path.join(DATA, "parts.json"), encoding="utf-8")).values() if isinstance(v, list))}
    rockets = next(v for v in json.load(open(os.path.join(DATA, "rockets.json"), encoding="utf-8")).values() if isinstance(v, list))
    stages = {s["partId"]: s for rk in rockets for s in rk["stages"]}

    built = []
    for pid, d in parts.items():
        if not pid.startswith("pf_"):
            continue
        r, L = d.get("diameterM", 1.2) / 2, d.get("lengthM", 1.0)
        cat = d.get("category")
        if cat == "rocket_stage":
            s = stages.get(pid, {})
            built.append(stage(pid, s.get("diameterM", r * 2) / 2, s.get("lengthM", L), s.get("engineCount", 1)))
        elif cat == "interstage":
            built.append(interstage(pid, r, L))
        elif cat == "avionics_ring":
            built.append(avionics_ring(pid, r, L))
        elif cat == "payload_adapter":
            built.append(adapter(pid, L))
        elif cat == "fairing":
            built.append(fairing(pid, r, L, pid.endswith("left")))
    for p in built:
        export(p)
    x = 0.0
    for p in built:                       # lay out for viewing (after export, which needs them at the origin)
        for o in p.objects:
            o.location.x = x
        x += 2.2
    return {p.pid: [o.name for o in p.objects] for p in built}


result = build_all()
