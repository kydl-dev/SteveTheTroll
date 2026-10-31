# T-009b troll MASCOT costume model v1 (Claude-Agent-2, 2026-10-28; look signed off on block-out v5, D-073).
# Three pieces (helmet = head, chest = top with arms + mitts, legs = trousers + loincloth) built AROUND the player's own body in its
# rest pose (reference meshes exported from the rip: reference/player_male.obj, player_female.obj, player_joints.txt - never shipped).
# Soft plush shapes = metaball capsules along the player's bones, sized to enclose BOTH body types with a plush margin (fit check
# logged). No Blender rig: in game the pieces get their bone weights from the player's body at runtime (SkinTransfer, as the slippers).
# Run: blender -b --factory-startup --python scripts/costume_model.py -- <out_prefix> <out_blend>
# Axes: Unity (x, y, z) -> Blender (x, z, y): Blender Z up, the player faces +Y, the player's RIGHT is +X (same as the block-outs).
import bpy, bmesh, sys, math, os
from mathutils import Vector
from mathutils.bvhtree import BVHTree

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else "//costume_v1"
OUT_BLEND = argv[1] if len(argv) > 1 else ""
HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REF = os.path.join(HERE, "reference")

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
bodies = [load_obj("player_male"), load_obj("player_female")]

def mat(name, rgb, rough=0.9):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]; b.inputs["Base Color"].default_value = (*rgb, 1); b.inputs["Roughness"].default_value = rough
    return m
M = {
    "blue": mat("plush_blue", (0.30, 0.46, 0.56)),
    "blue_dark": mat("plush_blue_dark", (0.17, 0.27, 0.36)),
    "blue_light": mat("plush_blue_light", (0.48, 0.62, 0.68)),
    "straw": mat("straw_yellow", (0.80, 0.66, 0.28)),
    "eye": mat("eye_orange", (1.0, 0.35, 0.02), 0.3),
    "pupil": mat("pupil_black", (0.01, 0.01, 0.01), 0.3),
    "fang": mat("fang_white", (0.92, 0.90, 0.82), 0.4),
    "claw": mat("claw_dark", (0.05, 0.05, 0.06), 0.5),
    "body": mat("reference_skin", (0.8, 0.5, 0.4)),
}
for b in bodies: b.data.materials.append(M["body"])

RES = 0.018
def mball(name):
    mb = bpy.data.metaballs.new(name); mb.resolution = RES; mb.render_resolution = RES; mb.threshold = 0.6
    o = bpy.data.objects.new(name, mb); scene.collection.objects.link(o)
    return o

def capsule(o, a, b, r, stiff=2.0):
    a, b = Vector(a), Vector(b)
    e = o.data.elements.new(type="CAPSULE")
    d = b - a
    e.co = (a + b) / 2; e.radius = r; e.stiffness = stiff
    e.size_x = max(d.length / 2, 0.001)
    e.rotation = Vector((1, 0, 0)).rotation_difference(d.normalized())
    return e

def ball(o, c, r, stiff=2.0, scale=(1, 1, 1)):
    e = o.data.elements.new(type="ELLIPSOID")
    e.co = Vector(c); e.radius = r; e.stiffness = stiff
    e.size_x, e.size_y, e.size_z = scale
    return e

S = float(os.environ.get("STT_PLUSH", "1.8"))      # metaball radius factor: 1.7 encloses both bodies (fit check), 1.8 = plush margin
def side(n, s): return n.replace("Left", "Right") if s > 0 else n       # s = +1 right (+X), -1 left

DROP = Vector((0, 0, -float(os.environ.get("STT_SHDROP", "0.12"))))   # v2e: shoulder bulge sits lower (block-out: collar above the shoulders)
top = mball("Costume_Top")
for a, b, r in (("Hips", "Spine", 0.20), ("Spine", "Spine1", 0.21), ("Spine1", "Spine2", 0.215), ("Spine2", "Neck", 0.17)):
    capsule(top, J[a], J[b], r * S)
