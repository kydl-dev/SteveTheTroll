# D-166 (user 2026-11-01: "Start from 0 ... Create proper pants from the start. Look at the assets from the game how pants are built, and do it the same way, just make
# them the same blue color as now") - PANTS v3, written from scratch (Claude-Agent-18).
# How the game builds trousers (Player.prefab, armour "legs" pieces): a separate mesh that follows the BODY'S OWN SURFACE a little way out, skinned to the player's armature
# with the body's bone list. So these pants are lofted from cross-sections of the body's own legs (reference/player_male.obj + player_female.obj, the rip's rest pose): every
# 3 cm a ring that encloses BOTH bodies plus the fabric margin, smoothed, mirrored left/right, merged into one hip seat above the crotch, thickened into a real cloth shell
# (hem and waistband have an edge). No metaballs, no cuff ring, nothing wider than the leg. Plus the belt and the loincloth flaps of the costume.
# Weights are taken from the player's body in Unity (CostumeBuild), as for every armour piece.
# Run: blender -b --factory-startup --python scripts/pants_model.py -- <out_blend> <out_fbx> <render_prefix>
# Axes: Unity (x, y, z) -> Blender (x, z, y): Blender Z up, the player faces +Y, the player's RIGHT is +X. Origin = the player's feet.
import bpy, bmesh, sys, math, os
from mathutils import Vector
from mathutils.bvhtree import BVHTree

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT_BLEND = argv[0] if len(argv) > 0 else ""
OUT_FBX = argv[1] if len(argv) > 1 else ""
RENDER = argv[2] if len(argv) > 2 else ""
HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REF = os.path.join(HERE, "reference")

Z_HEM = float(os.environ.get("STT_PANTS_HEM", "0.17"))       # the trousers end above the slipper's ankle opening
Z_TOP = float(os.environ.get("STT_PANTS_TOP", "1.04"))       # waistband top (the belt sits on it; it overlaps the chest's hem)
MARGIN = float(os.environ.get("STT_PANTS_MARGIN", "0.018"))  # fabric margin beside the body (m)
INNER = 0.006                                                # margin on the side facing the other leg
CLOTH = float(os.environ.get("STT_PANTS_CLOTH", "0.012"))    # cloth thickness (m), grows INWARD so the outer surface stays where the fit put it
DZ = 0.03                                                    # ring spacing
N = 28                                                       # points per ring
WIN = 0.045                                                  # body points within this height of a ring are used for it

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
log = []

def u2b(x, y, z): return Vector((x, z, y))

J = {}
for line in open(os.path.join(REF, "player_joints.txt")):
    p = line.split()
    if len(p) == 4: J[p[0]] = u2b(float(p[1]), float(p[2]), float(p[3]))

def load_obj(name):
    verts, faces = [], []
    for line in open(os.path.join(REF, name + ".obj")):
        if line.startswith("v "):
            _, x, y, z = line.split(); verts.append(u2b(float(x), float(y), float(z)))
        elif line.startswith("f "):
            faces.append([int(i) - 1 for i in line.split()[1:]][::-1])      # handedness flip -> keep the winding outward
    me = bpy.data.meshes.new(name); me.from_pydata(verts, [], faces); me.update()
    o = bpy.data.objects.new(name, me); scene.collection.objects.link(o)
    return o

male, female = load_obj("player_male"), load_obj("player_female")

def mat(name, rgb, rough=0.9):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]; b.inputs["Base Color"].default_value = (*rgb, 1); b.inputs["Roughness"].default_value = rough
    m.diffuse_color = (*rgb, 1)
    return m
# the SAME colours as the costume now (CostumeBuild.Colours): plush_blue and straw_yellow (Blender's linear values)
M = {"blue": mat("plush_blue", (0.30, 0.46, 0.56)), "straw": mat("straw_yellow", (0.80, 0.66, 0.28)), "body": mat("reference_skin", (0.8, 0.5, 0.4))}
male.data.materials.append(M["body"]); female.data.materials.append(M["body"])

