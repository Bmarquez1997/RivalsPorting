import bpy
import traceback
from .server import Server
from .logger import Log
from .processing.importer import Importer
from .processing.tasty import *
from mathutils import Matrix, Vector, Euler, Quaternion

bl_info = {
    "name": "Rivals Porting",
    "description": "Import Server for Rivals Porting",
    "author": "Half, DeveloperChipmunk",
    "blender": (4, 2, 0),
    "version": (0, 4, 3),
    "category": "Import-Export",
}


def display_popup(text="Message", title="Information", icon='INFO'):
    def draw(self, context):
        self.layout.label(text=text)

    bpy.context.window_manager.popup_menu(draw, title=title, icon=icon)


def server_data_handler():
    if data := server.get_data():
        try:
            Importer.Import(data)
        except Exception as e:
            error_message = str(e)

            Log.error(f"An unhandled error occurred:")
            traceback.print_exc()

            display_popup(error_message, "An unhandled error occurred", "ERROR")

    return 0.01

class RivalsPortingAdditiveFix(bpy.types.Operator):
    bl_idname = "rivals_porting.additive_fix"
    bl_label = "Broken Animation Fix"

    def execute(self, context):
        active = bpy.context.active_object
        if active is None:
            return
        if active.type != "ARMATURE":
            return

        bpy.ops.object.mode_set(mode='POSE')
        bpy.ops.pose.select_all(action='DESELECT')
        pose_bones = active.pose.bones
        bones = active.data.bones
        dispose_paths = []
        for bone in bones:
            if bone.name.casefold() in ["root", "pelvis"]:
                continue

            dispose_paths.append('pose.bones["{}"].location'.format(bone.name))
            pose_bones[bone.name].location = Vector()

        if active.animation_data.action:
            dispose_curves = [fcurve for fcurve in active.animation_data.action.fcurves if
                              fcurve.data_path in dispose_paths]
            for fcurve in dispose_curves:
                active.animation_data.action.fcurves.remove(fcurve)
        elif active.animation_data.nla_tracks:
            for track in active.animation_data.nla_tracks:
                for strip in track.strips:
                    dispose_curves = [fcurve for fcurve in strip.action.fcurves if fcurve.data_path in dispose_paths]
                    for fcurve in dispose_curves:
                        strip.action.fcurves.remove(fcurve)

        bpy.ops.object.mode_set(mode='OBJECT')
        return {'FINISHED'}


class RivalsPortingTastyRig(bpy.types.Operator):
    bl_idname = "rivals_porting.tasty_rig"
    bl_label = "Apply Tasty Rig"

    simplify_face_bones: bpy.props.BoolProperty(name="Simplify Face Bones", default=True)
    use_dynamic_rig_shapes: bpy.props.BoolProperty(name="Use Dynamic Rig Shapes", default=True)

    def execute(self, context):
        active = bpy.context.active_object
        if active is None:
            return
        if active.type != "ARMATURE":
            return

        create_tasty_rig(self, active, TastyRigOptions(scale=0.01, use_dynamic_bone_shape=self.use_dynamic_rig_shapes, simplify_face_bones=self.simplify_face_bones))

        return {'FINISHED'}

    def invoke(self, context, event):
        return context.window_manager.invoke_props_dialog(self)


class RivalsPortingGithub(bpy.types.Operator):
    bl_idname = "rivals_porting.github"
    bl_label = "Github"

    def execute(self, context):
        os.system("start https://github.com/Bmarquez1997/RivalsPorting")
        return {'FINISHED'}


class RivalsPortingDiscord(bpy.types.Operator):
    bl_idname = "rivals_porting.discord"
    bl_label = "Discord"

    def execute(self, context):
        os.system("start https://discord.gg/Yae66Aqsr5")
        return {'FINISHED'}


class RivalsPortingPanel(bpy.types.Panel):
    bl_region_type = 'UI'
    bl_space_type = 'VIEW_3D'
    bl_category = "Item"
    bl_idname = 'VIEW3D_PT_rivals_porting_panel'
    bl_label = "Rivals Porting Utils"
    bl_description = "Rivals Porting Blender Utilities"
    bl_options = {'DEFAULT_CLOSED'}

    @classmethod
    def poll(cls, context):
        try:
            active = context.active_object
            if active is None:
                return False
            if active.type == "ARMATURE":
                return True
        except (AttributeError, KeyError, TypeError):
            return False

    def draw(self, context):
        layout = self.layout

        box = layout.box()
        box.label(text="Rigging", icon="OUTLINER_OB_ARMATURE")
        box.row().operator("rivals_porting.tasty_rig", icon='ARMATURE_DATA')
        box.row().operator("rivals_porting.additive_fix", icon='ANIM')

        box = layout.box()
        box.label(text="Links", icon="LINK_BLEND")
        row = box.row()
        row.operator("rivals_porting.github", icon='FILE_SCRIPT')
        row.operator("rivals_porting.discord", icon='MONKEY')

def register():
    global server
    server = Server.create()
    server.start()

    bpy.app.timers.register(server_data_handler, persistent=True)
    
    bpy.utils.register_class(RivalsPortingAdditiveFix)
    bpy.utils.register_class(RivalsPortingTastyRig)
    bpy.utils.register_class(RivalsPortingGithub)
    bpy.utils.register_class(RivalsPortingDiscord)
    bpy.utils.register_class(RivalsPortingPanel)


def unregister():
    server.shutdown()
