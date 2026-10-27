# T-007 item icon (Claude-Agent-1, 2026-10-27): 256 x 256 transparent render of the slipper (own model only - no game troll in the icon).
# Run: blender -b source/slipper_v1.blend --python scripts/slipper_icon.py -- <out_png>
import bpy, sys, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else "//../exports/SlipperIcon.png"
scene = bpy.context.scene
scene.render.resolution_x = scene.render.resolution_y = 256
scene.render.film_transparent = True
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
cam = scene.camera
pos, tgt = Vector((0.42, 0.42, 0.34)), Vector((0, 0.0, 0.04))
cam.location = pos
cam.rotation_euler = (tgt - pos).to_track_quat('-Z', 'Y').to_euler()
cam.data.lens = 50
scene.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print("ICON " + OUT)
