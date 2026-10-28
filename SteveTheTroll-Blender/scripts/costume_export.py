# T-009c costume + club export (Claude-Agent-2, 2026-10-28). Opens source/costume_v2.blend (costume_model.py) and source/club_v1.blend
# (club_model.py), joins the parts per game piece and writes ONE FBX each:
#   exports/Costume.fbx: CostumeHelmet (mascot head), CostumeChest (top + mitts + claws + collar), CostumeLegs (trousers + cuffs + belt +
#   loincloth), CostumeFeet (plush feet, hidden in game while the slippers are worn). Coordinates = the player's root space in the rest pose
#   (Blender origin = the player's feet), so Unity reads them as metres around the player (+Z forward, +Y up).
#   exports/Club.fbx: Club (trunk + stubs + rope), origin = the grip.
# Material slots keep their Blender names (plush_blue, straw_yellow, ...); Unity makes one material per name (CostumeBuild.cs).
# Axes as the slipper (slipper_export.py): Blender Z up / +Y forward -> FBX forward -Z, up Y, apply transform.
# Run: blender -b --factory-startup --python scripts/costume_export.py -- <costume_blend> <club_blend> <out_dir>
import bpy, sys, os

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
COSTUME, CLUB, OUT = argv[0], argv[1], argv[2]
log = []
DECIMATE = float(os.environ.get("STT_DECIMATE", "0.35"))

GROUPS = {
    "CostumeHelmet": ("head", "head_cap", "brow", "nose", "muzzle", "ear_", "eye_", "pupil_", "fang_", "hair_"),
    "CostumeChest": ("Costume_Top_Mesh", "Costume_Top_claw_", "collar"),
    "CostumeLegs": ("Costume_Legs_Mesh", "belt", "loincloth_", "cuff_"),
    "CostumeFeet": ("Costume_Feet_",),
}

def select_only(objs, active):
    for o in bpy.context.view_layer.objects: o.select_set(False)
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = active

def join(name, objs):
    for o in objs: o.hide_set(False); o.hide_render = False
    select_only(objs, objs[0])
    if len(objs) > 1: bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    o.name = name; o.data.name = name
    if DECIMATE < 1.0:                                                  # metaball / sphere meshes are dense: game budget ~ armour pieces
        m = o.modifiers.new("decimate", "DECIMATE"); m.ratio = DECIMATE; bpy.ops.object.modifier_apply(modifier=m.name)
    for p in o.data.polygons: p.use_smooth = True
    return o

def export(objs, path):
    select_only(objs, objs[0])
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False)
    log.append(f"exported {path}")

def describe(o):
    pts = [o.matrix_world @ v.co for v in o.data.vertices]
    lo = [min(p[i] for p in pts) for i in range(3)]; hi = [max(p[i] for p in pts) for i in range(3)]
    log.append(f"{o.name}: {len(o.data.vertices)} verts, {len(o.data.polygons)} faces, materials {[m.name for m in o.data.materials]}, "
               f"x {lo[0]:.2f}..{hi[0]:.2f} y(fwd) {lo[1]:.2f}..{hi[1]:.2f} z(up) {lo[2]:.2f}..{hi[2]:.2f} m")

# --- costume
bpy.ops.wm.open_mainfile(filepath=COSTUME)
meshes = [o for o in bpy.data.objects if o.type == "MESH"]
used, groups, pieces = set(), {}, []
for name, prefixes in GROUPS.items():                                   # pick every group first: joining removes objects
    objs = [o for o in meshes if o not in used and any(o.name == p or (p.endswith("_") and o.name.startswith(p)) for p in prefixes)]
    if not objs: raise SystemExit(f"costume_export: no objects for {name}")
    used.update(objs); groups[name] = objs
left = [o.name for o in meshes if o not in used and not o.name.startswith("slipper_")]
log.append(f"not exported (slippers are their own item): {len([o for o in meshes if o.name.startswith('slipper_')])} slipper object(s); other leftovers: {left}")
for name, objs in groups.items():
    pieces.append(join(name, objs)); describe(pieces[-1])
export(pieces, os.path.join(OUT, "Costume.fbx"))

# --- club
bpy.ops.wm.open_mainfile(filepath=CLUB)
meshes = [o for o in bpy.data.objects if o.type == "MESH"]
club = join("Club", meshes); describe(club)
export([club], os.path.join(OUT, "Club.fbx"))
print("EXPORT_LOG\n" + "\n".join(log))
