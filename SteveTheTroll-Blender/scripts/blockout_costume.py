# T-009a block-out of the troll MASCOT costume (Claude-Agent-2, 2026-10-28, D-069 .. D-072). Primitives only: proportions + colours for
# the user's sign-off of the one-sentence look (user "yes" 2026-10-28). Not the final model, not exported.
# Run: blender -b --factory-startup --python scripts/blockout_costume.py -- <out_prefix> <out_blend>
# Axes: Blender Z up, the player faces +Y, the player's RIGHT is +X. 1 unit = 1 m. Player ~1.8 m (head top), shoulders ~1.45 m.
import bpy, sys, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else "//costume_blockout"
OUT_BLEND = argv[1] if len(argv) > 1 else ""

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
log = []

def mat(name, rgb, rough=0.9):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    bsdf.inputs["Roughness"].default_value = rough
    return m

M = {
    "blue": mat("plush_blue", (0.30, 0.55, 0.68)),
    "blue_dark": mat("plush_blue_dark", (0.20, 0.40, 0.52)),
    "lining": mat("lining_grey", (0.62, 0.60, 0.58)),
    "sole": mat("sole_black", (0.02, 0.02, 0.02), 0.6),
    "straw": mat("straw_yellow", (0.80, 0.66, 0.28)),
    "eye": mat("eye_orange", (1.0, 0.35, 0.02), 0.3),
    "pupil": mat("pupil_black", (0.01, 0.01, 0.01), 0.3),
    "fang": mat("fang_white", (0.92, 0.90, 0.82), 0.4),
    "claw": mat("claw_dark", (0.05, 0.05, 0.06), 0.5),
    "bark": mat("log_bark", (0.08, 0.06, 0.05)),
    "cut": mat("log_cut", (0.70, 0.40, 0.15)),
}

def add(kind, name, loc, scale=(1, 1, 1), rot=(0, 0, 0), material="blue", **kw):
    ops = {"sphere": bpy.ops.mesh.primitive_uv_sphere_add, "cube": bpy.ops.mesh.primitive_cube_add,
           "cyl": bpy.ops.mesh.primitive_cylinder_add, "cone": bpy.ops.mesh.primitive_cone_add,
           "torus": bpy.ops.mesh.primitive_torus_add}
    ops[kind](location=loc, rotation=[math.radians(a) for a in rot], **kw)
    o = bpy.context.active_object
    o.name = name
    o.scale = scale
    o.data.materials.append(M[material])
    if kind in ("sphere", "torus", "cyl", "cone"):
        bpy.ops.object.shade_smooth()
    log.append(f"{name}: loc={tuple(round(v,3) for v in loc)} scale={scale}")
    return o

def limb(name, a, b, r, material="blue"):
    a, b = Vector(a), Vector(b)
    d = b - a
    rot = d.normalized().to_track_quat("Z", "Y").to_euler()
    return add("cyl", name, tuple((a + b) / 2), (r, r, d.length / 2), tuple(math.degrees(x) for x in rot), material)

# --- HEAD piece: big plush troll head, hides the face (centre 1.72 m, ~0.62 m wide)
add("sphere", "head", (0, 0.02, 1.72), (0.31, 0.29, 0.30))
add("sphere", "brow", (0, 0.24, 1.80), (0.22, 0.08, 0.06), material="blue_dark")
add("sphere", "nose", (0, 0.31, 1.68), (0.06, 0.07, 0.07), material="blue_dark")
add("sphere", "muzzle", (0, 0.22, 1.58), (0.18, 0.12, 0.09))
for sx, side in ((1, "R"), (-1, "L")):
    add("cone", f"ear_{side}", (sx * 0.33, 0.0, 1.80), (0.06, 0.03, 0.16), (0, sx * 70, 0))
    add("sphere", f"eye_{side}", (sx * 0.10, 0.27, 1.74), (0.055, 0.03, 0.05), material="eye")
    add("sphere", f"pupil_{side}", (sx * 0.10, 0.295, 1.74), (0.022, 0.012, 0.03), material="pupil")
    add("cone", f"fang_{side}", (sx * 0.07, 0.30, 1.52), (0.025, 0.02, 0.05), (180, 0, 0), material="fang")
for i, (x, z, tilt) in enumerate(((0, 2.06, 0), (0.08, 2.03, 25), (-0.08, 2.03, -25), (0.04, 2.0, 12), (-0.04, 2.0, -12))):
    add("cone", f"hair_{i}", (x, -0.02, z), (0.05, 0.05, 0.12), (0, tilt, 0), material="straw")