ball(top, J["Spine1"] + Vector((0, 0.06, -0.05)), 0.18 * S, scale=(1.0, 0.8, 1.2))                # tummy
for s in (1, -1):
    capsule(top, J[side("LeftShoulder", s)] + DROP, J[side("LeftArm", s)] + DROP, 0.13 * S)   # v2e: shoulders lowered under the collar
    capsule(top, J[side("LeftArm", s)], J[side("LeftForeArm", s)], 0.105 * S)
    capsule(top, J[side("LeftForeArm", s)], J[side("LeftHand", s)], 0.09 * S)
    capsule(top, J[side("LeftHand", s)], J[side("LeftHandMiddle3", s)], 0.07 * S)                  # mitt
    capsule(top, J[side("LeftHandIndex1", s)], J[side("LeftHandIndex3", s)], 0.04 * S)
    capsule(top, J[side("LeftHandPinky1", s)], J[side("LeftHandPinky3", s)], 0.04 * S)
    capsule(top, J[side("LeftHandThumb1", s)], J[side("LeftHandThumb3", s)], 0.038 * S)

# v2 (user on v1: "the pants look weird, like a wanna be dress ... no feet?"): trousers = a short seat (no big hip ball that
# flared into a skirt) and two straight legs, each its own metaball family; the legs END in a cuff above the ankle so the
# Fluffy Slippers (shown in the render) are the feet
ANKLE = 0.20                                                           # cuff height above the ground
leg_objs = []
for s in (1, -1):
    lg = mball("Costume_Leg" + ("R" if s > 0 else "L"))
    up, knee, foot = J[side("LeftUpLeg", s)], J[side("LeftLeg", s)], J[side("LeftFoot", s)]
    # D-159 (user: "pants had fabric between the legs"): the two seats overlapped across the crotch (centres 0.12 m apart, radius 0.13) and the cloth bridged the legs.
    # Each leg's seat now sits straight above ITS hip joint with a smaller radius, so the legs stay separate down to the crotch.
    seat = Vector((up.x, J["Hips"].y - 0.01, J["Hips"].z))
    capsule(lg, seat, up + Vector((0, 0, -0.04)), 0.115 * S)              # seat / hip of this leg
    log.append(f"leg {s}: seat x {seat.x:.3f}, gap between the two seats' surfaces = {2 * abs(seat.x) - 2 * 0.115 * S:.3f} m")
    capsule(lg, up, knee, 0.125 * S)
    capsule(lg, knee, Vector((foot.x, foot.y, ANKLE + 0.04)), 0.105 * S)
    leg_objs.append(lg)

def to_mesh(o, name):
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
    m = bpy.data.objects.new(name, me); scene.collection.objects.link(m)
    bpy.data.objects.remove(o)
    for p in m.data.polygons: p.use_smooth = True
    return m

top_m = to_mesh(top, "Costume_Top_Mesh")
legs_parts = [to_mesh(lg, lg.name + "_Mesh") for lg in leg_objs]
# D-159 (user: "fabric between the legs"): the two plush legs overlap across the middle (hip joints only 0.2 m apart, leg radius ~0.2 m), so the cloth fused into one sheet between the
# thighs. Each leg is cut at the body's middle plane (a 2 cm gap) and the cut is closed, so there are two separate trouser legs.
for lo, sgn in zip(legs_parts, (1, -1)):
    bm = bmesh.new(); bm.from_mesh(lo.data)
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    r = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(sgn * 0.01, 0, 0), plane_no=(sgn, 0, 0), clear_inner=True)
    cut = [e for e in r["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
    if cut: bmesh.ops.triangle_fill(bm, edges=cut, use_beauty=True)
    bm.to_mesh(lo.data); bm.free()
    log.append(f"leg {sgn} cut at the middle plane: {len(lo.data.vertices)} verts, {len(cut)} cut edges, origin {tuple(round(v, 3) for v in lo.matrix_world.translation)}")
bpy.context.view_layer.update()
def bvh_one(o):
    me = bpy.data.meshes.new("tmp"); t = bmesh.new(); t.from_mesh(o.data); t.transform(o.matrix_world); t.to_mesh(me); t.free()
    bm = bmesh.new(); bm.from_mesh(me); bpy.data.meshes.remove(me); tree = BVHTree.FromBMesh(bm); bm.free(); return tree
leg_trees = [bvh_one(o) for o in legs_parts]   # fit test per leg BEFORE joining (overlapping legs confuse one surface test)
bpy.ops.object.select_all(action="DESELECT")
for o in legs_parts: o.select_set(True)
bpy.context.view_layer.objects.active = legs_parts[0]
bpy.ops.object.join()
legs_m = bpy.context.active_object; legs_m.name = "Costume_Legs_Mesh"
# flat the bottom of each leg: cut everything below the cuff line, then a plush cuff ring closes it
bm = bmesh.new(); bm.from_mesh(legs_m.data)
bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, ANKLE), plane_no=(0, 0, 1), clear_inner=True)
bm.to_mesh(legs_m.data); bm.free()

