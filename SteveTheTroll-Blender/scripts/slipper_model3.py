# D-166 (user 2026-11-01: "Start from 0 ... new slippers") - SLIPPER v3, written from scratch (Claude-Agent-18).
# The look is the reference picture (Steve the Troll plush slippers, D-010): a soft light-blue plush shoe, a tall round fur-lined opening over the ankle, a black dotted
# non-slip sole. The troll is NOT here: it is baked onto the slipper in Unity (SlipperBuild.cs) from the game's own troll, posed with the fit (TrollFitData).
# Built in the player's REST-POSE frame for the LEFT foot (the right slipper is its mirror): the slipper stands on the ground (z 0), the foot bone is 9.7 cm behind the
# slipper's middle (SlipperForward of the fit), size = the fitted 0.163 x 0.395 x 0.106 m x1.1 (x1.054 across the foot).
# Parts (Blender names): SlipperUpper (plush, with UVs for the fur shells), SlipperRim (cream plush ring round the opening), SlipperLining (the pit the ankle goes into),
# SlipperSole (black, with the grip dots). Origin = the ground under the slipper's middle; +Y = toe, +X = right, Z up.
# Run: blender -b --factory-startup --python scripts/slipper_model3.py -- <out_blend> <out_fbx> <render_prefix>
import bpy, bmesh, sys, math, os
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT_BLEND = argv[0] if len(argv) > 0 else ""
OUT_FBX = argv[1] if len(argv) > 1 else ""
RENDER = argv[2] if len(argv) > 2 else ""
HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REF = os.path.join(HERE, "reference")

L = 0.4345                 # length (m): 0.395 x 1.1
W = 0.1895                 # widest (m): 0.163 x 1.1 x 1.054
H = 0.1166                 # tall part (m): 0.106 x 1.1
SOLE = 0.012               # sole thickness
BALL = 0.58                # where it is widest, 0 = heel, 1 = toe
OPEN_Y, OPEN_RX, OPEN_RY = -0.097, 0.060, 0.080          # the opening: centred on the foot bone (9.7 cm behind the middle), half widths
N_ALONG, N_AROUND = 44, 32

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
log = []

def mat(name, rgb, rough=0.9):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]; b.inputs["Base Color"].default_value = (*rgb, 1); b.inputs["Roughness"].default_value = rough
    m.diffuse_color = (*rgb, 1)
    return m
# Blender's linear values of the existing Unity materials (SlipperPlush 0.36/0.62/0.70 etc. are the game-side sRGB values)
M = {"plush": mat("SlipperPlush", (0.108, 0.342, 0.448)), "rim": mat("SlipperRim", (0.72, 0.69, 0.62)), "lining": mat("SlipperLining", (0.26, 0.24, 0.21)),
     "sole": mat("SlipperSole", (0.01, 0.01, 0.012), 0.35), "skin": mat("reference_foot", (0.8, 0.5, 0.4))}

def f_width(t):                 # 0..1 across the length -> 0..1 of the half width (a lopsided superellipse: heel round, widest at the ball, round toe)
    u = (t - BALL) / (BALL if t < BALL else (1.0 - BALL))
    return max(0.0, 1.0 - abs(u) ** 2.35) ** (1.0 / 2.35)

def smooth(a, b, x):
    k = min(1.0, max(0.0, (x - a) / (b - a))); return k * k * (3 - 2 * k)

def top_z(t):                    # height of the top along the length: tall at the heel / opening, lower over the toes
    return SOLE + (H - SOLE) * (1.0 - 0.52 * smooth(0.40, 0.88, t))

def ring_pts(t, y):
    fw = f_width(t)
    hw = 0.5 * W * fw
    z0, z1 = SOLE * 0.3, top_z(t)
    cz, hh = (z0 + z1) / 2, (z1 - z0) / 2 * min(1.0, fw / 0.35) ** 0.7        # the ends close to a point (no slit)
    pts = []
    for k in range(N_AROUND):
        a = 2 * math.pi * k / N_AROUND
        c, s = math.cos(a), math.sin(a)
        e = 2.2 if s > 0 else 3.4                                 # plump on top, nearly flat underneath (sits on the sole)
        x = hw * math.copysign(abs(c) ** (2.0 / e), c)
        z = cz + hh * math.copysign(abs(s) ** (2.0 / e), s)
        pts.append(Vector((x, y, z)))
    return pts

