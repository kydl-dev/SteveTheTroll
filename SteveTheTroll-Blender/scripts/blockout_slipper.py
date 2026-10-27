# T-004 block-out of ONE troll slipper (Claude-Agent-1, 2026-10-27). Primitives only: proportions + colours for the
# user's sign-off (D-010 look, D-012 size). Not the final model, not exported.
# Run: blender -b --factory-startup --python scripts/blockout_slipper.py -- <out_png> <out_blend>
# Axes: Blender Z up, toe points +Y. 1 unit = 1 m. Foot length assumed 0.27 m (checked later against the player mesh).
import bpy, sys, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT_PNG = argv[0] if argv else "//blockout.png"
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
    "blue": mat("plush_blue", (0.18, 0.42, 0.50)),
    "lining": mat("lining_grey", (0.62, 0.60, 0.58)),
    "sole": mat("sole_black", (0.02, 0.02, 0.02), 0.6),
    "straw": mat("straw_yellow", (0.75, 0.62, 0.25)),
    "eye": mat("eye_orange", (1.0, 0.35, 0.02), 0.3),
    "bark": mat("log_bark", (0.06, 0.05, 0.05)),
    "cut": mat("log_cut", (0.70, 0.40, 0.15)),
    "claw": mat("claw_dark", (0.05, 0.05, 0.06), 0.5),
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
    if kind in ("sphere", "torus", "cyl"):
        bpy.ops.object.shade_smooth()
    log.append(f"{name}: loc={loc} scale={scale}")
    return o

# --- slipper (foot length 0.27 m along +Y, centred at Y=0)
add("cube", "sole", (0, 0, 0.012), (0.062, 0.148, 0.012), material="sole")
bpy.ops.object.modifier_add(type="BEVEL"); bpy.context.object.modifiers[-1].width = 0.03; bpy.context.object.modifiers[-1].segments = 6
add("sphere", "shoe", (0, 0, 0.055), (0.068, 0.15, 0.058))
add("torus", "rim", (0, -0.05, 0.105), (1.0, 1.35, 1.0), major_radius=0.05, minor_radius=0.018, material="lining")
add("cyl", "opening", (0, -0.05, 0.1), (0.048, 0.066, 0.01), material="lining")

# --- troll sits on the toe half, faces +Y. Its RIGHT side is +X (forward x up = +X; v1 had this wrong). D-010: log in the RIGHT hand
add("sphere", "troll_body", (0, 0.07, 0.14), (0.07, 0.06, 0.075))
add("sphere", "troll_head", (0, 0.105, 0.235), (0.045, 0.042, 0.045))
add("sphere", "troll_brow", (0, 0.135, 0.248), (0.036, 0.015, 0.012))
add("sphere", "troll_nose", (0, 0.15, 0.228), (0.012, 0.014, 0.014))
for sx, side in ((1, "L"), (-1, "R")):
    add("cone", f"ear_{side}", (sx * 0.05, 0.1, 0.245), (0.012, 0.006, 0.03), (0, sx * 70, 0))
    add("sphere", f"eye_{side}", (sx * 0.016, 0.142, 0.24), (0.007, 0.004, 0.007), material="eye")
    add("cone", f"fang_{side}", (sx * 0.012, 0.142, 0.205), (0.003, 0.003, 0.008), (180, 0, 0), material="claw")
    add("sphere", f"foot_{side}", (sx * 0.035, 0.145, 0.085), (0.025, 0.035, 0.02))
add("torus", "collar", (0, 0.095, 0.2), (1.0, 1.0, 0.6), major_radius=0.045, minor_radius=0.014, material="straw")
add("cone", "loincloth", (0, 0.11, 0.1), (0.06, 0.045, 0.04), (180, 0, 0), material="straw")
# left arm rests on the shoe, right arm holds the log pointing forward / outward like the picture
add("cyl", "arm_L", (-0.07, 0.11, 0.11), (0.018, 0.018, 0.05), (60, 0, 20))
add("sphere", "hand_L", (-0.075, 0.15, 0.08), (0.022, 0.025, 0.016))
add("cyl", "arm_R", (0.075, 0.1, 0.13), (0.018, 0.018, 0.05), (70, 0, -30))
add("sphere", "hand_R", (0.09, 0.145, 0.105), (0.023, 0.023, 0.023))
# log: from the right hand forward-outward and down toward the ground, cut end at the far tip
hand = Vector((0.09, 0.145, 0.105))
tip_dir = Vector((0.35, 0.85, -0.38)).normalized()
LOG_LEN = 0.13
log_c = hand + tip_dir * (LOG_LEN / 2 - 0.015)
rot = tip_dir.to_track_quat("Z", "Y").to_euler()
deg = tuple(math.degrees(a) for a in rot)
add("cyl", "log", tuple(log_c), (0.022, 0.022, LOG_LEN / 2), deg, material="bark")
add("cyl", "log_cut", tuple(log_c + tip_dir * (LOG_LEN / 2 + 0.001)), (0.021, 0.021, 0.003), deg, material="cut")
log.append(f"log: hand={tuple(round(v,3) for v in hand)} dir={tuple(round(v,3) for v in tip_dir)} len={LOG_LEN} m")

# --- size check against the foot (D-012: troll about slipper-long, up to mid-shin ~0.25-0.3 m)
pts = [o.matrix_world @ Vector(c) for o in scene.objects if o.type == "MESH" for c in o.bound_box]
size = [max(p[i] for p in pts) - min(p[i] for p in pts) for i in range(3)]
log.append(f"BOUNDS m: x={size[0]:.3f} y={size[1]:.3f} z={size[2]:.3f} (expected y~0.30-0.36 with the log, z~0.27)")

# --- camera 3/4 front-left like the user's picture, plus lights
bpy.ops.object.camera_add(location=(0.75, 0.95, 0.45))
cam = bpy.context.active_object
target = Vector((-0.02, 0.06, 0.12))
cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
cam.data.lens = 55
scene.camera = cam
bpy.ops.object.light_add(type="SUN", rotation=(math.radians(50), 0, math.radians(140)))
bpy.context.active_object.data.energy = 3.0
bpy.ops.object.light_add(type="AREA", location=(-0.5, 0.6, 0.6))
bpy.context.active_object.data.energy = 40
world = bpy.data.worlds.new("w"); scene.world = world; world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.18, 0.12, 0.08, 1)

for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
    try:
        scene.render.engine = eng
        break
    except TypeError:
        continue
scene.render.resolution_x, scene.render.resolution_y = 900, 700
scene.render.filepath = OUT_PNG
bpy.ops.render.render(write_still=True)
if OUT_BLEND:
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
log.append(f"engine={scene.render.engine} png={OUT_PNG} blend={OUT_BLEND}")
print("BLOCKOUT_LOG\n" + "\n".join(log))