hc = J["Head"] + Vector((0, 0.03, float(os.environ.get("STT_HEADUP", "0.18"))))   # v2e: mascot head sits higher (v2: 0.10), on top of the collar
def prim(kind, name, loc, scale, rot=(0, 0, 0), material="blue", **kw):
    ops = {"sphere": bpy.ops.mesh.primitive_uv_sphere_add, "cone": bpy.ops.mesh.primitive_cone_add, "torus": bpy.ops.mesh.primitive_torus_add}
    if kind == "sphere": kw.setdefault("segments", 48); kw.setdefault("ring_count", 24)
    ops[kind](location=tuple(loc), rotation=[math.radians(a) for a in rot], **kw)
    o = bpy.context.active_object; o.name = name; o.scale = scale; o.data.materials.append(M[material])
    bpy.ops.object.shade_smooth()
    return o
head_parts = [
    prim("sphere", "head", hc, (0.31, 0.29, 0.30)),
    prim("sphere", "head_cap", hc + Vector((0, -0.06, 0.08)), (0.29, 0.26, 0.21), material="blue_dark"),
    prim("sphere", "brow", hc + Vector((0, 0.22, 0.08)), (0.22, 0.08, 0.06), material="blue_dark"),
    prim("sphere", "nose", hc + Vector((0, 0.29, -0.04)), (0.06, 0.07, 0.07), material="blue_dark"),
    prim("sphere", "muzzle", hc + Vector((0, 0.20, -0.14)), (0.18, 0.12, 0.09), material="blue_light"),
]
for s in (1, -1):
    head_parts += [prim("cone", f"ear_{s}", hc + Vector((s * 0.33, -0.02, 0.08)), (0.06, 0.03, 0.16), (0, s * 70, 0)),
                   prim("sphere", f"eye_{s}", hc + Vector((s * 0.10, 0.25, 0.02)), (0.055, 0.03, 0.05), material="eye"),
                   prim("sphere", f"pupil_{s}", hc + Vector((s * 0.10, 0.275, 0.02)), (0.022, 0.012, 0.03), material="pupil"),
                   prim("cone", f"fang_{s}", hc + Vector((s * 0.07, 0.28, -0.20)), (0.025, 0.02, 0.05), (180, 0, 0), material="fang")]
for i, (x, z, tilt) in enumerate(((0, 0.34, 0), (0.08, 0.31, 25), (-0.08, 0.31, -25), (0.04, 0.28, 12), (-0.04, 0.28, -12))):
    head_parts.append(prim("cone", f"hair_{i}", hc + Vector((x, -0.04, z)), (0.05, 0.05, 0.12), (0, tilt, 0), material="straw"))

# v2c (user: "give the hands similar nails, spiky like that" - as the plush feet, copying the in-game troll): a claw on each
# fingertip of the mitts (thumb, index, middle, pinky), set ON the mitt surface by casting from the finger bone outwards
bpy.context.view_layer.update()
top_tree = bvh_one(top_m)
hand_claws = []
for s in (1, -1):
    for fg in ("Thumb", "Index", "Middle", "Pinky"):
        a, b = J[side(f"LeftHand{fg}2", s)], J[side(f"LeftHand{fg}3", s)]
        d = (b - a).normalized()
        hit, nor, idx, dist = top_tree.ray_cast(b, d, 0.5)
        if hit is None: log.append(f"claw {fg} {s}: no mitt surface hit"); continue
        c = prim("cone", f"Costume_Top_claw_{s}_{fg}", hit + d * 0.012, (0.02, 0.02, 0.035) if fg != "Thumb" else (0.018, 0.018, 0.03), material="fang")
        c.rotation_euler = Vector((0, 0, 1)).rotation_difference(d).to_euler()
        hand_claws.append(c)
