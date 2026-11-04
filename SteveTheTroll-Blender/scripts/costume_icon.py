# T-009d / T-009e icons (Claude-Agent-2, 2026-10-28): 256 x 256 transparent renders of our own models (no game asset):
#   CostumeHelmetIcon (head), CostumeChestIcon (suit top), CostumeLegsIcon (trousers + plush feet), TrollClubIcon (club),
#   SteveTheTrollSetIcon (status effect "Steve The Troll": the whole mascot, head-on, with the club).
# Run: blender -b --factory-startup --python scripts/costume_icon.py -- <costume_blend> <club_blend> <out_dir>
import bpy, sys, os, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
COSTUME, CLUB, OUT = argv[0], argv[1], argv[2]
log = []

GROUPS = {
    "head": ("head", "head_cap", "brow", "nose", "muzzle", "ear_", "eye_", "pupil_", "fang_", "hair_"),
    "chest": ("Costume_Top_Mesh", "Costume_Top_claw_", "collar"),
    "legs": ("Costume_Legs_Mesh", "belt", "loincloth_", "cuff_", "Costume_Feet_"),
}
def member(o, prefixes): return any(o.name == p or (p.endswith("_") and o.name.startswith(p)) for p in prefixes)

def setup(scene):
    scene.render.resolution_x = scene.render.resolution_y = 256
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = 'PNG'; scene.render.image_settings.color_mode = 'RGBA'
    for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try: scene.render.engine = eng; break
        except TypeError: continue
    if scene.camera is None:
        bpy.ops.object.camera_add(); scene.camera = bpy.context.active_object
    if not any(o.type == "LIGHT" for o in scene.objects):
        bpy.ops.object.light_add(type="SUN", rotation=(math.radians(50), 0, math.radians(140))); bpy.context.active_object.data.energy = 3.0
        bpy.ops.object.light_add(type="AREA", location=(-2.0, 2.5, 2.5)); bpy.context.active_object.data.energy = 300

def frame(scene, objs, view_dir, name, lens=50, margin=1.15):
    """Camera along view_dir (towards the objects) so the objects' bounds fill the square image."""
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    lo = Vector([min(p[i] for p in pts) for i in range(3)]); hi = Vector([max(p[i] for p in pts) for i in range(3)])
    c = (lo + hi) / 2; r = (hi - lo).length / 2 * margin
    cam = scene.camera; cam.data.lens = lens
    fov = 2 * math.atan(cam.data.sensor_width / 2 / lens)
    d = r / math.tan(fov / 2)
    cam.location = c - Vector(view_dir).normalized() * d
    cam.rotation_euler = (c - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.clip_end = d * 4
    scene.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    log.append(f"{name}: {len(objs)} object(s), size {tuple(round(x, 2) for x in (hi - lo))} m -> {scene.render.filepath}")

def show_only(scene, objs):
    for o in scene.objects:
        if o.type == "MESH": o.hide_render = o not in objs

# --- costume icons (the player faces +Y: a 3/4 front view looks along -Y, slightly from the right and above)
bpy.ops.wm.open_mainfile(filepath=COSTUME)
scene = bpy.context.scene; setup(scene)
# 2026-11-04 (user): the legs icon shows the CURRENT pants (pants_model.py's CostumeLegs, no shoes), not the old suit legs
PANTS = argv[3] if len(argv) > 3 else os.path.join(os.path.dirname(COSTUME), "pants_v3.blend")
if os.path.exists(PANTS):
    with bpy.data.libraries.load(PANTS, link=False) as (src, dst): dst.objects = ["CostumeLegs"]
    for o in dst.objects: scene.collection.objects.link(o)
    GROUPS["legs"] = ("CostumeLegs",)
    log.append(f"legs icon from {PANTS}")
meshes = [o for o in scene.objects if o.type == "MESH"]
sel = {k: [o for o in meshes if member(o, v)] for k, v in GROUPS.items()}
feet = [o for o in meshes if o.name.startswith("Costume_Feet_")]       # only the whole-mascot icon keeps the plush feet
for k, objs in sel.items(): log.append(f"{k}: {len(objs)} object(s)")
three_q = (-0.45, -1.0, -0.25)
show_only(scene, sel["head"]); frame(scene, sel["head"], three_q, "CostumeHelmetIcon")
show_only(scene, sel["chest"]); frame(scene, sel["chest"], three_q, "CostumeChestIcon")
show_only(scene, sel["legs"]); frame(scene, sel["legs"], three_q, "CostumeLegsIcon")
whole = sel["head"] + sel["chest"] + sel["legs"] + feet
show_only(scene, whole); frame(scene, whole, (-0.25, -1.0, -0.12), "SteveTheTrollSetIcon", margin=1.0)

# --- club icon: lying diagonally, seen from the side
bpy.ops.wm.open_mainfile(filepath=CLUB)
scene = bpy.context.scene; setup(scene)
objs = [o for o in scene.objects if o.type == "MESH"]
pivot = bpy.data.objects.new("pivot", None); scene.collection.objects.link(pivot)
for o in objs: o.parent = pivot
pivot.rotation_euler = (0, math.radians(-45), 0)
bpy.context.view_layer.update()
frame(scene, objs, (0.15, 1.0, -0.2), "TrollClubIcon", margin=1.02)
print("ICON_LOG\n" + "\n".join(log))
