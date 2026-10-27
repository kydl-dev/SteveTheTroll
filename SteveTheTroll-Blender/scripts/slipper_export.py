# T-011 slipper export (Claude-Agent-1, 2026-10-27, D-025). Opens source/slipper_v1.blend (made by slipper_model.py), bakes the
# modifiers, UV-unwraps upper + rim (the ShellFur strands need UVs), joins the grip dots into the sole and writes ONE FBX with three
# meshes: SlipperUpper, SlipperRim, SlipperSole (+ SlipperLining as the upper's 2nd material slot stays inside the upper mesh).
# Axes: Blender Z up / +Y toe -> FBX forward -Z, up Y, apply transform (Unity: toe = +Z, up = +Y, 1 unit = 1 m). Origin = ground under the
# middle of the slipper (Blender 0,0,0).
# Run: blender -b source/slipper_v1.blend --python scripts/slipper_export.py -- <out_fbx>
import bpy, sys

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else "//../exports/Slipper.fbx"
log = []

def apply_all(obj):
    bpy.context.view_layer.objects.active = obj
    for o in bpy.context.selected_objects: o.select_set(False)
    obj.select_set(True)
    for m in list(obj.modifiers):
        if m.type == 'PARTICLE_SYSTEM':
            obj.modifiers.remove(m)
            continue
        bpy.ops.object.modifier_apply(modifier=m.name)

def unwrap(obj):
    bpy.context.view_layer.objects.active = obj
    for o in bpy.context.selected_objects: o.select_set(False)
    obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.02)
    bpy.ops.object.mode_set(mode='OBJECT')

upper = bpy.data.objects["slipper_body"]
rim = bpy.data.objects["slipper_rim"]
sole = bpy.data.objects["slipper_sole"]
for o in (upper, rim, sole): apply_all(o)
# grip dots are children of the sole: join them in
dots = [o for o in bpy.data.objects if o.parent == sole]
for o in bpy.context.selected_objects: o.select_set(False)
for d in dots: d.select_set(True)
sole.select_set(True)
bpy.context.view_layer.objects.active = sole
if dots: bpy.ops.object.join()
unwrap(upper)
unwrap(rim)
upper.name, rim.name, sole.name = "SlipperUpper", "SlipperRim", "SlipperSole"
for o in (upper, rim, sole):
    log.append(f"{o.name}: {len(o.data.vertices)} verts, {len(o.data.polygons)} faces, uv {len(o.data.uv_layers)}, materials {[m.name for m in o.data.materials]}, dims {tuple(round(d, 3) for d in o.dimensions)}")
for o in bpy.context.selected_objects: o.select_set(False)
for o in (upper, rim, sole): o.select_set(True)
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, object_types={'MESH'}, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                         axis_forward='-Z', axis_up='Y', bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False)
log.append(f"exported {OUT}")
print("EXPORT_LOG\n" + "\n".join(log))