log.append(f"hand claws: {len(hand_claws)}")
# v2d (user: "make sure the front cloth is visible better, in costume_blockout.blend the waist was so much better"): the belt and
# the loincloth are FITTED to the suit's outer surface (rays cast inwards from outside, so they never sink into the plush), and the
# flaps are back to the block-out size (wide straw aprons hanging from the belt, front and back)
bpy.context.view_layer.update()
outer = [bvh_one(top_m), bvh_one(legs_m)]
def surf(z, ang):                                                       # outermost suit surface at height z, direction ang (0 = front)
    d = Vector((math.sin(ang), math.cos(ang), 0)); best = 0.0
    for t in outer:
        hit = t.ray_cast(Vector((0, 0, z)) + d * 0.45, -d, 0.45)[0]      # from 0.45 m: inside the hanging arms
        if hit is not None: best = max(best, Vector((hit.x, hit.y, 0)).length)
    return best
BELT_Z = J["Hips"].z - 0.02
rx = surf(BELT_Z, math.pi / 2); ry = max(surf(BELT_Z, 0), surf(BELT_Z, math.pi))
belt = prim("torus", "belt", (0, 0, BELT_Z), (1.0, (ry + 0.01) / (rx + 0.01), 0.7), material="straw", major_radius=rx + 0.01, minor_radius=0.045)
FLAP = 0.32                                                             # flap length below the belt
loin = []
for name, ang, sgn in (("loincloth_front", 0.0, 1), ("loincloth_back", math.pi, -1)):
    y = max(surf(BELT_Z - FLAP * k / 6, ang) for k in range(7)) + 0.035  # in front of the plush along its whole length
    loin.append(prim("cone", name, (0, sgn * y, BELT_Z - FLAP / 2 - 0.01), (0.24, 0.035, FLAP / 2), (180, 0, 0), material="straw"))
log.append(f"belt rx {rx:.2f} ry {ry:.2f} at z {BELT_Z:.2f}; flaps at y +-{abs(loin[0].location.y):.2f} / {abs(loin[1].location.y):.2f}")
# v2e (user: the collar "in the blockout was nicer ... the shoulders are higher than it and it seems bigger"): the straw collar rests
# ON TOP of the plush shoulders, round the neck, fitted like the belt (rays on the suit top), block-out size tube
top_only = outer[0]
def top_surf(z, ang):
    d = Vector((math.sin(ang), math.cos(ang), 0))
    hit = top_only.ray_cast(Vector((0, 0, z)) + d * 0.45, -d, 0.45)[0]
    return Vector((hit.x, hit.y, 0)).length if hit is not None else 0.0
# under the big mascot head (the block-out look): level, just below the muzzle, resting on the plush shoulders
COL_Z = hc.z - float(os.environ.get("STT_COLDZ", "0.28"))
for k in range(8): log.append(f"  top surface at z {COL_Z - 0.05 + k * 0.02:.2f}: x {top_surf(COL_Z - 0.05 + k * 0.02, math.pi / 2):.2f} y {top_surf(COL_Z - 0.05 + k * 0.02, 0):.2f}")
cx, cy = top_surf(COL_Z, math.pi / 2), max(top_surf(COL_Z, 0), top_surf(COL_Z, math.pi))
RX, RY = max(0.27, cx + 0.01), max(0.24, cy + 0.01)
collar = prim("torus", "collar", (0, -0.01, COL_Z), (1.0, RY / RX, 0.55), material="straw", major_radius=RX, minor_radius=0.065)   # level, just under the chin (block-out)
log.append(f"collar: ring at z {COL_Z:.2f}, rx {RX:.2f} ry {RY:.2f} (plush there {cx:.2f} / {cy:.2f})")
for s in (1, -1):
    f = J[side("LeftFoot", s)]
    # D-165 (user 2026-10-31: "a blue line at the bottom, from one leg to the other, at the level of the slippers"): the protruding cuff ring was wide enough that the two rings
    # touched in the middle and read as ONE blue bar across both feet. The trousers now simply end at the ankle (closed by the flat cap below, narrower than the leg); no ring.
    prim("sphere", f"cuff_cap_{s}", (f.x, f.y, ANKLE), (0.105 * S * 0.62, 0.105 * S * 0.62, 0.02), material="blue_dark")