# ---- the upper: a closed plush shell lofted along the length ----
bm = bmesh.new()
rings = []
for i in range(N_ALONG + 1):
    t = i / N_ALONG
    t_eff = 0.004 + 0.992 * t                                   # tiny radius at the ends instead of a degenerate point
    y = -L / 2 + L * t
    rings.append([bm.verts.new(p) for p in ring_pts(t_eff, y)])
for i in range(N_ALONG):
    for k in range(N_AROUND):
        a, b = rings[i][k], rings[i][(k + 1) % N_AROUND]; c, d = rings[i + 1][(k + 1) % N_AROUND], rings[i + 1][k]
        try: bm.faces.new((a, d, c, b))
        except ValueError: pass
bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])                        # close the heel and the toe
bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
me = bpy.data.meshes.new("SlipperUpper"); bm.to_mesh(me); bm.free()
upper = bpy.data.objects.new("SlipperUpper", me); scene.collection.objects.link(upper)
for p in me.polygons: p.use_smooth = True
upper.data.materials.append(M["plush"])
bpy.context.view_layer.update()

# ---- the opening: an oval pit cut into the top (boolean), its walls become the lining ----
def oval_cutter(name, y, rx, ry, z0, z1, segs=48):
    b = bmesh.new()
    top = [b.verts.new((rx * math.cos(2 * math.pi * k / segs), y + ry * math.sin(2 * math.pi * k / segs), z1)) for k in range(segs)]
    bot = [b.verts.new((rx * math.cos(2 * math.pi * k / segs), y + ry * math.sin(2 * math.pi * k / segs), z0)) for k in range(segs)]
    for k in range(segs):
        b.faces.new((bot[k], bot[(k + 1) % segs], top[(k + 1) % segs], top[k]))
    b.faces.new(top[::-1]); b.faces.new(bot)
    bmesh.ops.recalc_face_normals(b, faces=b.faces)
    m = bpy.data.meshes.new(name); b.to_mesh(m); b.free()
    o = bpy.data.objects.new(name, m); scene.collection.objects.link(o)
    return o
cut = oval_cutter("cutter", OPEN_Y, OPEN_RX, OPEN_RY, 0.066, 0.2)
cut.data.materials.append(M["lining"])
bpy.context.view_layer.update()
mod = upper.modifiers.new("hole", "BOOLEAN"); mod.operation = "DIFFERENCE"; mod.object = cut; mod.solver = "EXACT"
bpy.context.view_layer.objects.active = upper
bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.data.objects.remove(cut)
# faces made by the cutter carry the lining material (slot 1)
upper.data.materials.append(M["lining"])
bm = bmesh.new(); bm.from_mesh(upper.data)
inside = 0
for f in bm.faces:
    c = f.calc_center_median()
    if ((c.x / (OPEN_RX + 0.002)) ** 2 + ((c.y - OPEN_Y) / (OPEN_RY + 0.002)) ** 2) < 1.0 and c.z > 0.062 and f.normal.z < 0.6:
        f.material_index = 1; inside += 1
    elif ((c.x / (OPEN_RX + 0.002)) ** 2 + ((c.y - OPEN_Y) / (OPEN_RY + 0.002)) ** 2) < 1.0 and c.z > 0.062 and abs(c.z - 0.066) < 0.003:
        f.material_index = 1; inside += 1
bm.to_mesh(upper.data); bm.free()
for p in upper.data.polygons: p.use_smooth = True
log.append(f"upper: {len(upper.data.vertices)} verts, {len(upper.data.polygons)} faces, {inside} lining faces")

