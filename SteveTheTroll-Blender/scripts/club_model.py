# T-009b troll club model v1 (Claude-Agent-2, 2026-10-28; look from block-out v5, user: "the bat looks nice", D-073).
# A dark bark tree trunk (as the in-game troll's log), thicker at the hitting end, three broken branch stubs, a light cut face on top,
# a straw rope grip wrap (matches the costume's straw belt / collar / loincloth) where the hands hold it. Two-handed weapon.
# Origin = the grip point (the right hand), the trunk runs along +Z (Blender) from 0.25 m below the grip to the cut top.
# Run: blender -b --factory-startup --python scripts/club_model.py -- <out_prefix> <out_blend>
import bpy, bmesh, sys, math, random
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else "//club_v1"
OUT_BLEND = argv[1] if len(argv) > 1 else ""

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
log = []
rnd = random.Random(7)                                               # fixed seed: the same club on every run

def mat(name, rgb, rough=0.9):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1); b.inputs["Roughness"].default_value = rough
    return m
M = {"bark": mat("club_bark", (0.08, 0.06, 0.05)), "bark_light": mat("club_bark_light", (0.14, 0.10, 0.07)),
     "cut": mat("club_cut", (0.70, 0.40, 0.15)), "ring": mat("club_cut_ring", (0.45, 0.24, 0.08)),
     "straw": mat("straw_yellow", (0.80, 0.66, 0.28))}

L = 1.5                    # trunk length (block-out)
GRIP_FROM_END = 0.25       # the right hand holds it 0.25 m from the thin end
R_END, R_TOP = 0.07, 0.12  # thin (handle) end, thick (hitting) end
Z0, Z1 = -GRIP_FROM_END, L - GRIP_FROM_END
SIDES, RINGS = 20, 30

def radius(t):             # t 0..1 along the trunk: taper + a swelling near the head + a knot
    r = R_END + (R_TOP - R_END) * t ** 1.4
    r += 0.012 * math.exp(-((t - 0.82) / 0.08) ** 2)
    return r

# --- trunk: rings of slightly wobbly bark (fixed seed), rounded handle end, flat cut top
bm = bmesh.new()
rings = []
for i in range(RINGS + 1):
    t = i / RINGS; z = Z0 + t * L; r = radius(t)
    ring = []
    for k in range(SIDES):
        a = 2 * math.pi * k / SIDES
        rr = r * (1 + 0.06 * math.sin(3 * a + 1.7 * t * 6) * (0.5 + t)) * (1 + rnd.uniform(-0.03, 0.03))
        ring.append(bm.verts.new((rr * math.cos(a), rr * math.sin(a), z)))
    rings.append(ring)
for i in range(RINGS):
    for k in range(SIDES):
        a, b = rings[i][k], rings[i][(k + 1) % SIDES]; c, d = rings[i + 1][(k + 1) % SIDES], rings[i + 1][k]
        bm.faces.new((a, b, c, d))
# handle end: a rounded cap
bot = bm.verts.new((0, 0, Z0 - 0.03))
for k in range(SIDES): bm.faces.new((rings[0][(k + 1) % SIDES], rings[0][k], bot))
# top: inset cut face (bark rim -> ring -> light core)
top_ring = rings[-1]
inner1 = [bm.verts.new((v.co.x * 0.88, v.co.y * 0.88, Z1 + 0.004)) for v in top_ring]
inner2 = [bm.verts.new((v.co.x * 0.30, v.co.y * 0.30, Z1 + 0.006)) for v in top_ring]
for k in range(SIDES):
    n = (k + 1) % SIDES
    f = bm.faces.new((top_ring[k], top_ring[n], inner1[n], inner1[k])); f.material_index = 3      # dark growth ring
    f = bm.faces.new((inner1[k], inner1[n], inner2[n], inner2[k])); f.material_index = 2         # light cut wood
centre = bm.verts.new((0, 0, Z1 + 0.007))
for k in range(SIDES):
    f = bm.faces.new((inner2[k], inner2[(k + 1) % SIDES], centre)); f.material_index = 3          # heart
me = bpy.data.meshes.new("Club"); bm.normal_update(); bm.to_mesh(me); bm.free()
club = bpy.data.objects.new("Club", me); scene.collection.objects.link(club)
for k in ("bark", "bark_light", "cut", "ring"): me.materials.append(M[k])
STREAK = {k: (rnd.uniform(0, 0.4), rnd.uniform(0.5, 1.0)) for k in rnd.sample(range(SIDES), 6)}  # lighter bark streaks along the grain
for i, p in enumerate(me.polygons):
    p.use_smooth = p.material_index == 0
    if i < RINGS * SIDES:                                                                          # trunk side faces: ring-major order
        ring, k = divmod(i, SIDES); t = ring / RINGS
        if k in STREAK and STREAK[k][0] <= t <= STREAK[k][1]: p.material_index = 1