# The reference bodies are very coarse (1010 / 1112 vertices for the whole body), so the SURFACE is sampled densely (corners, edge midpoints, face centres and points between)
def dense_points(o):
    out = []
    bm = bmesh.new(); bm.from_mesh(o.data); bmesh.ops.triangulate(bm, faces=bm.faces)
    mw = o.matrix_world
    # only the legs + hips: cut the body at z = Z_TOP + 0.15 and keep the island that holds a leg (the hands hang at hip height but belong to the arms, which leave this island)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if any(v.co.z > Z_TOP + 0.15 for v in f.verts)], context="FACES")
    bm.verts.ensure_lookup_table()
    seed = min(bm.verts, key=lambda v: (v.co - Vector((J["LeftLeg"].x, J["LeftLeg"].y, 0.5))).length)
    island, stack = {seed}, [seed]
    while stack:
        v = stack.pop()
        for e in v.link_edges:
            w = e.other_vert(v)
            if w not in island: island.add(w); stack.append(w)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if any(v not in island for v in f.verts)], context="FACES")
    bm.faces.ensure_lookup_table()
    for f in bm.faces:
        a, b, c = [mw @ v.co for v in f.verts]
        m = (a + b + c) / 3
        mids = ((a + b) / 2, (b + c) / 2, (c + a) / 2)
        out += [a, b, c, m] + list(mids) + [(q + m) / 2 for q in mids] + [(q + r) / 2 for q, r in ((a, m), (b, m), (c, m))]
    bm.free()
    return out

# body points of BOTH bodies, folded onto the right leg (x -> |x|): the pants are symmetric, the left leg is the mirror
body_pts = []
for o in (male, female):
    body_pts += [p for p in dense_points(o) if abs(p.x) < 0.30 and abs(p.y) < 0.30 and Z_HEM - WIN <= p.z <= Z_TOP + WIN]
pts = [Vector((abs(p.x), p.y, p.z)) for p in body_pts]
log.append(f"{len(pts)} body surface points (both bodies, folded to the right leg) between z {Z_HEM - WIN:.2f} and {Z_TOP + WIN:.2f}")

# crotch height: going up, the first slice where the two legs have joined (a body surface point within 1.2 cm of the middle plane at the front or back)
Z_CROTCH = 0.80
for i in range(0, 40):
    z = 0.60 + i * 0.01
    sl = [p for p in pts if abs(p.z - z) <= 0.012 and abs(p.y) < 0.20]
    if sl and min(p.x for p in sl) <= 0.012: Z_CROTCH = z - 0.01; break
log.append(f"crotch at z {Z_CROTCH:.2f}")
RAMP0, RAMP1 = Z_CROTCH - 0.045, Z_CROTCH + 0.035     # the inner walls close up between these heights

