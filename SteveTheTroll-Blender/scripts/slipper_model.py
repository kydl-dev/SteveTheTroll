# T-005 slipper model v1 (Claude-Agent-1, 2026-10-27). D-010 look, D-021 "way prettier" than the primitive block-out.
# v1 loft (metaballs rippled, rim wandered: replaced). Plush slipper for the Valheim player foot (reference player in the Unity fit scene: foot 0.32 m heel-toe, 0.145 m wide incl. ankle,
# foot bone 0.125 m above the ground). Soft body = metaball union (plush, no hard seams) -> mesh, ankle opening cut, thick fluffy rim,
# black sole with dotted grip pattern. Z up, toe toward +Y, ground at Z = 0, heel at Y ~ -0.17. 1 unit = 1 m.
# Run: blender -b --factory-startup --python scripts/slipper_model.py -- <out_dir> <out_blend>
import bpy, bmesh, sys, math, os
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT_DIR = argv[0] if argv else "//renders"
OUT_BLEND = argv[1] if len(argv) > 1 else ""
LOG = []

L, W = 0.335, 0.165            # slipper length / width (foot 0.32 x ~0.12 + plush thickness)
SOLE_T = 0.018                # sole thickness
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

def mat(name, rgb, rough=0.85, sheen=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1)
    b.inputs["Roughness"].default_value = rough
    if "Sheen Weight" in b.inputs: b.inputs["Sheen Weight"].default_value = sheen
    return m

M_PLUSH = mat("slipper_plush", (0.19, 0.4275, 0.5225), 0.9, 1.0)   # v2: 5% darker (user)
M_RIM = mat("slipper_rim", (0.70, 0.68, 0.64), 0.95, 1.0)
M_SOLE = mat("slipper_sole", (0.015, 0.015, 0.017), 0.55)
M_LINING = mat("slipper_lining", (0.45, 0.43, 0.40), 0.95, 0.5)

def outline(t):
    """Half-width of the foot outline at t = 0 (heel) .. 1 (toe): narrow heel, wide ball, round toe."""
    heel = 0.62 + 0.38 * math.sin(min(t / 0.25, 1.0) * math.pi / 2)        # rounded by the cap rings, not pinched
    toe = 0.55 + 0.45 * math.sqrt(max(0.0, 1.0 - ((t - 0.80) / 0.20) ** 2)) if t > 0.80 else 1.0
    ball = 0.82 + 0.18 * math.exp(-((t - 0.68) / 0.22) ** 2)
    return 0.5 * W * heel * toe * ball

def height(t):
    """Top of the plush upper above the ground at t (0 heel .. 1 toe): high heel collar, instep, low rounded toe."""
    collar = 0.125 * math.exp(-((t - 0.04) / 0.16) ** 2)
    instep = 0.098 * math.exp(-((t - 0.55) / 0.30) ** 2)
    toe = 0.072
    return max(toe, collar, instep)

# --- soft upper: a lofted shell of superellipse rings (flat bottom on the sole), closed at heel and toe, then the ankle
#     opening is cut out of the top, Solidify makes a plush wall (lining inside), Subdivision makes it soft.
NT, NR = 28, 24                     # rings along the foot, points per ring
OPEN_T0, OPEN_T1 = 0.07, 0.60       # the opening runs from just in front of the heel wall to the instep
bm = bmesh.new()
rings = []
CAP = [(0.3, 0.94), (0.6, 0.80), (0.85, 0.56), (1.0, 0.22)]      # (share of the cap length, ring size) -> dome
CAP_LEN = 0.03
layout = [(0.0, -CAP_LEN * d, s) for d, s in reversed(CAP)]
layout += [(0.5 - 0.5 * math.cos(math.pi * i / NT), 0.0, 1.0) for i in range(NT + 1)]
layout += [(1.0, CAP_LEN * d, s) for d, s in CAP]
for te, dy, shrink in layout:
    y = -L / 2 + te * L + dy
    a = max(outline(te), 0.006)
    top = height(te)
    c = (top - 0.004) * 0.5
    zc = 0.004 + c
    ring = []
    for k in range(NR):
        phi = -math.pi / 2 + 2 * math.pi * k / NR    # k = 0: bottom centre
        cx, sz = math.cos(phi), math.sin(phi)
        lower = sz < 0                                  # v3 (user: foot skin showed between sole and upper): boxy lower half,
        x = a * shrink * math.copysign(abs(cx) ** (0.25 if lower else 0.55), cx)   # full width down to the sole
        z = zc + c * shrink * math.copysign(abs(sz) ** 0.75, sz)
        z = max(z, 0.004)                             # and the wall goes down INTO the sole (sole top = SOLE_T), no gap
        ring.append(bm.verts.new((x, y, z)))
    rings.append((te, ring))