# ---- the rim: a cream plush ring round the opening ----
# 2026-11-04: the old torus (major 1.0, minor 0.0185) was SCALED to the opening by (0.066, 0.086, 0.85), which scaled the tube too: 2.4 mm wide, 3 cm tall = a
# slim ribbon in game (user: "the white margin is very slim"). Now the tube is built round with its real radius along the ellipse of the opening.
RIM_R = 0.018                  # tube radius (m): a 3.6 cm fluffy ring
RIM_PUSH = 0.012               # the ring centre sits this far outside the old opening, so the hole stays as wide as before and the ring ends at the upper's edge
RIM_RX, RIM_RY = OPEN_RX + 0.006 + RIM_PUSH, OPEN_RY + 0.006 + RIM_PUSH
RIM_SEG, RIM_RING = 64, 16
rim_mesh = bpy.data.meshes.new("SlipperRim")
rim_bm = bmesh.new()
rim_rows = []
for i in range(RIM_SEG):
    a = 2 * math.pi * i / RIM_SEG
    cx, cy = RIM_RX * math.cos(a), RIM_RY * math.sin(a)
    nx, ny = math.cos(a) / RIM_RX, math.sin(a) / RIM_RY                          # outward normal of the ellipse
    nl = math.hypot(nx, ny); nx, ny = nx / nl, ny / nl
    row = []
    for j in range(RIM_RING):
        b = 2 * math.pi * j / RIM_RING
        row.append(rim_bm.verts.new((cx + nx * RIM_R * math.cos(b), OPEN_Y + cy + ny * RIM_R * math.cos(b), top_z(0.30) - 0.004 + RIM_R * math.sin(b))))
    rim_rows.append(row)
for i in range(RIM_SEG):
    r0, r1 = rim_rows[i], rim_rows[(i + 1) % RIM_SEG]
    for j in range(RIM_RING):
        rim_bm.faces.new((r0[j], r1[j], r1[(j + 1) % RIM_RING], r0[(j + 1) % RIM_RING]))
bmesh.ops.recalc_face_normals(rim_bm, faces=rim_bm.faces[:])
rim_bm.to_mesh(rim_mesh); rim_bm.free()
rim = bpy.data.objects.new("SlipperRim", rim_mesh)
bpy.context.scene.collection.objects.link(rim)
bpy.context.view_layer.objects.active = rim
for p in rim.data.polygons: p.use_smooth = True
rim.data.materials.append(M["rim"])

# ---- the sole: black, a little wider than the upper, with grip dots under it ----
t_n = 60
outline = []
for i in range(t_n + 1):
    t = i / t_n
    y = -L / 2 + L * t
    hw = 0.5 * W * f_width(0.004 + 0.992 * t) + 0.004
    outline.append((hw, y))
poly = [Vector((hw, y, 0.0)) for hw, y in outline] + [Vector((-hw, y, 0.0)) for hw, y in reversed(outline[1:-1])]
b = bmesh.new()
vb = [b.verts.new(p) for p in poly]; vt = [b.verts.new(p + Vector((0, 0, SOLE))) for p in poly]
n = len(poly)
for k in range(n): b.faces.new((vb[k], vb[(k + 1) % n], vt[(k + 1) % n], vt[k]))
b.faces.new(vt); b.faces.new(vb[::-1])
bmesh.ops.recalc_face_normals(b, faces=b.faces)
bmesh.ops.bevel(b, geom=[e for e in b.edges if abs(e.verts[0].co.z - e.verts[1].co.z) < 1e-6 and e.verts[0].co.z > SOLE * 0.5], offset=0.003, segments=2, affect="EDGES")
dots = 0
y = -L / 2 + 0.03
while y < L / 2 - 0.025:
    row = int(round((y + L / 2) / 0.024))
    x = -0.5 * W + 0.02 + (0.012 if row % 2 else 0.0)
    while x < 0.5 * W - 0.02:
        t = (y + L / 2) / L
        if abs(x) < 0.5 * W * f_width(0.004 + 0.992 * t) - 0.012:
            c0 = b.verts.new((x, y, -0.003)); ring = []
            for k in range(8):
                a = 2 * math.pi * k / 8
                ring.append(b.verts.new((x + 0.0042 * math.cos(a), y + 0.0042 * math.sin(a), 0.0)))
            for k in range(8): b.faces.new((c0, ring[(k + 1) % 8], ring[k]))
            dots += 1
        x += 0.024
    y += 0.021
me = bpy.data.meshes.new("SlipperSole"); b.to_mesh(me); b.free()
sole = bpy.data.objects.new("SlipperSole", me); scene.collection.objects.link(sole)
sole.data.materials.append(M["sole"])
for p in sole.data.polygons: p.use_smooth = False
log.append(f"sole: {len(sole.data.vertices)} verts, {dots} grip dots")

# ---- UVs for the plush parts (the fur shells need them) ----
bpy.context.view_layer.update()
for o in (upper, rim):
    for ob in bpy.context.view_layer.objects: ob.select_set(ob == o)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")