def build(extra):
    zs = []
    z = Z_HEM
    while z < Z_TOP - 1e-6: zs.append(z); z += DZ
    zs.append(Z_TOP)
    raw = []                                  # per ring: (cx, cy, [h_k])
    for z in zs:
        sl = [p for p in pts if abs(p.z - z) <= WIN]
        if len(sl) < 5: sl = [p for p in pts if abs(p.z - z) <= WIN * 2.5]
        cx = (min(p.x for p in sl) + max(p.x for p in sl)) / 2; cy = (min(p.y for p in sl) + max(p.y for p in sl)) / 2
        hs = []
        for k in range(N):
            a = 2 * math.pi * k / N; ux, uy = math.cos(a), math.sin(a)
            h = max((p.x - cx) * ux + (p.y - cy) * uy for p in sl)
            inner = ux < -0.35                       # the side facing the other leg
            hs.append(h + (INNER if inner else MARGIN + extra))      # extra fabric only on the outside: the inner walls must not cross the middle plane
        raw.append((cx, cy, hs))
    # smooth over the ring and over the height, never below what covers the bodies
    cxs = [r[0] for r in raw]; cys = [r[1] for r in raw]; H = [list(r[2]) for r in raw]
    floor = [list(r[2]) for r in raw]
    for _ in range(4):
        for i in range(len(H)):
            H[i] = [max(floor[i][k] * 0.985, 0.5 * H[i][k] + 0.25 * (H[i][(k - 1) % N] + H[i][(k + 1) % N])) for k in range(N)]
        H = [[max(floor[i][k] * 0.985, (0.5 * H[i][k] + 0.25 * (H[max(i - 1, 0)][k] + H[min(i + 1, len(H) - 1)][k]))) for k in range(N)] for i in range(len(H))]
        cxs = [0.5 * cxs[i] + 0.25 * (cxs[max(i - 1, 0)] + cxs[min(i + 1, len(cxs) - 1)]) for i in range(len(cxs))]
        cys = [0.5 * cys[i] + 0.25 * (cys[max(i - 1, 0)] + cys[min(i + 1, len(cys) - 1)]) for i in range(len(cys))]
    verts, rings = [], []
    for i, z in enumerate(zs):
        ring = []
        t = min(1.0, max(0.0, (z - RAMP0) / (RAMP1 - RAMP0)))      # 0 below the crotch, 1 above: inner walls close onto the middle plane
        flare = 1.0 + 0.03 * max(0.0, 1.0 - (z - Z_HEM) / 0.05)    # the hem flares a hair
        for k in range(N):
            a = 2 * math.pi * k / N; ux, uy = math.cos(a), math.sin(a)
            x = cxs[i] + ux * H[i][k] * flare; y = cys[i] + uy * H[i][k] * flare
            if ux < 0.0: x = max(x, 0.004) * (1.0 - t) if t > 0.0 else max(x, 0.004)      # below the crotch the legs stay apart; above it the inner half moves onto x = 0
            ring.append(len(verts)); verts.append(Vector((x, y, z)))
        rings.append(ring)
    return zs, verts, rings

def to_object(name, verts, rings, mirror):
    bm = bmesh.new()
    sgn = -1.0 if mirror else 1.0
    bv = [bm.verts.new((v.x * sgn, v.y, v.z)) for v in verts]
    for i in range(len(rings) - 1):
        for k in range(N):
            a, b = rings[i][k], rings[i][(k + 1) % N]; c, d = rings[i + 1][(k + 1) % N], rings[i + 1][k]
            quad = (bv[a], bv[b], bv[c], bv[d]) if not mirror else (bv[d], bv[c], bv[b], bv[a])
            try: bm.faces.new(quad)
            except ValueError: pass
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); scene.collection.objects.link(o)
    return o

def join(objs, active):
    # objects made with bpy.data.objects.new are in the view layer only after an update; without it select_set() / join() silently skip them
    bpy.context.view_layer.update()
    for o in bpy.context.view_layer.objects: o.select_set(o in objs)
    bpy.context.view_layer.objects.active = active
    bpy.ops.object.join()
    return bpy.context.view_layer.objects.active

def shell_tree(o):
    b = bmesh.new(); b.from_mesh(o.data); b.transform(o.matrix_world); t = BVHTree.FromBMesh(b); b.free(); return t

def outward_fix(o):
    bm = bmesh.new(); bm.from_mesh(o.data)
    if sum(f.normal.dot(f.calc_center_median() - Vector((0.0, f.calc_center_median().y, 0.6))) for f in bm.faces) < 0: bmesh.ops.reverse_faces(bm, faces=bm.faces)
    bm.to_mesh(o.data); bm.free()