faces_open = []
for i in range(len(rings) - 1):
    (t0, r0), (t1, r1) = rings[i], rings[i + 1]
    for k in range(NR):
        f = bm.faces.new((r0[k], r0[(k + 1) % NR], r1[(k + 1) % NR], r1[k]))
        top_face = all(math.sin(-math.pi / 2 + 2 * math.pi * kk / NR) > 0.30 for kk in (k, (k + 1) % NR))
        if top_face and OPEN_T0 <= 0.5 * (t0 + t1) <= OPEN_T1 and t0 != t1:
            faces_open.append(f)
for te, ring in (rings[0], rings[-1]):
    bm.faces.new(ring if te > 0.5 else list(reversed(ring)))          # tiny end rings: the dome closes there
bmesh.ops.delete(bm, geom=faces_open, context='FACES')
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
me = bpy.data.meshes.new("slipper_body")
bm.to_mesh(me)
bm.free()
body = bpy.data.objects.new("slipper_body", me)
scene.collection.objects.link(body)
me.materials.append(M_PLUSH)
me.materials.append(M_LINING)
sol = body.modifiers.new("plush_wall", 'SOLIDIFY')
sol.thickness = 0.012
sol.offset = -1.0
sol.material_offset = 1                              # inner wall = lining
sol.use_rim = True
sub = body.modifiers.new("soft", 'SUBSURF')
sub.levels = sub.render_levels = 1                   # game mesh budget (level 2 = 26k verts on the upper)
for p in me.polygons: p.use_smooth = True
LOG.append(f"body: {NT + 1} rings x {NR}, {len(faces_open)} faces cut for the opening, dims {tuple(round(d, 3) for d in body.dimensions)}")

# --- thick fluffy rim along the opening's boundary loop (taken from the cut, so it always sits on the edge)
bm = bmesh.new()
bm.from_mesh(me)
edge_loop = [e for e in bm.edges if e.is_boundary]
start = edge_loop[0].verts[0]
order, prev, cur = [start], None, start
while True:
    nxt = [e.other_vert(cur) for e in cur.link_edges if e.is_boundary and e.other_vert(cur) is not prev]
    if not nxt or nxt[0] is start: break
    prev, cur = cur, nxt[0]
    order.append(cur)
raw = [Vector((v.co.x * 1.04, v.co.y, v.co.z + 0.004)) for v in order]
for _ in range(6):                                   # smooth the zig-zag of the cut edge
    raw = [(raw[i - 1] + raw[i] * 2 + raw[(i + 1) % len(raw)]) / 4 for i in range(len(raw))]
seg = [(raw[(i + 1) % len(raw)] - raw[i]).length for i in range(len(raw))]
total, NP, pts, acc, j = sum(seg), 64, [], 0.0, 0
for n in range(NP):                                  # even spacing -> an even tube
    want = total * n / NP
    while acc + seg[j] < want: acc += seg[j]; j += 1
    f = (want - acc) / max(seg[j], 1e-9)
    p = raw[j].lerp(raw[(j + 1) % len(raw)], f)
    pts.append((p.x, p.y, p.z))
bm.free()
crv = bpy.data.curves.new("rim_curve", 'CURVE')
crv.dimensions = '3D'
sp = crv.splines.new('BEZIER')
sp.bezier_points.add(len(pts) - 1)
for bp, p in zip(sp.bezier_points, pts):
    bp.co = p
    bp.handle_left_type = bp.handle_right_type = 'AUTO'
sp.use_cyclic_u = True
crv.bevel_depth = 0.016
crv.bevel_resolution = 3
crv.resolution_u = 3                                  # game mesh budget (v2 export: rim 12k verts -> ~2.5k)
rim = bpy.data.objects.new("slipper_rim", crv)
scene.collection.objects.link(rim)
crv.materials.append(M_RIM)
bpy.context.view_layer.objects.active = rim
for o in bpy.context.selected_objects: o.select_set(False)
rim.select_set(True)
bpy.ops.object.convert(target='MESH')
bpy.ops.object.shade_smooth()
LOG.append(f"rim: {len(pts)} boundary points, z {min(p[2] for p in pts):.3f}..{max(p[2] for p in pts):.3f} m")