# the Fluffy Slippers as the feet (our own model, exports/Slipper.fbx; worn fit: 0.097 m ahead of the foot bone, on the ground, x1.1)
slipper_fbx = os.path.join(HERE, "exports", "Slipper.fbx")
if os.path.exists(slipper_fbx):
    for s in (1, -1):
        before = set(scene.objects)
        bpy.ops.import_scene.fbx(filepath=slipper_fbx)
        new = [o for o in scene.objects if o not in before]
        f = J[side("LeftFoot", s)]; toe = J[side("LeftToeBase", s)]
        fwd = Vector((toe.x - f.x, toe.y - f.y, 0)).normalized()
        for o in new:
            if o.type != "MESH": continue
            o.parent = None
            o.location = Vector((f.x, f.y, 0)) + fwd * 0.097
            o.rotation_euler = (0, 0, math.atan2(-fwd.x, fwd.y))
            o.scale = (1.1 * 1.05, 1.1, 1.1)
            o.name = f"slipper_{'R' if s > 0 else 'L'}_{o.name}"
        for o in new:
            if o.type != "MESH": bpy.data.objects.remove(o)
    log.append("slippers: shown from exports/Slipper.fbx")
else:
    log.append("slippers: exports/Slipper.fbx missing - no feet in the render")

def bvh(objs):
    bm = bmesh.new()
    for o in objs:
        me = bpy.data.meshes.new("tmp"); t = bmesh.new(); t.from_mesh(o.data); t.transform(o.matrix_world); t.to_mesh(me); t.free()
        bm.from_mesh(me); bpy.data.meshes.remove(me)
    tree = BVHTree.FromBMesh(bm); bm.free(); return tree

# v2b (user: "maybe don't hide the feet if no slippers are used?"): the legs piece also carries plush troll FEET below the cuffs
# (own objects "Costume_Feet_*"); in game they are shown only while the Fluffy Slippers are NOT worn - the slippers replace them
feet_objs = []; feet_trees = []
for s in (1, -1):
    f = J[side("LeftFoot", s)]; toe = J[side("LeftToeBase", s)]
    fwd = Vector((toe.x - f.x, toe.y - f.y, 0)).normalized(); rgt = Vector((fwd.y, -fwd.x, 0)) * s
    base = Vector((f.x, f.y, 0))
    fm = mball("Costume_Foot" + ("R" if s > 0 else "L"))
    capsule(fm, base + Vector((0, 0, ANKLE + 0.02)), base + fwd * 0.02 + Vector((0, 0, 0.09)), 0.10 * S)      # ankle into the cuff
    ball(fm, base - fwd * 0.03 + Vector((0, 0, 0.07)), 0.10 * S, scale=(1.0, 1.0, 0.8))                    # heel
    capsule(fm, base + Vector((0, 0, 0.07)), base + fwd * 0.20 + Vector((0, 0, 0.06)), 0.085 * S)          # long troll foot
    fo = to_mesh(fm, "Costume_Feet_" + ("R" if s > 0 else "L"))
    bm = bmesh.new(); bm.from_mesh(fo.data)                                                                # flat sole on the ground
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, 0.0), plane_no=(0, 0, 1), clear_inner=True)
    bm.to_mesh(fo.data); bm.free()
    fo.data.materials.append(M["blue"]); feet_objs.append(fo)
    for i, k in enumerate((-1, 0, 1)):                                                                     # three toes with claws
        tp = base + fwd * 0.27 + Vector((fwd.y, -fwd.x, 0)) * (k * 0.06) + Vector((0, 0, 0.045))
        t = prim("sphere", f"Costume_Feet_toe_{s}_{i}", tp, (0.045, 0.05, 0.04), material="blue_dark")
        c = prim("cone", f"Costume_Feet_claw_{s}_{i}", tp + fwd * 0.06 + Vector((0, 0, 0.0)), (0.022, 0.022, 0.04), material="fang")
        c.rotation_euler = (math.radians(-90), 0, math.atan2(-fwd.x, fwd.y))
        feet_objs += [t, c]
bpy.context.view_layer.update()
feet_trees = [bvh([o]) for o in feet_objs if "claw" not in o.name]          # per object (overlaps confuse one tree)
slippers = [o for o in scene.objects if o.name.startswith("slipper_")]

def colour(o, rules):
    me = o.data
    for k in ("blue", "blue_dark", "blue_light"): me.materials.append(M[k])
    for p in me.polygons:
        c = o.matrix_world @ p.center; n = p.normal
        p.material_index = rules(c, n)
def top_rules(c, n):
    if n.y > 0.35 and (c.x / 0.18) ** 2 + ((c.z - 1.15) / 0.26) ** 2 < 1: return 2               # oval belly (v1: a box)
    if any((c - J[side("LeftHand", s)]).length < 0.13 and n.z < -0.2 for s in (1, -1)): return 2
    if (n.y < -0.35 and c.z > 1.10) or (c.z > J["Spine2"].z + 0.03 and abs(c.x) > 0.12): return 1   # upper back, shoulders
    if abs(c.x) > 0.28 and n.x * c.x > 0.5 * abs(c.x): return 1
    return 0