# --- CHEST piece: round fluffy body top + straw collar + plush arms with mitts
add("sphere", "body", (0, 0.0, 1.13), (0.40, 0.33, 0.42))
add("sphere", "belly", (0, 0.20, 1.08), (0.28, 0.16, 0.30), material="blue_dark")
add("torus", "collar", (0, 0.0, 1.43), (1.0, 0.9, 0.55), major_radius=0.27, minor_radius=0.07, material="straw")
for sx, side in ((1, "R"), (-1, "L")):
    limb(f"upper_arm_{side}", (sx * 0.40, 0.0, 1.35), (sx * 0.50, 0.05, 1.05), 0.11)
    limb(f"fore_arm_{side}", (sx * 0.50, 0.05, 1.05), (sx * 0.52, 0.20, 0.85), 0.10)
    add("sphere", f"mitt_{side}", (sx * 0.53, 0.24, 0.80), (0.12, 0.12, 0.11))
    for k in range(3):
        add("cone", f"claw_{side}{k}", (sx * (0.49 + 0.04 * k), 0.34, 0.76), (0.015, 0.015, 0.035), (90, 0, 0), material="claw")

# --- LEGS piece: fluffy trousers + straw loincloth (the Fluffy Slippers are the feet)
add("sphere", "hips", (0, 0.0, 0.78), (0.36, 0.29, 0.20))
for sx, side in ((1, "R"), (-1, "L")):
    limb(f"thigh_{side}", (sx * 0.17, 0.0, 0.75), (sx * 0.18, 0.02, 0.42), 0.15)
    limb(f"shin_{side}", (sx * 0.18, 0.02, 0.42), (sx * 0.18, 0.0, 0.14), 0.13)
    add("sphere", f"cuff_{side}", (sx * 0.18, 0.0, 0.14), (0.15, 0.15, 0.06), material="blue_dark")
    # slippers (feet) - stand-ins for the real Fluffy Slippers
    add("sphere", f"slipper_{side}", (sx * 0.18, 0.07, 0.055), (0.075, 0.15, 0.06), material="blue")
    add("cube", f"sole_{side}", (sx * 0.18, 0.07, 0.01), (0.07, 0.145, 0.01), material="sole")
add("cone", "loincloth_front", (0, 0.24, 0.62), (0.26, 0.10, 0.20), (180, 0, 0), material="straw")
add("cone", "loincloth_back", (0, -0.24, 0.62), (0.26, 0.10, 0.20), (180, 0, 0), material="straw")
add("torus", "belt", (0, 0.0, 0.84), (1.0, 0.82, 0.5), major_radius=0.35, minor_radius=0.04, material="straw")

# --- CLUB: dark bark tree trunk in the RIGHT mitt, resting on the right shoulder, ~1.5 m
grip = Vector((0.53, 0.24, 0.80))
up = Vector((0.15, -0.35, 1.0)).normalized()
L = 1.5
c = grip + up * (L / 2 - 0.25)
rot = up.to_track_quat("Z", "Y").to_euler()
deg = tuple(math.degrees(a) for a in rot)
bpy.ops.mesh.primitive_cone_add(vertices=24, radius1=0.07, radius2=0.12, depth=L, location=tuple(c), rotation=rot)
club = bpy.context.active_object; club.name = "club"; club.data.materials.append(M["bark"]); bpy.ops.object.shade_smooth()
add("cyl", "club_cut", tuple(c + up * (L / 2 + 0.002)), (0.118, 0.118, 0.004), deg, material="cut")
for i, (t, side) in enumerate(((0.35, 1), (0.6, -1), (0.8, 1))):
    p = grip + up * (t * L - 0.25)
    out = Vector((side, 0.3, 0.2)).normalized()
    limb(f"club_stub_{i}", tuple(p), tuple(p + out * 0.16), 0.025, "bark")
log.append(f"club: grip={tuple(round(v,3) for v in grip)} dir={tuple(round(v,3) for v in up)} len={L} m")

pts = [o.matrix_world @ Vector(cn) for o in scene.objects if o.type == "MESH" for cn in o.bound_box]
size = [max(p[i] for p in pts) - min(p[i] for p in pts) for i in range(3)]
log.append(f"BOUNDS m: x={size[0]:.2f} y={size[1]:.2f} z={size[2]:.2f} (player ~1.8 m; the mascot head makes it ~2.1 m)")

bpy.ops.object.light_add(type="SUN", rotation=(math.radians(50), 0, math.radians(140)))
bpy.context.active_object.data.energy = 3.0
bpy.ops.object.light_add(type="AREA", location=(-2.0, 2.5, 2.5))
bpy.context.active_object.data.energy = 300
world = bpy.data.worlds.new("w"); scene.world = world; world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.18, 0.12, 0.08, 1)
for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
    try:
        scene.render.engine = eng
        break
    except TypeError:
        continue
scene.render.resolution_x, scene.render.resolution_y = 800, 1000
bpy.ops.object.camera_add()
cam = bpy.context.active_object
cam.data.lens = 50
scene.camera = cam
target = Vector((0, 0, 1.05))
for view, loc in (("front34", (2.6, 4.2, 1.6)), ("side", (5.0, 0.0, 1.3)), ("back34", (-2.8, -4.0, 1.7))):
    cam.location = loc
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = f"{OUT}_{view}.png"
    bpy.ops.render.render(write_still=True)
    log.append(f"render {view}: {scene.render.filepath}")
if OUT_BLEND:
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
log.append(f"engine={scene.render.engine} blend={OUT_BLEND}")
print("BLOCKOUT_LOG\n" + "\n".join(log))