# --- black sole: outline slab with rounded edges + grip dots underneath
bm = bmesh.new()
K = 40
ring = []
for k in range(K):
    t = k / (K - 1)
    ring.append((outline(t) * 1.0 + 0.002, -L / 2 - CAP_LEN * 0.9 + t * (L + CAP_LEN * 1.8)))     # v2: narrower (user), still covers the domes
verts = [bm.verts.new((x, y, 0.0)) for x, y in ring] + [bm.verts.new((-x, y, 0.0)) for x, y in reversed(ring)]
face = bm.faces.new(verts)
r = bmesh.ops.extrude_face_region(bm, geom=[face])
for v in [e for e in r["geom"] if isinstance(e, bmesh.types.BMVert)]:
    v.co.z = SOLE_T
sole_me = bpy.data.meshes.new("slipper_sole")
bm.to_mesh(sole_me)
bm.free()
sole = bpy.data.objects.new("slipper_sole", sole_me)
scene.collection.objects.link(sole)
sole_me.materials.append(M_SOLE)
bev = sole.modifiers.new("round", 'BEVEL'); bev.width = 0.006; bev.segments = 4
dots = 0
for gy in range(-7, 8):
    for gx in range(-3, 4):
        x, y = gx * 0.018 + (0.009 if gy % 2 else 0), gy * 0.022
        t = (y + L / 2) / L
        if abs(x) > outline(t) * 0.85 - 0.006: continue
        bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=0.0045, depth=0.003, location=(x, y, -0.0012))
        d = bpy.context.active_object
        d.data.materials.append(M_SOLE)
        d.parent = sole
        dots += 1
LOG.append(f"sole: outline {K * 2} verts, {dots} grip dots")

# --- render-only fluff (the game gets its fur from the ShellFur shader): short particle hair on the plush body and the rim
def fluff(obj, count, length, mat_index_only=None):
    ps = obj.modifiers.new("fluff", 'PARTICLE_SYSTEM').particle_system
    s = ps.settings
    s.type = 'HAIR'
    s.count = count
    s.hair_length = length
    s.child_type = 'INTERPOLATED'
    s.child_percent = 12
    s.rendered_child_count = 12
    s.root_radius, s.tip_radius = 0.0012, 0.0002
    s.material = 1
    s.roughness_1 = 0.4
    s.roughness_endpoint = 0.3
    return ps
# particle hair rendered metres long in Blender 5.2 (hair_length not honoured, v1 renders) - the game fur is the ShellFur shader;
# the renders show the shape only (D-021 sign-off of the SHAPE).
LOG.append("fluff: none in the render (shape sign-off)")

# --- size checks against the reference foot (heel-toe 0.32 m, width ~0.12 m)
dims = [round(v, 3) for v in body.dimensions]
LOG.append(f"BODY dims x{dims[0]} y{dims[1]} z{dims[2]} m (expected x~0.16 y~0.37 z~0.14)")

# --- lights, camera, renders: 3/4 front-left (like the picture), side, from below
world = bpy.data.worlds.new("w"); scene.world = world
world.color = (0.16, 0.11, 0.08)
bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), 0, math.radians(140))); bpy.context.active_object.data.energy = 3.5
bpy.ops.object.light_add(type='AREA', location=(-0.5, 0.6, 0.6)); bpy.context.active_object.data.energy = 40
bpy.ops.object.light_add(type='AREA', location=(0.4, -0.6, 0.3)); bpy.context.active_object.data.energy = 15
bpy.ops.object.camera_add()
cam = bpy.context.active_object
scene.camera = cam
cam.data.lens = 60
scene.render.engine = 'BLENDER_EEVEE'
scene.render.resolution_x, scene.render.resolution_y = 1000, 750
os.makedirs(OUT_DIR, exist_ok=True)
views = {"front34": (Vector((0.55, 0.62, 0.38)), Vector((0, 0.02, 0.06))),
         "side": (Vector((0.85, 0.0, 0.12)), Vector((0, 0, 0.06))),
         "back34": (Vector((-0.45, -0.6, 0.45)), Vector((0, -0.03, 0.07))),
         "below": (Vector((0.35, 0.25, -0.45)), Vector((0, 0, 0.0)))}
for name, (pos, tgt) in views.items():
    cam.location = pos
    cam.rotation_euler = (tgt - pos).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = os.path.join(OUT_DIR, f"slipper_v3_{name}.png")
    bpy.ops.render.render(write_still=True)
    LOG.append(f"render {name} -> {scene.render.filepath}")
if OUT_BLEND:
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
print("SLIPPER_LOG\n" + "\n".join(LOG))