parts = [upper, rim, sole]
for o in parts:
    d = o.dimensions
    log.append(f"{o.name}: {len(o.data.vertices)} verts, {len(o.data.polygons)} faces, uv {len(o.data.uv_layers)}, materials {[m.name for m in o.data.materials]}, dims {d.x:.3f} x {d.y:.3f} x {d.z:.3f} m")

# ---- a reference foot for the pictures (never exported): the player's left foot at its place relative to the slipper ----
def load_obj(name):
    verts, faces = [], []
    for line in open(os.path.join(REF, name + ".obj")):
        if line.startswith("v "):
            _, x, y_, z = line.split(); verts.append(Vector((float(x), float(z), float(y_))))
        elif line.startswith("f "):
            faces.append([int(i) - 1 for i in line.split()[1:]][::-1])
    mm = bpy.data.meshes.new(name); mm.from_pydata(verts, [], faces); mm.update()
    o = bpy.data.objects.new(name, mm); scene.collection.objects.link(o); o.data.materials.append(M["skin"])
    return o
body = load_obj("player_male")
J = {}
for line in open(os.path.join(REF, "player_joints.txt")):
    p = line.split()
    if len(p) == 4: J[p[0]] = Vector((float(p[1]), float(p[3]), float(p[2])))
foot, toe = J["LeftFoot"], J["LeftToeBase"]
fwd = Vector((toe.x - foot.x, toe.y - foot.y, 0)).normalized()
# the slipper's frame in the player's: the middle is 9.7 cm ahead of the foot bone along the foot, 0.9 cm outward, yawed 2.6 deg
yaw = math.radians(-2.636) + math.atan2(-fwd.x, fwd.y)
mid = Vector((foot.x, foot.y, 0)) + fwd * 0.09727 + Vector((-fwd.y * -0.00909, fwd.x * -0.00909, 0))
log.append(f"foot bone ({foot.x:.3f}, {foot.y:.3f}, {foot.z:.3f}), toe ({toe.x:.3f}, {toe.y:.3f}); slipper middle at ({mid.x:.3f}, {mid.y:.3f}), yaw {math.degrees(yaw):.1f} deg")
for o in parts:
    o.location = mid; o.rotation_euler = (0, 0, yaw)

# ---- the export is in the SLIPPER's own frame (origin under its middle, toe +Y); the pictures are in the player's frame ----
if RENDER:
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"; scene.display.shading.color_type = "MATERIAL"; scene.display.shading.show_cavity = True
    scene.render.resolution_x = 1000; scene.render.resolution_y = 700
    scene.world = bpy.data.worlds.new("w"); scene.world.color = (0.82, 0.86, 0.9)
    cam_d = bpy.data.cameras.new("cam"); cam_d.type = "ORTHO"; cam_d.ortho_scale = 0.62
    cam = bpy.data.objects.new("cam", cam_d); scene.collection.objects.link(cam); scene.camera = cam
    ctr = Vector((mid.x, mid.y, 0.07))
    def shot(name, direction, up_hint, scale, with_body):
        body.hide_render = not with_body
        cam_d.ortho_scale = scale
        d = Vector(direction).normalized()
        cam.location = ctr + d * 2.0
        cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = f"{RENDER}_{name}.png"; bpy.ops.render.render(write_still=True)
    shot("side", (-1, 0, 0.0), None, 0.62, False)
    shot("top", (0, 0.001, 1), None, 0.62, False)
    shot("three_quarter", (-0.8, 0.9, 0.55), None, 0.62, False)
    shot("side_with_foot", (-1, 0, 0.0), None, 0.62, True)
    shot("front_with_foot", (0, 1, 0.1), None, 0.62, True)
    log.append(f"rendered {RENDER}_*.png")

# bring the parts back to the slipper's own frame for the export
for o in parts:
    o.location = (0, 0, 0); o.rotation_euler = (0, 0, 0)
body.hide_set(True)
bpy.context.view_layer.update()
if OUT_BLEND:
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND); log.append(f"saved {OUT_BLEND}")
if OUT_FBX:
    for ob in bpy.context.view_layer.objects: ob.select_set(ob in parts)
    bpy.context.view_layer.objects.active = upper
    bpy.ops.export_scene.fbx(filepath=OUT_FBX, use_selection=True, object_types={'MESH'}, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False)
    log.append(f"exported {OUT_FBX}")
print("SLIPPER_LOG\n" + "\n".join(log))