def make(extra):
    zs, verts, rings = build(extra)
    r = to_object("PantsR", verts, rings, False); l = to_object("PantsL", verts, rings, True)
    p = join([r, l], r); p.name = "Pants"
    bm = bmesh.new(); bm.from_mesh(p.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-4)
    mid = [f for f in bm.faces if all(abs(v.co.x) < 1e-4 for v in f.verts)]       # the membrane between the two legs above the crotch: not part of the cloth
    bmesh.ops.delete(bm, geom=mid, context="FACES_ONLY")
    bmesh.ops.delete(bm, geom=[e for e in bm.edges if not e.link_faces], context="EDGES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bm.to_mesh(p.data); bm.free()           # winding is outward by construction (the left leg is the mirror with reversed quads); no recalc: open tubes would be oriented per island
    return p, len(mid)

def checked_points():
    # the fit test skips the crotch pocket (between the legs, |x| < 4 cm, 70..92 cm high): hidden by the legs themselves, nothing a player can see
    return [q for q in body_pts if Z_HEM + 0.01 <= q.z <= Z_TOP - 0.01 and not (abs(q.x) < 0.04 and 0.70 < q.z < 0.92)]

def coverage(p):
    tree = shell_tree(p); out = 0; worst = 0.0
    allp = checked_points()
    for q in allp:
        loc, nor, idx, dist = tree.find_nearest(q)
        if loc is None: continue
        d = (q - loc).dot(nor)
        if d > -0.001: out += 1; worst = max(worst, d + 0.001)
    return out, len(allp), worst

extra = 0.0
ATT = int(os.environ.get("STT_ATTEMPTS", "6"))
for attempt in range(ATT):
    pants, membrane = make(extra)
    out, n, worst = coverage(pants)
    log.append(f"attempt {attempt}: margin +{extra * 100:.1f} cm, {len(pants.data.vertices)} verts, {membrane} membrane faces removed, {out} of {n} body points outside, worst {worst * 100:.2f} cm")
    if out == 0 or attempt == ATT - 1: break
    bpy.data.objects.remove(pants); extra += worst + 0.004
log.append(f"FIT (both bodies): {out} of {n} body points outside the pants")
if os.environ.get("STT_DEBUG"):
    tree = shell_tree(pants); bands = {}
    for q in checked_points():
        loc, nor, idx, dist = tree.find_nearest(q)
        d = (q - loc).dot(nor)
        if d > -0.001:
            k = (round(q.z, 1), "x<0" if q.x < 0 else "x>=0", "front" if q.y > 0 else "back")
            e = bands.setdefault(k, [0, 0.0, q.x, q.y]); e[0] += 1; e[1] = max(e[1], d)
    for k, e in sorted(bands.items()): log.append(f"  outside {k}: {e[0]} pts, worst {e[1] * 100:.1f} cm, e.g. x {e[2]:.2f} y {e[3]:.2f}")
    fn = [f.normal.dot(f.center - Vector((0, f.center.y, 0.6))) for f in pants.data.polygons]
    log.append(f"  normals pointing outward: {sum(1 for v in fn if v > 0)} of {len(fn)}")

# ---- the waist goes OVER the chest (user 2026-11-04: "the chest is cutting into the yellow cloth ... the pants should go above the chest piece") ----
# The costume's chest (Costume_Top_Mesh, a plush bag down to z 0.70) is wider than the body at the hips, so it swallowed the belt and flaps. The body is hidden while the costume is
# worn, so the waist of the pants is pushed radially OUT to the chest's own surface + CHEST_OVER, fading in from CHEST_Z0 to CHEST_Z1 (the legs below stay body-tight).
CHEST_BLEND = os.environ.get("STT_CHEST_BLEND", os.path.join(HERE, "source", "costume_v2.blend"))
CHEST_OVER = float(os.environ.get("STT_CHEST_OVER", "0.025"))
CHEST_Z0, CHEST_Z1 = 0.80, 0.92
if os.path.exists(CHEST_BLEND):
    with bpy.data.libraries.load(CHEST_BLEND, link=False) as (src, dst):
        dst.objects = [n for n in src.objects if n == "Costume_Top_Mesh"]
    chest = dst.objects[0]
    scene.collection.objects.link(chest); bpy.context.view_layer.update()
    cb = bmesh.new(); cb.from_mesh(chest.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()); cb.transform(chest.matrix_world)
    ctree = BVHTree.FromBMesh(cb); cb.free()
    bpy.data.objects.remove(chest)
    AX_Y = J["Hips"].y
    moved = 0; worst_push = 0.0
    for v in pants.data.vertices:
        if v.co.z < CHEST_Z0: continue
        dx, dy = v.co.x, v.co.y - AX_Y
        r = math.hypot(dx, dy)
        if r < 1e-4: continue
        d = Vector((dx / r, dy / r, 0.0))
        hit = ctree.ray_cast(Vector((0.0, AX_Y, v.co.z)), d, 1.0)[0]      # from the middle outwards: the first surface is the torso (the arms hang further out)
        if hit is None: continue
        rc = math.hypot(hit.x, hit.y - AX_Y)
        t = min(1.0, (v.co.z - CHEST_Z0) / (CHEST_Z1 - CHEST_Z0)); t = t * t * (3 - 2 * t)
        need = rc + CHEST_OVER
        if need > r:
            push = (need - r) * t
            v.co.x = d.x * (r + push); v.co.y = AX_Y + d.y * (r + push)
            moved += 1; worst_push = max(worst_push, push)
    log.append(f"waist over the chest: {moved} pants vertices pushed out, up to {worst_push * 100:.1f} cm")
else:
    log.append("WARNING: costume_v2.blend not found, waist not fitted over the chest")

# ---- a real cloth shell: thickness grows inward, hem and waistband get an edge ----
bpy.context.view_layer.objects.active = pants
sol = pants.modifiers.new("solidify", "SOLIDIFY"); sol.thickness = CLOTH; sol.offset = -1.0; sol.use_even_offset = False; sol.use_rim = True; sol.nonmanifold_thickness_mode = "CONSTRAINTS"
bpy.ops.object.modifier_apply(modifier=sol.name)
pants.data.materials.append(M["blue"])
for p in pants.data.polygons: p.use_smooth = True
log.append(f"Pants: {len(pants.data.vertices)} verts, {len(pants.data.polygons)} faces")

# ---- the belt and the loincloth flaps (straw yellow), fitted on the pants' outer surface ----
bpy.context.view_layer.update()
BELT_Z = J["Hips"].z - 0.02
pv = [v.co for v in pants.data.vertices if abs(v.co.z - BELT_Z) < 0.02]
rx = max(abs(p.x) for p in pv); ry_f = max(p.y for p in pv); ry_b = -min(p.y for p in pv)
bpy.ops.mesh.primitive_torus_add(location=(0, (ry_f - ry_b) / 2, BELT_Z), major_radius=1.0, minor_radius=0.030, major_segments=48, minor_segments=14)
belt = bpy.context.active_object; belt.name = "belt"
belt.scale = (rx + 0.004, (ry_f + ry_b) / 2 + 0.004, 0.8)
belt.data.materials.append(M["straw"]); bpy.ops.object.shade_smooth()
log.append(f"belt: rx {rx:.3f}, front {ry_f:.3f}, back {ry_b:.3f} at z {BELT_Z:.2f}")
FLAP = 0.34
def flap(name, sgn):
    # a slab hanging from the belt, leaning out with the leg: its top rests on the waist, its bottom clears the front / back of the thighs
    zt = BELT_Z - 0.03
    yt = max([v.co.y * sgn for v in pants.data.vertices if abs(v.co.z - BELT_Z) < 0.03 and abs(v.co.x) < 0.12] or [0.15]) + 0.014
    yb = max([v.co.y * sgn for v in pants.data.vertices if BELT_Z - FLAP <= v.co.z <= BELT_Z - 0.05 and abs(v.co.x) < 0.14] or [yt]) + 0.016
    yb = max(yb, yt)
    b = bmesh.new()
    top, bot, t = 0.15, 0.11, 0.012
    vs = []
    for dz, w, y in ((0.03, top, yt), (-FLAP + 0.03, bot, yb)):
        for dx in (-w, w):
            for dy in (-t, t): vs.append(b.verts.new((dx, y * sgn + dy, BELT_Z + dz)))
    f = lambda a, c, d, e: b.faces.new((vs[a], vs[c], vs[d], vs[e]))
    f(0, 1, 3, 2); f(4, 6, 7, 5); f(0, 4, 5, 1); f(2, 3, 7, 6); f(0, 2, 6, 4); f(1, 5, 7, 3)
    bmesh.ops.recalc_face_normals(b, faces=b.faces)
    bmesh.ops.bevel(b, geom=b.edges[:], offset=0.005, segments=2, affect="EDGES")
    me = bpy.data.meshes.new(name); b.to_mesh(me); b.free()
    o = bpy.data.objects.new(name, me); scene.collection.objects.link(o); o.data.materials.append(M["straw"])
    for p in o.data.polygons: p.use_smooth = True
    return o
flaps = [flap("loincloth_front", 1), flap("loincloth_back", -1)]
log.append(f"flaps at y +{max(v.co.y for v in flaps[0].data.vertices):.3f} / {min(v.co.y for v in flaps[1].data.vertices):.3f}")

# ---- join into the ONE game piece "CostumeLegs" (material slots plush_blue, straw_yellow: CostumeBuild tells them apart by name) ----
legs = join([pants, belt] + flaps, pants); legs.name = "CostumeLegs"; legs.data.name = "CostumeLegs"
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
pp = [legs.matrix_world @ v.co for v in legs.data.vertices]
log.append(f"CostumeLegs: {len(legs.data.vertices)} verts, {len(legs.data.polygons)} faces, materials {[m.name for m in legs.data.materials]}, "
           f"x {min(p.x for p in pp):.2f}..{max(p.x for p in pp):.2f} y {min(p.y for p in pp):.2f}..{max(p.y for p in pp):.2f} z {min(p.z for p in pp):.2f}..{max(p.z for p in pp):.2f} m")

# ---- save, export, render ----
if OUT_BLEND:
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
    log.append(f"saved {OUT_BLEND}")
if OUT_FBX:
    male.hide_set(True); female.hide_set(True)
    for o in bpy.context.view_layer.objects: o.select_set(False)
    legs.select_set(True); bpy.context.view_layer.objects.active = legs
    bpy.ops.export_scene.fbx(filepath=OUT_FBX, use_selection=True, object_types={'MESH'}, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False)
    male.hide_set(False); female.hide_set(False)
    log.append(f"exported {OUT_FBX}")
if RENDER:
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"; scene.display.shading.color_type = "MATERIAL"; scene.display.shading.show_cavity = True
    scene.render.resolution_x = 900; scene.render.resolution_y = 1000
    scene.world = bpy.data.worlds.new("w"); scene.world.color = (0.82, 0.86, 0.9)
    cam_d = bpy.data.cameras.new("cam"); cam_d.type = "ORTHO"; cam_d.ortho_scale = 1.1
    cam = bpy.data.objects.new("cam", cam_d); scene.collection.objects.link(cam); scene.camera = cam
    male.hide_render = True; female.hide_render = True
    for name, loc, rot in (("front", (0, 3, 0.58), (90, 0, 180)), ("side", (3, 0, 0.58), (90, 0, 90)), ("back", (0, -3, 0.58), (90, 0, 0)), ("three_quarter", (2.2, 2.2, 1.0), (75, 0, 135))):
        cam.location = loc; cam.rotation_euler = tuple(math.radians(a) for a in rot)
        scene.render.filepath = f"{RENDER}_{name}.png"; bpy.ops.render.render(write_still=True)
    # fit picture: pants + the male body from the front and the side (the body must stay inside)
    male.hide_render = False
    for name, loc, rot in (("side_with_body", (3, 0, 0.58), (90, 0, 90)), ("front_with_body", (0, 3, 0.58), (90, 0, 180))):
        cam.location = loc; cam.rotation_euler = tuple(math.radians(a) for a in rot)
        scene.render.filepath = f"{RENDER}_{name}.png"; bpy.ops.render.render(write_still=True)
    log.append(f"rendered {RENDER}_*.png")
print("PANTS_LOG\n" + "\n".join(log))
