import bpy
from bpy.utils import register_class, unregister_class
from bpy_extras import anim_utils
from mathutils import Vector

from ..processing.context import ImportContext
from ..processing.utils import get_selected_armature
from ..utils import ensure_blend_data

GITHUB_URL = "https://github.com/Bmarquez1997/RivalsPorting"
DISCORD_URL = "https://discord.gg/Yae66Aqsr5"


def remove_fcurves(action, dispose_paths):
    if action is None:
        return

    if bpy.app.version < (5, 0, 0):
        fcurves = action.fcurves
    elif len(action.slots) > 0:
        fcurves = anim_utils.action_ensure_channelbag_for_slot(action, action.slots[0]).fcurves
    else:
        return

    for fcurve in [fcurve for fcurve in fcurves if fcurve.data_path in dispose_paths]:
        fcurves.remove(fcurve)


class RIVALS_OT_AdditiveFix(bpy.types.Operator):
    bl_idname = "rivals_porting.additive_fix"
    bl_label = "Broken Animation Fix"
    bl_description = "Removes bone location keyframes that break additive animations"
    bl_options = {'REGISTER', 'UNDO'}

    @classmethod
    def poll(cls, context):
        active = context.active_object
        return active is not None and active.type == "ARMATURE"

    def execute(self, context):
        active = context.active_object
        if active is None or active.type != "ARMATURE":
            self.report({'ERROR'}, "An armature must be selected to fix an animation.")
            return {'CANCELLED'}

        anim_data = active.animation_data
        if anim_data is None:
            self.report({'WARNING'}, "The selected armature has no animation data.")
            return {'CANCELLED'}

        previous_mode = active.mode
        bpy.ops.object.mode_set(mode='POSE')
        bpy.ops.pose.select_all(action='DESELECT')

        pose_bones = active.pose.bones
        dispose_paths = []
        for bone in active.data.bones:
            if bone.name.casefold() in ["root", "pelvis"]:
                continue

            dispose_paths.append('pose.bones["{}"].location'.format(bone.name))
            pose_bones[bone.name].location = Vector()

        if anim_data.action:
            remove_fcurves(anim_data.action, dispose_paths)
        elif anim_data.nla_tracks:
            for track in anim_data.nla_tracks:
                for strip in track.strips:
                    remove_fcurves(strip.action, dispose_paths)

        bpy.ops.object.mode_set(mode=previous_mode)
        return {'FINISHED'}


class RIVALS_OT_TastyRig(bpy.types.Operator):
    bl_idname = "rivals_porting.tasty_rig"
    bl_label = "Apply Tasty Rig"
    bl_description = "Applies the Tasty rig onto the selected armature"
    bl_options = {'REGISTER', 'UNDO'}

    use_dynamic_rig_shapes: bpy.props.BoolProperty(name="Use Dynamic Rig Shapes", default=True)
    bone_length: bpy.props.FloatProperty(name="Bone Length", default=4.0, min=1.0, max=5.0)

    @classmethod
    def poll(cls, context):
        active = context.active_object
        return active is not None and active.type in {"ARMATURE", "MESH"}

    def execute(self, context):
        armature = get_selected_armature()
        if armature is None:
            self.report({'ERROR'}, "An armature must be selected to apply the Tasty rig.")
            return {'CANCELLED'}

        if armature.data.get("is_tasty"):
            self.report({'WARNING'}, "This armature already has the Tasty rig applied onto it.")
            return {'CANCELLED'}

        if context.mode != "OBJECT":
            bpy.ops.object.mode_set(mode='OBJECT')

        context.view_layer.objects.active = armature
        armature.select_set(True)

        ensure_blend_data()

        import_context = ImportContext({
            "Settings": {
                "UseDynamicBoneShape": self.use_dynamic_rig_shapes,
                "BoneLength": self.bone_length,
            }
        })
        import_context.scale = 0.01
        import_context.import_tasty_rig_standalone(None)

        return {'FINISHED'}

    def invoke(self, context, event):
        return context.window_manager.invoke_props_dialog(self)


class RIVALS_OT_Github(bpy.types.Operator):
    bl_idname = "rivals_porting.github"
    bl_label = "Github"
    bl_description = "Open the Rivals Porting repository"

    def execute(self, context):
        bpy.ops.wm.url_open(url=GITHUB_URL)
        return {'FINISHED'}


class RIVALS_OT_Discord(bpy.types.Operator):
    bl_idname = "rivals_porting.discord"
    bl_label = "Discord"
    bl_description = "Open the Rivals Porting Discord server"

    def execute(self, context):
        bpy.ops.wm.url_open(url=DISCORD_URL)
        return {'FINISHED'}


class RIVALS_PT_Utils(bpy.types.Panel):
    bl_region_type = 'UI'
    bl_space_type = 'VIEW_3D'
    bl_category = "Item"
    bl_idname = 'VIEW3D_PT_rivals_porting_panel'
    bl_label = "Rivals Porting Utils"
    bl_description = "Rivals Porting Blender Utilities"
    bl_options = {'DEFAULT_CLOSED'}

    @classmethod
    def poll(cls, context):
        active = context.active_object
        return active is not None and active.type == "ARMATURE"

    def draw(self, context):
        layout = self.layout

        box = layout.box()
        box.label(text="Rigging", icon='OUTLINER_OB_ARMATURE')
        box.row().operator("rivals_porting.tasty_rig", icon='ARMATURE_DATA')
        box.row().operator("rivals_porting.additive_fix", icon='ANIM')

        box = layout.box()
        box.label(text="Links", icon='LINK_BLEND')
        row = box.row()
        row.operator("rivals_porting.github", icon='FILE_SCRIPT')
        row.operator("rivals_porting.discord", icon='MONKEY')


classes = (
    RIVALS_OT_AdditiveFix,
    RIVALS_OT_TastyRig,
    RIVALS_OT_Github,
    RIVALS_OT_Discord,
    RIVALS_PT_Utils,
)


def register():
    for cls in classes:
        register_class(cls)


def unregister():
    for cls in reversed(classes):
        unregister_class(cls)