def legs_rules(c, n):
    if n.x * c.x > 0.55 * abs(c.x) and abs(c.x) > 0.12: return 1                                 # outer legs
    if n.y < -0.4 and c.z > 0.75: return 1                                                        # seat
    return 0
colour(top_m, top_rules); colour(legs_m, legs_rules)

bpy.context.view_layer.update()                  # object scales set after creation must reach matrix_world first
# a body point is covered when it lies INSIDE any piece (pieces overlap: one surface test over all of them is wrong)
pieces = [("top", bvh([top_m])), ("legR", leg_trees[0]), ("legL", leg_trees[1]), ("head", bvh([head_parts[0]]))]
def inside(tree, p):
    loc, nor, idx, dist = tree.find_nearest(p)
    return loc is not None and (p - loc).dot(nor) <= 0.002, dist
for b in bodies:
    out = 0; worst = 0.0; worst_at = None; n = 0; bands = {}; fn = 0; fout = 0; fworst = (0.0, None)
    for v in b.data.vertices:
        p = b.matrix_world @ v.co
        if p.z < ANKLE:                                                # below the cuffs: the plush feet (or the slippers)
            fn += 1
            rs = [inside(t, p) for t in feet_trees]; r = (any(x[0] for x in rs), min(x[1] for x in rs))
            if not r[0]: fout += 1; fworst = max(fworst, (r[1], tuple(round(x, 2) for x in p)))
            continue
        n += 1
        res = [inside(t, p) for _, t in pieces]
        k = round(p.z, 1); t = bands.setdefault(k, [0, 0]); t[1] += 1
        if any(r[0] for r in res): continue
        out += 1; t[0] += 1
        d = min(r[1] for r in res)
        if d > worst: worst, worst_at = d, p
    log.append(f"FIT {b.name}: {out} of {n} body points outside the suit, worst {worst*100:.1f} cm at {tuple(round(x,2) for x in worst_at) if worst_at else '-'}")
    log.append(f"  feet: {fout} of {fn} foot points outside the plush feet, worst {fworst[0]*100:.1f} cm at {fworst[1]}")
    log.append("  by height: " + ", ".join(f"{k:.1f}m {t[0]}/{t[1]}" for k, t in sorted(bands.items()) if t[0]))

for o in (top_m, legs_m):
    pts = [o.matrix_world @ v.co for v in o.data.vertices]
    sz = [max(p[i] for p in pts) - min(p[i] for p in pts) for i in range(3)]
    log.append(f"{o.name}: {len(o.data.vertices)} verts, size x {sz[0]:.2f} y {sz[1]:.2f} z {sz[2]:.2f} m")

bpy.ops.object.light_add(type="SUN", rotation=(math.radians(50), 0, math.radians(140))); bpy.context.active_object.data.energy = 3.0
bpy.ops.object.light_add(type="AREA", location=(-2.0, 2.5, 2.5)); bpy.context.active_object.data.energy = 300
world = bpy.data.worlds.new("w"); scene.world = world; world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.18, 0.12, 0.08, 1)
for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
    try: scene.render.engine = eng; break
    except TypeError: continue
scene.render.resolution_x, scene.render.resolution_y = 800, 1000
bpy.ops.object.camera_add(); cam = bpy.context.active_object; cam.data.lens = 50; scene.camera = cam
target = Vector((0, 0, 1.05))
for b in bodies: b.hide_render = True
for variant, show_feet in (("", False), ("_feet", True)):
    for o in feet_objs: o.hide_render = not show_feet
    for o in slippers: o.hide_render = show_feet
    for view, loc in (("front34", (2.6, 4.2, 1.6)), ("side", (5.0, 0.0, 1.3)), ("back34", (-2.8, -4.0, 1.7))):
        cam.location = loc; cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = f"{OUT}{variant}_{view}.png"; bpy.ops.render.render(write_still=True); log.append(f"render {view}: {scene.render.filepath}")
for o in feet_objs: o.hide_render = False
if OUT_BLEND:
    for b in bodies: bpy.data.objects.remove(b)        # the game's player bodies are reference only: never saved into a tracked .blend (D-014)
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
print("COSTUME_LOG\n" + "\n".join(log))