me.validate(); me.update()

def limb(name, a, b, r, material):
    a, b = Vector(a), Vector(b); d = b - a
    bpy.ops.mesh.primitive_cone_add(vertices=12, radius1=r, radius2=r * 0.55, depth=d.length, location=tuple((a + b) / 2),
                                    rotation=d.to_track_quat("Z", "Y").to_euler())
    o = bpy.context.active_object; o.name = name; o.data.materials.append(M[material]); bpy.ops.object.shade_smooth()
    return o

# --- three broken branch stubs (block-out positions), angled up like real branches
parts = []
for i, (t, ang) in enumerate(((0.46, 0.0), (0.64, 200.0), (0.80, 100.0))):   # first stub above the grip wrap
    z = Z0 + t * L; a = math.radians(ang); r = radius(t)
    out = Vector((math.cos(a), math.sin(a), 0.55)).normalized()
    base = Vector((math.cos(a) * r * 0.6, math.sin(a) * r * 0.6, z))
    parts.append(limb(f"Club_stub_{i}", base, base + out * (0.12 + 0.05 * (i == 2)), 0.028, "bark"))
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=0.016, depth=0.004, location=tuple(base + out * (0.12 + 0.05 * (i == 2)) + out * 0.001),
                                        rotation=out.to_track_quat("Z", "Y").to_euler())
    c = bpy.context.active_object; c.name = f"Club_stub_cut_{i}"; c.data.materials.append(M["cut"]); parts.append(c)

# --- straw rope grip wrap: a helix around the handle, both hands' room (from 0.05 below the grip to 0.40 above)
W0, W1, TURNS, RR = -0.07, 0.40, 15, 0.011
pts = []
for i in range(TURNS * 24 + 1):
    s = i / (TURNS * 24); z = W0 + s * (W1 - W0); a = 2 * math.pi * TURNS * s
    r = radius((z - Z0) / L) * 1.03 + RR * 0.7
    pts.append((r * math.cos(a), r * math.sin(a), z))
cu = bpy.data.curves.new("Club_wrap", "CURVE"); cu.dimensions = "3D"; cu.bevel_depth = RR; cu.bevel_resolution = 3
sp = cu.splines.new("POLY"); sp.points.add(len(pts) - 1)
for p, c in zip(sp.points, pts): p.co = (*c, 1)
wrap = bpy.data.objects.new("Club_wrap", cu); scene.collection.objects.link(wrap); cu.materials.append(M["straw"])
bpy.context.view_layer.objects.active = wrap
for o in bpy.context.selected_objects: o.select_set(False)
wrap.select_set(True); bpy.ops.object.convert(target="MESH"); bpy.ops.object.shade_smooth(); parts.append(bpy.context.active_object)

bpy.context.view_layer.update()
allp = [club] + parts
pts = [o.matrix_world @ v.co for o in allp for v in o.data.vertices]
sz = [max(p[i] for p in pts) - min(p[i] for p in pts) for i in range(3)]
log.append(f"club: {sum(len(o.data.vertices) for o in allp)} verts in {len(allp)} objects, size x {sz[0]:.2f} y {sz[1]:.2f} z {sz[2]:.2f} m, "
           f"grip at origin, {GRIP_FROM_END} m from the handle end, radius {R_END}..{R_TOP} m")

# --- renders: full length lying sideways, 3/4, close-up of the head and of the grip
bpy.ops.object.light_add(type="SUN", rotation=(math.radians(50), 0, math.radians(140))); bpy.context.active_object.data.energy = 3.0
bpy.ops.object.light_add(type="AREA", location=(-2.0, 2.5, 2.5)); bpy.context.active_object.data.energy = 300
world = bpy.data.worlds.new("w"); scene.world = world; world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.18, 0.12, 0.08, 1)
for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
    try: scene.render.engine = eng; break
    except TypeError: continue
scene.render.resolution_x, scene.render.resolution_y = 800, 1000
bpy.ops.object.camera_add(); cam = bpy.context.active_object; cam.data.lens = 50; scene.camera = cam
mid = Vector((0, 0, (Z0 + Z1) / 2))
for view, loc, tgt, lens in (("full", (1.3, 1.9, 0.75), mid, 40), ("head", (0.6, 0.9, 1.55), Vector((0, 0, 1.05)), 50),
                             ("grip", (0.5, 0.8, 0.35), Vector((0, 0, 0.08)), 50)):
    cam.location = loc; cam.data.lens = lens; cam.rotation_euler = (tgt - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = f"{OUT}_{view}.png"; bpy.ops.render.render(write_still=True); log.append(f"render {view}: {scene.render.filepath}")
if OUT_BLEND: bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
print("CLUB_LOG\n" + "\n".join(log))
