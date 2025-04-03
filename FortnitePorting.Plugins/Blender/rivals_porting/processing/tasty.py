import bpy

import re
from .import_context import *
from ..utils import *
from mathutils import Matrix, Vector, Euler, Quaternion

class Lazy:
    def __init__(self, getter):
        self.getter = getter

    def value(self):
        try:
            return self.getter()
        except Exception as e:
            return None

class DriverVariable:

    def __init__(self, name, type, target, path):
        self.name = name
        self.type = type
        self.target = target
        self.path = path

class DriverBuilder:
    def __init__(self, expression: str, variables: list[DriverVariable]):
        self.expression = expression
        self.variables = variables

    def add_to(self, target_object, property_name: str):
        driver = target_object.driver_add(property_name).driver
        driver.expression = self.expression

        for variable in self.variables:
            new_var = driver.variables.new()
            new_var.name = variable.name
            new_var.type = variable.type

            target = new_var.targets[0]
            target.id = variable.target.id_data
            target.data_path = variable.path

class TastyRigOptions:
    def __init__(self, scale: float = 0.01, use_dynamic_bone_shape=True, simplify_face_bones=True, master_skeleton=None):
        self.scale = scale
        self.use_dynamic_bone_shape = use_dynamic_bone_shape
        self.simplify_face_bones = simplify_face_bones
        self.master_skeleton = master_skeleton

class CustomShape:
    def __init__(self, bone_name, object_name, palette_name, wire_width=2.5, offset=(0.0, 0.0, 0.0), rotation=(0.0, 0.0, 0.0), scale=1.0, scale_to_bone_length=False, collection=None):
        self.bone_name = bone_name
        self.object_name = object_name
        self.palette_name = palette_name
        self.wire_width = wire_width
        self.offset = offset
        self.rotation = rotation
        self.scale = scale
        self.scale_to_bone_length = scale_to_bone_length
        self.collection = collection

class IKBone:
    def __init__(self, bone_name, target_name, pole_name, chain_length, pole_angle=180, use_rotation=False, lock_axes=None, limit_axes=None, driver=None, name=None):
        self.bone_name = bone_name
        self.target_name = target_name
        self.pole_name = pole_name
        self.chain_length = chain_length
        self.pole_angle = pole_angle
        self.use_rotation = use_rotation
        self.lock_axes = lock_axes or []
        self.limit_axes = limit_axes or {}
        self.driver = driver
        self.name = name or "IK"


TOGGLE_ON = 0.1
TOGGLE_OFF = 0.0

def create_tasty_rig(context, target_skeleton, options: TastyRigOptions):
    target_skeleton["is_tasty"] = True
    armature_data = target_skeleton.data
    is_metahuman = any(armature_data.bones, lambda bone: bone.name == "FACIAL_C_FacialRoot")
    is_human = any(armature_data.bones, lambda bone: bone.name == "facial_master")

    base_collection = armature_data.collections.new("Base")
    rig_collection = armature_data.collections.new("Rig")
    face_collection = armature_data.collections.new("Face")
    twist_collection = armature_data.collections.new("Twist")
    dynamic_collection = armature_data.collections.new("Dynamic")
    deform_collection = armature_data.collections.new("Deform")
    tail_collection = armature_data.collections.new("Tail")
    extra_collection = armature_data.collections.new("Extra")

    base_collection.is_visible = False
    extra_collection.is_visible = False
    
    if options.simplify_face_bones:
        extra_face_collection = armature_data.collections.new("Additional Face")
        extra_face_collection.is_visible = False

    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.context.view_layer.objects.active = target_skeleton
    target_skeleton.select_set(True)

    if tasty_options_obj := bpy.data.objects.get("Tasty_Options"):
        bpy.context.collection.objects.link(tasty_options_obj)

        bpy.context.view_layer.objects.active = target_skeleton
        target_skeleton.select_set(True)
        tasty_options_obj.select_set(True)
        bpy.ops.object.join()

        if ik_finger_toggle := target_skeleton.pose.bones.get("finger_toggle"):
            ik_finger_toggle.location[0] = TOGGLE_OFF

        if world_space_pole_toggle := target_skeleton.pose.bones.get("pole_toggle"):
            world_space_pole_toggle.location[0] = TOGGLE_ON


    # bone creation and modification
    bpy.ops.object.mode_set(mode='EDIT')
    edit_bones = armature_data.edit_bones

    for new_name, target_name, parent_name in dupe_bones:
        if not (target_bone := edit_bones.get(target_name)): continue
        if not (parent_bone := edit_bones.get(parent_name)): continue

        bone = edit_bones.get(new_name) or edit_bones.new(new_name)
        bone.parent = parent_bone

        bone.head = target_bone.head
        bone.tail = target_bone.tail
        bone.roll = target_bone.roll

    r_eye_name = "FACIAL_R_Eye" if is_metahuman else ("r_eye_ball" if is_human else "R_Eyeball_A_jnt")
    l_eye_name = "FACIAL_L_Eye" if is_metahuman else ("l_eye_ball" if is_human else "L_Eyeball_A_jnt")

    # new name, parent name, (head, tail, roll)
    new_bones = [
        ("ik_foot_pole_r", "ik_foot_root", Lazy(lambda: (edit_bones["calf_r"].head + Vector((-0.05, -0.75, 0)), edit_bones["calf_r"].head + Vector((-0.05, -0.7, 0)), 0))),
        ("ik_foot_pole_l", "ik_foot_root", Lazy(lambda: (edit_bones["calf_l"].head + Vector((0.05, -0.75, 0)), edit_bones["calf_l"].head + Vector((0.05, -0.7, 0)), 0))),
        ("ik_hand_pole_r", "ik_hand_root", Lazy(lambda: (edit_bones["lowerarm_r"].head + Vector((0, 0.75, 0)), edit_bones["lowerarm_r"].head + Vector((0, 0.7, 0)), 0))),
        ("ik_hand_pole_l", "ik_hand_root", Lazy(lambda: (edit_bones["lowerarm_l"].head + Vector((0, 0.75, 0)), edit_bones["lowerarm_l"].head + Vector((0, 0.7, 0)), 0))),

        ("ik_foot_ctrl_r", "ik_foot_r", Lazy(lambda: (edit_bones["ball_r"].head + Vector((0, 0.25, 0.05)), edit_bones["ball_r"].tail + Vector((0, 0.25, 0.05)), edit_bones["ball_r"].roll))),
        ("ik_foot_roll_front_r", "ik_foot_r", Lazy(lambda: (Vector((edit_bones["ball_r"].head.x, edit_bones["ball_r"].head.y - 0.05, 0)), Vector((edit_bones["ball_r"].tail.x, edit_bones["ball_r"].tail.y - 0.05, 0)), edit_bones["ball_r"].roll))),
        ("ik_foot_roll_inner_r", "ik_foot_roll_front_r", Lazy(lambda: (Vector((edit_bones["ball_r"].head.x + 0.04, edit_bones["ball_r"].head.y, 0)), Vector((edit_bones["ball_r"].tail.x + 0.04, edit_bones["ball_r"].tail.y, 0)), edit_bones["ball_r"].roll))),
        ("ik_foot_roll_outer_r", "ik_foot_roll_inner_r", Lazy(lambda: (Vector((edit_bones["ball_r"].head.x - 0.04, edit_bones["ball_r"].head.y, 0)), Vector((edit_bones["ball_r"].tail.x - 0.04, edit_bones["ball_r"].tail.y, 0)), edit_bones["ball_r"].roll))),
        ("ik_foot_roll_back_r", "ik_foot_roll_outer_r", Lazy(lambda: (Vector((edit_bones["foot_r"].head.x, edit_bones["foot_r"].head.y + 0.065, 0)), Vector((edit_bones["foot_r"].tail.x, edit_bones["foot_r"].tail.y + 0.065, 0)), edit_bones["ball_r"].roll))),
        ("ik_ball_roll_r", "ik_foot_roll_back_r", Lazy(lambda:  (edit_bones["ball_r"].head, edit_bones["ik_foot_r"].head, edit_bones["ball_r"].roll))),
        ("ik_ball_ctrl_r", "ik_foot_roll_back_r", Lazy(lambda: (edit_bones["ball_r"].head, edit_bones["ball_r"].tail, edit_bones["ball_r"].roll))),
        ("ik_foot_target_r", "ik_ball_roll_r", Lazy(lambda: (edit_bones["ik_foot_r"].head, edit_bones["ik_foot_r"].tail, edit_bones["ik_foot_r"].roll))),
        ("ik_foot_rot_ctrl_r", "ik_foot_target_r", Lazy(lambda: (edit_bones["foot_r"].head, edit_bones["foot_r"].tail, edit_bones["foot_r"].roll))),

        ("ik_foot_ctrl_l", "ik_foot_l", Lazy(lambda: (edit_bones["ball_l"].head + Vector((0, 0.25, 0.05)), edit_bones["ball_l"].tail + Vector((0, 0.25, 0.05)), edit_bones["ball_l"].roll))),
        ("ik_foot_roll_front_l", "ik_foot_l", Lazy(lambda: (Vector((edit_bones["ball_l"].head.x, edit_bones["ball_l"].head.y - 0.05, 0)), Vector((edit_bones["ball_l"].tail.x, edit_bones["ball_l"].tail.y - 0.05, 0)), edit_bones["ball_l"].roll))),
        ("ik_foot_roll_inner_l", "ik_foot_roll_front_l", Lazy(lambda: (Vector((edit_bones["ball_l"].head.x + 0.04, edit_bones["ball_l"].head.y, 0)), Vector((edit_bones["ball_l"].tail.x + 0.04, edit_bones["ball_l"].tail.y, 0)), edit_bones["ball_l"].roll))),
        ("ik_foot_roll_outer_l", "ik_foot_roll_inner_l", Lazy(lambda: (Vector((edit_bones["ball_l"].head.x - 0.04, edit_bones["ball_l"].head.y, 0)), Vector((edit_bones["ball_l"].tail.x - 0.04, edit_bones["ball_l"].tail.y, 0)), edit_bones["ball_l"].roll))),
        ("ik_foot_roll_back_l", "ik_foot_roll_outer_l", Lazy(lambda: (Vector((edit_bones["foot_l"].head.x, edit_bones["foot_l"].head.y + 0.065, 0)), Vector((edit_bones["foot_l"].tail.x, edit_bones["foot_l"].tail.y + 0.065, 0)), edit_bones["ball_l"].roll))),
        ("ik_ball_roll_l", "ik_foot_roll_back_l", Lazy(lambda:  (edit_bones["ball_l"].head, edit_bones["ik_foot_l"].head, edit_bones["ball_l"].roll))),
        ("ik_ball_ctrl_l", "ik_foot_roll_back_l", Lazy(lambda: (edit_bones["ball_l"].head, edit_bones["ball_l"].tail, edit_bones["ball_l"].roll))),
        ("ik_foot_target_l", "ik_ball_roll_l", Lazy(lambda: (edit_bones["ik_foot_l"].head, edit_bones["ik_foot_l"].tail, edit_bones["ik_foot_l"].roll))),
        ("ik_foot_rot_ctrl_l", "ik_foot_target_l", Lazy(lambda: (edit_bones["foot_l"].head, edit_bones["foot_l"].tail, edit_bones["foot_l"].roll))),

        ("ik_finger_thumb_r", "ik_hand_parent_r", Lazy(lambda: (edit_bones["thumb_03_r"].tail, 2 * edit_bones["thumb_03_r"].tail - edit_bones["thumb_03_r"].head, edit_bones["thumb_03_r"].roll))),
        ("ik_finger_index_r", "ik_hand_parent_r", Lazy(lambda: (edit_bones["index_03_r"].tail, 2 * edit_bones["index_03_r"].tail - edit_bones["index_03_r"].head, edit_bones["index_03_r"].roll))),
        ("ik_finger_middle_r", "ik_hand_parent_r", Lazy(lambda: (edit_bones["middle_03_r"].tail, 2 * edit_bones["middle_03_r"].tail - edit_bones["middle_03_r"].head, edit_bones["middle_03_r"].roll))),
        ("ik_finger_ring_r", "ik_hand_parent_r", Lazy(lambda: (edit_bones["ring_03_r"].tail, 2 * edit_bones["ring_03_r"].tail - edit_bones["ring_03_r"].head, edit_bones["ring_03_r"].roll))),
        ("ik_finger_pinky_r", "ik_hand_parent_r", Lazy(lambda: (edit_bones["pinky_03_r"].tail, 2 * edit_bones["pinky_03_r"].tail - edit_bones["pinky_03_r"].head, edit_bones["pinky_03_r"].roll))),

        ("ik_finger_thumb_l", "ik_hand_parent_l", Lazy(lambda: (edit_bones["thumb_03_l"].tail, 2 * edit_bones["thumb_03_l"].tail - edit_bones["thumb_03_l"].head, edit_bones["thumb_03_l"].roll))),
        ("ik_finger_index_l", "ik_hand_parent_l", Lazy(lambda: (edit_bones["index_03_l"].tail, 2 * edit_bones["index_03_l"].tail - edit_bones["index_03_l"].head, edit_bones["index_03_l"].roll))),
        ("ik_finger_middle_l", "ik_hand_parent_l", Lazy(lambda: (edit_bones["middle_03_l"].tail, 2 * edit_bones["middle_03_l"].tail - edit_bones["middle_03_l"].head, edit_bones["middle_03_l"].roll))),
        ("ik_finger_ring_l", "ik_hand_parent_l", Lazy(lambda: (edit_bones["ring_03_l"].tail, 2 * edit_bones["ring_03_l"].tail - edit_bones["ring_03_l"].head, edit_bones["ring_03_l"].roll))),
        ("ik_finger_pinky_l", "ik_hand_parent_l", Lazy(lambda: (edit_bones["pinky_03_l"].tail, 2 * edit_bones["pinky_03_l"].tail - edit_bones["pinky_03_l"].head, edit_bones["pinky_03_l"].roll))),

        ("ik_dog_ball_r", "ik_foot_target_r", Lazy(lambda: (edit_bones["dog_ball_r"].tail, 2 * edit_bones["dog_ball_r"].tail - edit_bones["dog_ball_r"].head, edit_bones["dog_ball_r"].roll))),
        ("ik_dog_ball_l", "ik_foot_target_l", Lazy(lambda: (edit_bones["dog_ball_l"].tail, 2 * edit_bones["dog_ball_l"].tail - edit_bones["dog_ball_l"].head, edit_bones["dog_ball_l"].roll))),

        ("ik_wolf_ball_r", "ik_foot_target_r", Lazy(lambda: (edit_bones["wolf_ball_r"].tail, edit_bones["wolf_ball_r"].tail + Vector((0, 0.25, 0)), edit_bones["wolf_ball_r"].roll))),
        ("ik_wolf_ball_l", "ik_foot_target_l", Lazy(lambda: (edit_bones["wolf_ball_l"].tail,edit_bones["wolf_ball_l"].tail + Vector((0, 0.25, 0)), edit_bones["wolf_ball_l"].roll))),

        ("eye_control_r", "head", Lazy(lambda: (edit_bones[r_eye_name].head - Vector((0, 0.325, 0)), edit_bones[r_eye_name].head - Vector((0, 0.35, 0)), edit_bones[r_eye_name].roll))),
        ("eye_control_l", "head", Lazy(lambda: (edit_bones[l_eye_name].head - Vector((0, 0.325, 0)), edit_bones[l_eye_name].head - Vector((0, 0.35, 0)), edit_bones[l_eye_name].roll))),
        ("eye_control_parent", "head", Lazy(lambda: ((edit_bones["eye_control_r"].head + edit_bones["eye_control_l"].head) / 2, (edit_bones["eye_control_r"].tail + edit_bones["eye_control_l"].tail) / 2, 0))),
    ]

    for new_name, parent_name, lazy in new_bones:
        if not (data_tuple := lazy.value()): continue
        if not (parent_bone := edit_bones.get(parent_name)): continue

        bone = edit_bones.get(new_name) or edit_bones.new(new_name)
        bone.parent = parent_bone

        head, tail, roll = data_tuple
        bone.head = head
        bone.tail = tail
        bone.roll = roll

    for name, parent in parent_adjustment_bones:
        if not (bone := edit_bones.get(name)): continue
        if not (parent_bone := edit_bones.get(parent)): continue

        bone_parent(bone, parent_bone)

    if options.simplify_face_bones:
        for name, parent in face_parent_adjustment_bones:
            if not (bone := edit_bones.get(name)): continue
            if not (parent_bone := edit_bones.get(parent)): continue
            bone_parent(bone, parent_bone)
        

    head_adjustment_bones = [
        ("R_eye_lid_upper_mid", Lazy(lambda: edit_bones["R_eye"].head)),
        ("R_eye_lid_lower_mid", Lazy(lambda: edit_bones["R_eye"].head)),
        ("L_eye_lid_upper_mid", Lazy(lambda: edit_bones["L_eye"].head)),
        ("L_eye_lid_lower_mid", Lazy(lambda: edit_bones["L_eye"].head)),
    ]

    for bone_name, lazy in head_adjustment_bones:
        if not (bone := edit_bones.get(bone_name)): continue
        if not (position := lazy.value()): continue

        bone_head(bone, position)

    tail_adjustment_bones = [
        ("lowerarm_r", Lazy(lambda: edit_bones["hand_r"].head)),
        ("lowerarm_l", Lazy(lambda: edit_bones["hand_l"].head)),
        ("calf_r", Lazy(lambda: edit_bones["foot_r"].head)),
        ("calf_l", Lazy(lambda: edit_bones["foot_l"].head)),
        ("R_eye", Lazy(lambda: edit_bones["R_eye"].head - Vector((0, 0.1, 0)))),
        ("L_eye", Lazy(lambda: edit_bones["L_eye"].head - Vector((0, 0.1, 0)))),
        ("FACIAL_R_Eye", Lazy(lambda: edit_bones["FACIAL_R_Eye"].head - Vector((0, 0.1, 0)))),
        ("FACIAL_L_Eye", Lazy(lambda: edit_bones["FACIAL_L_Eye"].head - Vector((0, 0.1, 0)))),
        ("r_eye_ball", Lazy(lambda: edit_bones["r_eye_ball"].head - Vector((0, 0.1, 0)))),
        ("l_eye_ball", Lazy(lambda: edit_bones["l_eye_ball"].head - Vector((0, 0.1, 0)))),
        ("FACIAL_L_EyeParallel", Lazy(lambda: edit_bones["FACIAL_L_EyeParallel"].head - Vector((0, 0.1, 0)))),
        ("FACIAL_R_EyeParallel", Lazy(lambda: edit_bones["FACIAL_R_EyeParallel"].head - Vector((0, 0.1, 0)))),
        ("C_jaw", Lazy(lambda: edit_bones["C_jaw"].head - Vector((0, 0.1, 0)))),
    ]

    for bone_name, lazy in tail_adjustment_bones:
        if not (bone := edit_bones.get(bone_name)): continue
        if not (position := lazy.value()): continue

        bone_tail(bone, position)

    roll_adjustment_bones = [
        ("C_jaw", 0),
    ]

    for name, roll in roll_adjustment_bones:
        if not (bone := edit_bones.get(name)): continue

        bone_roll(bone, roll)

    for name, transform in transform_adjustment_bones:
        if not (bone := edit_bones.get(name)): continue

        bone.matrix @= Matrix.Translation(transform)

    if (lower_lip_bone := edit_bones.get("FACIAL_C_LowerLipRotation")) and (jaw_bone := edit_bones.get("FACIAL_C_Jaw")):
        bone_parent(lower_lip_bone, jaw_bone)

    # pose bone modifications
    bpy.ops.object.mode_set(mode='POSE')

    pose_bones = target_skeleton.pose.bones

    # TODO definitely rewrite this ew
    def add_foot_ik_constraints(suffix):
        is_left = suffix == "l"
        ctrl_bone_name = f"ik_foot_ctrl_{suffix}"

        if inner_roll_bone := pose_bones.get(f"ik_foot_roll_inner_{suffix}"):
            copy_rotation = inner_roll_bone.constraints.new("COPY_ROTATION")
            copy_rotation.target = target_skeleton
            copy_rotation.subtarget = ctrl_bone_name
            copy_rotation.use_x = False
            copy_rotation.use_y = True
            copy_rotation.use_z = False
            copy_rotation.target_space = "LOCAL"
            copy_rotation.owner_space = "LOCAL"

            limit_rotation = inner_roll_bone.constraints.new("LIMIT_ROTATION")
            limit_rotation.use_limit_y = True
            limit_rotation.min_y = radians(-180) if is_left else 0
            limit_rotation.max_y = 0 if is_left else radians(180)
            limit_rotation.owner_space = "LOCAL"

        if outer_roll_bone := pose_bones.get(f"ik_foot_roll_outer_{suffix}"):
            copy_rotation = outer_roll_bone.constraints.new("COPY_ROTATION")
            copy_rotation.target = target_skeleton
            copy_rotation.subtarget = ctrl_bone_name
            copy_rotation.use_x = False
            copy_rotation.use_y = True
            copy_rotation.use_z = False
            copy_rotation.target_space = "LOCAL"
            copy_rotation.owner_space = "LOCAL"

            limit_rotation = outer_roll_bone.constraints.new("LIMIT_ROTATION")
            limit_rotation.use_limit_y = True
            limit_rotation.min_y = 0 if is_left else radians(-180)
            limit_rotation.max_y = radians(180) if is_left else 0
            limit_rotation.owner_space = "LOCAL"

        if front_roll_bone := pose_bones.get(f"ik_foot_roll_front_{suffix}"):
            copy_rotation = front_roll_bone.constraints.new("COPY_ROTATION")
            copy_rotation.target = target_skeleton
            copy_rotation.subtarget = ctrl_bone_name
            copy_rotation.use_x = False
            copy_rotation.use_y = False
            copy_rotation.use_z = True
            copy_rotation.target_space = "LOCAL"
            copy_rotation.owner_space = "LOCAL"

            limit_rotation = front_roll_bone.constraints.new("LIMIT_ROTATION")
            limit_rotation.use_limit_z = True
            limit_rotation.min_z = radians(-180)
            limit_rotation.max_z = 0
            limit_rotation.owner_space = "LOCAL"

        if back_roll_bone := pose_bones.get(f"ik_foot_roll_back_{suffix}"):
            copy_rotation = back_roll_bone.constraints.new("COPY_ROTATION")
            copy_rotation.target = target_skeleton
            copy_rotation.subtarget = ctrl_bone_name
            copy_rotation.use_x = False
            copy_rotation.use_y = False
            copy_rotation.use_z = True
            copy_rotation.invert_z = True
            copy_rotation.target_space = "LOCAL"
            copy_rotation.owner_space = "LOCAL"

            limit_rotation = back_roll_bone.constraints.new("LIMIT_ROTATION")
            limit_rotation.use_limit_z = True
            limit_rotation.min_z = radians(-180)
            limit_rotation.max_z = 0
            limit_rotation.owner_space = "LOCAL"

        if ball_roll_bone := pose_bones.get(f"ik_ball_roll_{suffix}"):
            transformation = ball_roll_bone.constraints.new("TRANSFORM")
            transformation.target = target_skeleton
            transformation.subtarget = ctrl_bone_name
            transformation.target_space = "LOCAL"
            transformation.owner_space = "LOCAL"

            transformation.map_from = "LOCATION"
            transformation.from_min_x = 0
            transformation.from_max_x = 0.1

            transformation.map_to = "ROTATION"
            transformation.map_to_z_from = "X"
            transformation.to_min_z_rot = 0
            transformation.to_max_z_rot = radians(-55)

        if ball_bone := pose_bones.get(f"ball_{suffix}"):
            copy_rotation = ball_bone.constraints.new("COPY_ROTATION")
            copy_rotation.target = target_skeleton
            copy_rotation.subtarget = f"ik_ball_ctrl_{suffix}"
            copy_rotation.use_x = True
            copy_rotation.use_y = True
            copy_rotation.use_z = True
            copy_rotation.target_space = "WORLD"
            copy_rotation.owner_space = "WORLD"

            driver = DriverBuilder("loc > 0", [
                DriverVariable("loc", "SINGLE_PROP", target_skeleton, f'pose.bones["ik_foot_ctrl_{suffix}"].location[0]')
            ])

            driver.add_to(copy_rotation, "influence")

        if ctrl_bone := pose_bones.get(ctrl_bone_name):
            limit_location = ctrl_bone.constraints.new("LIMIT_LOCATION")
            limit_location.use_min_x = True
            limit_location.use_min_y = True
            limit_location.use_min_z = True
            limit_location.use_max_x = True
            limit_location.use_max_y = True
            limit_location.use_max_z = True
            limit_location.min_x = 0
            limit_location.max_x = 0.1
            limit_location.owner_space = "LOCAL"
            limit_location.use_transform_limit = True


    add_foot_ik_constraints("r")
    add_foot_ik_constraints("l")

    use_pole_arm_driver = DriverBuilder("pole_toggle > 0.05 and arm_toggle > 0.05", [
        DriverVariable("pole_toggle", "SINGLE_PROP", target_skeleton, 'pose.bones["pole_toggle"].location[0]'),
        DriverVariable("arm_toggle", "SINGLE_PROP", target_skeleton, 'pose.bones["arm_toggle"].location[0]')
    ])

    dont_use_pole_arm_driver = DriverBuilder("pole_toggle < 0.05 and arm_toggle > 0.05", [
        DriverVariable("pole_toggle", "SINGLE_PROP", target_skeleton, 'pose.bones["pole_toggle"].location[0]'),
        DriverVariable("arm_toggle", "SINGLE_PROP", target_skeleton, 'pose.bones["arm_toggle"].location[0]')
    ])

    hide_ik_arm_driver = DriverBuilder("loc < 0.05", [
        DriverVariable("loc", "SINGLE_PROP", target_skeleton, 'pose.bones["arm_toggle"].location[0]')
    ])

    hide_pole_arm_driver = DriverBuilder("pole_toggle < 0.05 or arm_toggle < 0.05", [
        DriverVariable("pole_toggle", "SINGLE_PROP", target_skeleton, 'pose.bones["pole_toggle"].location[0]'),
        DriverVariable("arm_toggle", "SINGLE_PROP", target_skeleton, 'pose.bones["arm_toggle"].location[0]')
    ])

    use_pole_leg_driver = DriverBuilder("pole_toggle > 0.05 and leg_toggle > 0.05", [
        DriverVariable("pole_toggle", "SINGLE_PROP", target_skeleton, 'pose.bones["pole_toggle"].location[0]'),
        DriverVariable("leg_toggle", "SINGLE_PROP", target_skeleton, 'pose.bones["leg_toggle"].location[0]')
    ])

    dont_use_pole_leg_driver = DriverBuilder("pole_toggle < 0.05 and leg_toggle > 0.05", [
        DriverVariable("pole_toggle", "SINGLE_PROP", target_skeleton, 'pose.bones["pole_toggle"].location[0]'),
        DriverVariable("leg_toggle", "SINGLE_PROP", target_skeleton, 'pose.bones["leg_toggle"].location[0]')
    ])

    hide_ik_leg_driver = DriverBuilder("loc < 0.05", [
        DriverVariable("loc", "SINGLE_PROP", target_skeleton, 'pose.bones["leg_toggle"].location[0]')
    ])

    hide_pole_leg_driver = DriverBuilder("pole_toggle < 0.05 or leg_toggle < 0.05", [
        DriverVariable("pole_toggle", "SINGLE_PROP", target_skeleton, 'pose.bones["pole_toggle"].location[0]'),
        DriverVariable("leg_toggle", "SINGLE_PROP", target_skeleton, 'pose.bones["leg_toggle"].location[0]')
    ])

    use_ik_finger_driver = DriverBuilder("loc > 0.05", [
        DriverVariable("loc", "SINGLE_PROP", target_skeleton, 'pose.bones["finger_toggle"].location[0]')
    ])

    dont_use_ik_finger_driver = DriverBuilder("loc < 0.05", [
        DriverVariable("loc", "SINGLE_PROP", target_skeleton, 'pose.bones["finger_toggle"].location[0]')
    ])

    # bone name, target name, chain count, use rotation, lock axes
    ik_bones = [
        IKBone("lowerarm_r", "ik_hand_target_r", "ik_hand_pole_r", 2, name="IK w/ Pole", lock_axes=["X"], limit_axes={"Y": [-45, 45], "Z": [-120, 30]}, driver=use_pole_arm_driver),
        IKBone("lowerarm_l", "ik_hand_target_l", "ik_hand_pole_l", 2, name="IK w/ Pole", lock_axes=["X"], limit_axes={"Y": [-45, 45], "Z": [-120, 30]}, driver=use_pole_arm_driver),
        IKBone("calf_r", "ik_foot_target_r", "ik_foot_pole_r", 2, name="IK w/ Pole", lock_axes=["X"], limit_axes={"Y": [-15, 15], "Z": [-135, 5]}, driver=use_pole_leg_driver),
        IKBone("calf_l", "ik_foot_target_l", "ik_foot_pole_l", 2, name="IK w/ Pole", lock_axes=["X"], limit_axes={"Y": [-15, 15], "Z": [-135, 5]}, driver=use_pole_leg_driver),

        IKBone("lowerarm_r", "ik_hand_target_r", None, 2, name="IK w/o Pole", lock_axes=["X"], limit_axes={"Y": [-45, 45], "Z": [-120, 30]}, driver=dont_use_pole_arm_driver),
        IKBone("lowerarm_l", "ik_hand_target_l", None, 2, name="IK w/o Pole", lock_axes=["X"], limit_axes={"Y": [-45, 45], "Z": [-120, 30]}, driver=dont_use_pole_arm_driver),
        IKBone("calf_r", "ik_foot_target_r", None, 2, name="IK w/o Pole", lock_axes=["X"], limit_axes={"Y": [-15, 15], "Z": [-135, 5]}, driver=dont_use_pole_leg_driver),
        IKBone("calf_l", "ik_foot_target_l", None, 2, name="IK w/o Pole", lock_axes=["X"], limit_axes={"Y": [-15, 15], "Z": [-135, 5]}, driver=dont_use_pole_leg_driver),

        IKBone("dog_ball_r", "ik_dog_ball_r", "ik_foot_pole_r", 4, use_rotation=True, name="IK w/ Pole", lock_axes=["X"], limit_axes={"Y": [-15, 15], "Z": [-135, 5]}, driver=use_pole_leg_driver),
        IKBone("dog_ball_l", "ik_dog_ball_l", "ik_foot_pole_l", 4, use_rotation=True, name="IK w/ Pole", lock_axes=["X"], limit_axes={"Y": [-15, 15], "Z": [-135, 5]}, driver=use_pole_leg_driver),
        IKBone("dog_ball_r", "ik_dog_ball_r", None, 4, use_rotation=True, name="IK w/o Pole", lock_axes=["X"], limit_axes={"Y": [-15, 15], "Z": [-135, 5]}, driver=dont_use_pole_leg_driver),
        IKBone("dog_ball_l", "ik_dog_ball_l", None, 4, use_rotation=True, name="IK w/o Pole", lock_axes=["X"], limit_axes={"Y": [-15, 15], "Z": [-135, 5]}, driver=dont_use_pole_leg_driver),

        IKBone("wolf_ball_r", "ik_wolf_ball_r", "ik_foot_pole_r", 4, use_rotation=True, name="IK w/ Pole", lock_axes=["X"], limit_axes={"Y": [-15, 15], "Z": [-135, 5]}, driver=use_pole_leg_driver),
        IKBone("wolf_ball_l", "ik_wolf_ball_l", "ik_foot_pole_l", 4, use_rotation=True, name="IK w/ Pole", lock_axes=["X"], limit_axes={"Y": [-15, 15], "Z": [-135, 5]}, driver=use_pole_leg_driver),
        IKBone("wolf_ball_r", "ik_wolf_ball_r", None, 4, use_rotation=True, name="IK w/o Pole", lock_axes=["X"], limit_axes={"Y": [-15, 15], "Z": [-135, 5]}, driver=dont_use_pole_leg_driver),
        IKBone("wolf_ball_l", "ik_wolf_ball_l", None, 4, use_rotation=True, name="IK w/o Pole", lock_axes=["X"], limit_axes={"Y": [-15, 15], "Z": [-135, 5]}, driver=dont_use_pole_leg_driver),

        IKBone("thumb_03_r", "ik_finger_thumb_r", "ik_finger_pole_thumb_r", 2, pole_angle=0, driver=use_ik_finger_driver),
        IKBone("index_03_r", "ik_finger_index_r", "ik_finger_pole_index_r", 3, pole_angle=0, driver=use_ik_finger_driver),
        IKBone("middle_03_r", "ik_finger_middle_r", "ik_finger_pole_middle_r", 3, pole_angle=0, driver=use_ik_finger_driver),
        IKBone("ring_03_r", "ik_finger_ring_r", "ik_finger_pole_ring_r", 3, pole_angle=0, driver=use_ik_finger_driver),
        IKBone("pinky_03_r", "ik_finger_pinky_r", "ik_finger_pole_pinky_r", 3, pole_angle=0, driver=use_ik_finger_driver),

        IKBone("thumb_03_l", "ik_finger_thumb_l", "ik_finger_pole_thumb_l", 2, pole_angle=0, driver=use_ik_finger_driver),
        IKBone("index_03_l", "ik_finger_index_l", "ik_finger_pole_index_l", 3, pole_angle=0, driver=use_ik_finger_driver),
        IKBone("middle_03_l", "ik_finger_middle_l", "ik_finger_pole_middle_l", 3, pole_angle=0, driver=use_ik_finger_driver),
        IKBone("ring_03_l", "ik_finger_ring_l", "ik_finger_pole_ring_l", 3, pole_angle=0, driver=use_ik_finger_driver),
        IKBone("pinky_03_l", "ik_finger_pinky_l", "ik_finger_pole_pinky_l", 3, pole_angle=0, driver=use_ik_finger_driver),
    ]

    for ik_bone in ik_bones:
        if not (bone := pose_bones.get(ik_bone.bone_name)): continue

        constraint = bone.constraints.new("IK")
        constraint.name = ik_bone.name
        constraint.target = target_skeleton
        constraint.subtarget = ik_bone.target_name
        constraint.chain_count = ik_bone.chain_length
        constraint.use_rotation = ik_bone.use_rotation

        if ik_bone.pole_name:
            constraint.pole_target = target_skeleton
            constraint.pole_subtarget = ik_bone.pole_name
            constraint.pole_angle = radians(ik_bone.pole_angle)

        for axis in ik_bone.lock_axes:
            setattr(bone, f"lock_ik_{axis.lower()}", True)

        for axis, values in ik_bone.limit_axes.items():
            setattr(bone, f"use_ik_limit_{axis.lower()}", True)
            setattr(bone, f"ik_min_{axis.lower()}", radians(values[0]))
            setattr(bone, f"ik_max_{axis.lower()}", radians(values[1]))

        if ik_bone.driver:
            ik_bone.driver.add_to(constraint, "influence")

    lock_ik_bones = [
        ("dog_foot_r", ["X"]),
        ("dog_foot_l", ["X"]),
        ("wolf_foot_r", ["X"]),
        ("wolf_foot_l", ["X"]),
    ]

    for name, axes in lock_ik_bones:
        if not (bone := pose_bones.get(name)): continue

        for axis in axes:
            setattr(bone, f"lock_ik_{axis.lower()}", True)


    # bone name, target name, target space, owner space, mix, weight
    copy_rotation_bones = [
        ("hand_r", "ik_hand_parent_r", "POSE", "POSE", "REPLACE", 1.0, None),
        ("hand_l", "ik_hand_parent_l", "POSE", "POSE", "REPLACE", 1.0, None),
        ("foot_r", "ik_foot_rot_ctrl_r", "POSE", "POSE", "REPLACE", 1.0, None),
        ("foot_l", "ik_foot_rot_ctrl_l", "POSE", "POSE", "REPLACE", 1.0, None),

        ("R_eye_lid_upper_mid", "R_eye", "LOCAL_OWNER_ORIENT", "LOCAL_WITH_PARENT", "REPLACE", 0.25, None),
        ("R_eye_lid_lower_mid", "R_eye", "LOCAL_OWNER_ORIENT", "LOCAL_WITH_PARENT", "REPLACE", 0.25, None),
        ("L_eye_lid_upper_mid", "L_eye", "LOCAL_OWNER_ORIENT", "LOCAL_WITH_PARENT", "REPLACE", 0.25, None),
        ("L_eye_lid_lower_mid", "L_eye", "LOCAL_OWNER_ORIENT", "LOCAL_WITH_PARENT", "REPLACE", 0.25, None),

        ("FACIAL_R_EyelidUpperA", "FACIAL_R_Eye", "LOCAL_OWNER_ORIENT", "LOCAL_WITH_PARENT", "REPLACE", 0.25, None),
        ("FACIAL_R_EyelidLowerA", "FACIAL_R_Eye", "LOCAL_OWNER_ORIENT", "LOCAL_WITH_PARENT", "REPLACE", 0.25, None),
        ("FACIAL_L_EyelidUpperA", "FACIAL_L_Eye", "LOCAL_OWNER_ORIENT", "LOCAL_WITH_PARENT", "REPLACE", 0.25, None),
        ("FACIAL_L_EyelidLowerA", "FACIAL_L_Eye", "LOCAL_OWNER_ORIENT", "LOCAL_WITH_PARENT", "REPLACE", 0.25, None),

        ("thumb_02_r", "thumb_control_r", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
        ("thumb_03_r", "thumb_control_r", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
        ("index_01_r", "index_control_r", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
        ("index_02_r", "index_control_r", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
        ("index_03_r", "index_control_r", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
        ("middle_01_r", "middle_control_r", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
        ("middle_02_r", "middle_control_r", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
        ("middle_03_r", "middle_control_r", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
        ("ring_01_r", "ring_control_r", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
        ("ring_02_r", "ring_control_r", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
        ("ring_03_r", "ring_control_r", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
        ("pinky_01_r", "pinky_control_r", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
        ("pinky_02_r", "pinky_control_r", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
        ("pinky_03_r", "pinky_control_r", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),

        ("thumb_02_l", "thumb_control_l", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
        ("thumb_03_l", "thumb_control_l", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
        ("index_01_l", "index_control_l", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
        ("index_02_l", "index_control_l", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
        ("index_03_l", "index_control_l", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
        ("middle_01_l", "middle_control_l", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
        ("middle_02_l", "middle_control_l", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
        ("middle_03_l", "middle_control_l", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
        ("ring_01_l", "ring_control_l", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
        ("ring_02_l", "ring_control_l", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
        ("ring_03_l", "ring_control_l", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
        ("pinky_01_l", "pinky_control_l", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
        ("pinky_02_l", "pinky_control_l", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
        ("pinky_03_l", "pinky_control_l", "LOCAL", "LOCAL", "ADD", 1.0, dont_use_ik_finger_driver),
    ]

    for bone_name, target_name, target_space, owner_space, mix, weight, driver in copy_rotation_bones:
        if not (bone := pose_bones.get(bone_name)): continue
        if not (target_bone := pose_bones.get(target_name)): continue

        constraint = bone.constraints.new("COPY_ROTATION")
        constraint.target = target_skeleton
        constraint.subtarget = target_name
        constraint.influence = weight
        constraint.target_space = target_space
        constraint.owner_space = owner_space
        constraint.mix_mode = mix

        if driver:
            driver.add_to(constraint, "influence")

    # bone name, space, axis -> dict[axis, list[min, max]]
    limit_rotation_bones = [
        ("ik_foot_ctrl_r", "LOCAL", {"X": [0, 0]}),
        ("ik_foot_ctrl_l", "LOCAL", {"X": [0, 0]}),
    ]

    for bone_name, space, axes in limit_rotation_bones:
        if not (bone := pose_bones.get(bone_name)): continue

        constraint = bone.constraints.new("LIMIT_ROTATION")
        constraint.owner_space = space

        for axis, min_max in axes.items():
            min = min_max[0]
            max = min_max[1]
            axis_lower = axis.lower()

            setattr(constraint, f"use_limit_{axis_lower}", True)
            setattr(constraint, f"min_{axis_lower}", min)
            setattr(constraint, f"max_{axis_lower}", max)

    for bone_name, target_name, space, weight in copy_location_bones:
        if not (bone := pose_bones.get(bone_name)): continue

        constraint = bone.constraints.new("COPY_LOCATION")
        constraint.target = target_skeleton
        constraint.subtarget = target_name
        constraint.influence = weight
        constraint.target_space = space
        constraint.owner_space = space

    track_bones = [
        ("eye_control_parent", "head")
    ]

    for bone_name, target_name in track_bones:
        if not (bone := pose_bones.get(bone_name)): continue

        constraint = bone.constraints.new('TRACK_TO')
        constraint.target = target_skeleton
        constraint.subtarget = target_name
        constraint.track_axis = 'TRACK_NEGATIVE_Y'
        constraint.up_axis = 'UP_Z'

    for bone_name, target_name in damped_track_bones:
        if not (bone := pose_bones.get(bone_name)): continue

        constraint = bone.constraints.new('DAMPED_TRACK')
        constraint.target = target_skeleton
        constraint.subtarget = target_name

    rig_bones = [
        CustomShape("root", "CTRL_Root", "THEME03"),
        CustomShape("ik_hand_gun", "CTRL_Socket", "THEME01", scale=0.1),
        CustomShape("ik_hand_r", "CTRL_Box", "THEME01"),
        CustomShape("ik_hand_l", "CTRL_Box", "THEME04"),
        CustomShape("ik_hand_target_r", "CTRL_Box", "THEME01", scale=0.5),
        CustomShape("ik_hand_target_l", "CTRL_Box", "THEME04", scale=0.5),
        CustomShape("ik_foot_r", "CTRL_Box", "THEME01"),
        CustomShape("ik_foot_l", "CTRL_Box", "THEME04"),
        CustomShape("pelvis", "CTRL_Spine", "THEME12", scale=2.0),
        CustomShape("spine_01", "CTRL_Spine", "THEME12", scale=1.3),
        CustomShape("spine_02", "CTRL_Spine", "THEME12", scale=1.2),
        CustomShape("spine_03", "CTRL_Spine", "THEME12", scale=1.2),
        CustomShape("spine_04", "CTRL_Spine", "THEME12", scale=1.4),
        CustomShape("spine_05", "CTRL_Spine", "THEME12", scale=1.5),
        CustomShape("neck_01", "CTRL_Spine", "THEME12", scale=0.9),
        CustomShape("neck_02", "CTRL_Spine", "THEME12", scale=0.75),
        CustomShape("head", "CTRL_Spine", "THEME12", scale=0.75, offset=(0.025, 0.025, 0.0)),
        CustomShape("clavicle_r", "CTRL_Clavicle", "THEME01"),
        CustomShape("clavicle_l", "CTRL_Clavicle", "THEME04"),
        CustomShape("ball_r", "CTRL_Toe", "THEME01"),
        CustomShape("ball_l", "CTRL_Toe", "THEME04"),
        CustomShape("ik_foot_pole_r", "CTRL_Pole", "THEME01"),
        CustomShape("ik_foot_pole_l", "CTRL_Pole", "THEME04"),
        CustomShape("ik_hand_pole_r", "CTRL_Pole", "THEME01"),
        CustomShape("ik_hand_pole_l", "CTRL_Pole", "THEME04"),
        CustomShape("ik_foot_ctrl_r", "CTRL_Foot_Ctrl", "THEME01"),
        CustomShape("ik_foot_ctrl_l", "CTRL_Foot_Ctrl", "THEME04"),

        CustomShape("ik_finger_thumb_r", "CTRL_Box", "THEME01", scale=0.2),
        CustomShape("ik_finger_index_r", "CTRL_Box", "THEME01", scale=0.2),
        CustomShape("ik_finger_middle_r", "CTRL_Box", "THEME01", scale=0.2),
        CustomShape("ik_finger_ring_r", "CTRL_Box", "THEME01", scale=0.2),
        CustomShape("ik_finger_pinky_r", "CTRL_Box", "THEME01", scale=0.2),
        CustomShape("ik_finger_pole_thumb_r", "CTRL_Pole", "THEME01", scale=0.25, rotation=(0, 0, radians(90))),
        CustomShape("ik_finger_pole_index_r", "CTRL_Pole", "THEME01", scale=0.25, rotation=(0, 0, radians(90))),
        CustomShape("ik_finger_pole_middle_r", "CTRL_Pole", "THEME01", scale=0.25, rotation=(0, 0, radians(90))),
        CustomShape("ik_finger_pole_ring_r", "CTRL_Pole", "THEME01", scale=0.25, rotation=(0, 0, radians(90))),
        CustomShape("ik_finger_pole_pinky_r", "CTRL_Pole", "THEME01", scale=0.25, rotation=(0, 0, radians(90))),
        CustomShape("thumb_01_r", "CTRL_Metacarpal", "THEME01", scale_to_bone_length=True),
        CustomShape("index_metacarpal_r", "CTRL_Metacarpal", "THEME01", scale_to_bone_length=True),
        CustomShape("middle_metacarpal_r", "CTRL_Metacarpal", "THEME01", scale_to_bone_length=True),
        CustomShape("ring_metacarpal_r", "CTRL_Metacarpal", "THEME01", scale_to_bone_length=True),
        CustomShape("pinky_metacarpal_r", "CTRL_Metacarpal", "THEME01", scale_to_bone_length=True),

        CustomShape("ik_finger_thumb_l", "CTRL_Box", "THEME04", scale=0.2),
        CustomShape("ik_finger_index_l", "CTRL_Box", "THEME04", scale=0.2),
        CustomShape("ik_finger_middle_l", "CTRL_Box", "THEME04", scale=0.2),
        CustomShape("ik_finger_ring_l", "CTRL_Box", "THEME04", scale=0.2),
        CustomShape("ik_finger_pinky_l", "CTRL_Box", "THEME04", scale=0.2),
        CustomShape("ik_finger_pole_thumb_l", "CTRL_Pole", "THEME04", scale=0.25, rotation=(0, 0, radians(90))),
        CustomShape("ik_finger_pole_index_l", "CTRL_Pole", "THEME04", scale=0.25, rotation=(0, 0, radians(90))),
        CustomShape("ik_finger_pole_middle_l", "CTRL_Pole", "THEME04", scale=0.25, rotation=(0, 0, radians(90))),
        CustomShape("ik_finger_pole_ring_l", "CTRL_Pole", "THEME04", scale=0.25, rotation=(0, 0, radians(90))),
        CustomShape("ik_finger_pole_pinky_l", "CTRL_Pole", "THEME04", scale=0.25, rotation=(0, 0, radians(90))),
        CustomShape("thumb_01_l", "CTRL_Metacarpal", "THEME04", scale_to_bone_length=True),
        CustomShape("index_metacarpal_l", "CTRL_Metacarpal", "THEME04", scale_to_bone_length=True),
        CustomShape("middle_metacarpal_l", "CTRL_Metacarpal", "THEME04", scale_to_bone_length=True),
        CustomShape("ring_metacarpal_l", "CTRL_Metacarpal", "THEME04", scale_to_bone_length=True),
        CustomShape("pinky_metacarpal_l", "CTRL_Metacarpal", "THEME04", scale_to_bone_length=True),

        CustomShape("thumb_control_r", "CTRL_Finger_Rotation", "THEME01", scale_to_bone_length=True),
        CustomShape("index_control_r", "CTRL_Finger_Rotation", "THEME01", scale_to_bone_length=True),
        CustomShape("middle_control_r", "CTRL_Finger_Rotation", "THEME01", scale_to_bone_length=True),
        CustomShape("ring_control_r", "CTRL_Finger_Rotation", "THEME01", scale_to_bone_length=True),
        CustomShape("pinky_control_r", "CTRL_Finger_Rotation", "THEME01", scale_to_bone_length=True),

        CustomShape("thumb_control_l", "CTRL_Finger_Rotation", "THEME04", scale_to_bone_length=True),
        CustomShape("index_control_l", "CTRL_Finger_Rotation", "THEME04", scale_to_bone_length=True),
        CustomShape("middle_control_l", "CTRL_Finger_Rotation", "THEME04", scale_to_bone_length=True),
        CustomShape("ring_control_l", "CTRL_Finger_Rotation", "THEME04", scale_to_bone_length=True),
        CustomShape("pinky_control_l", "CTRL_Finger_Rotation", "THEME04", scale_to_bone_length=True),

        CustomShape("eye_control_parent", "CTRL_Pole", "THEME02", scale=0.35, collection=face_collection),
        CustomShape("eye_control_r", "CTRL_Pole", "THEME02", scale=0.2, collection=face_collection),
        CustomShape("eye_control_l", "CTRL_Pole", "THEME02", scale=0.2, collection=face_collection),

        CustomShape("R_eye", "CTRL_Eye", "THEME02", collection=face_collection),
        CustomShape("L_eye", "CTRL_Eye", "THEME02", collection=face_collection),
        CustomShape("C_jaw", "CTRL_Jaw", "THEME02", rotation=(radians(-45), 0, 0), collection=face_collection),
        CustomShape("FACIAL_R_Eye", "CTRL_Eye", "THEME02", collection=face_collection),
        CustomShape("FACIAL_L_Eye", "CTRL_Eye", "THEME02", collection=face_collection),
        CustomShape("FACIAL_C_Jaw", "CTRL_Jaw", "THEME02", collection=face_collection),
        CustomShape("r_eye_ball", "CTRL_Eye", "THEME02", collection=face_collection),
        CustomShape("l_eye_ball", "CTRL_Eye", "THEME02", collection=face_collection),
        CustomShape("jaw", "CTRL_Jaw", "THEME02", scale=1.25, rotation=(radians(60), radians(90), 0), collection=face_collection),

        # reserve to not be affected by automatic facial bone detection
        CustomShape("R_eye_lid_upper_mid", None, "THEME02", collection=face_collection),
        CustomShape("R_eye_lid_lower_mid", None, "THEME02", collection=face_collection),
        CustomShape("L_eye_lid_upper_mid", None, "THEME02", collection=face_collection),
        CustomShape("L_eye_lid_lower_mid", None, "THEME02", collection=face_collection),
        CustomShape("FACIAL_R_EyelidUpperA", None, "THEME02", collection=face_collection),
        CustomShape("FACIAL_R_EyelidLowerA", None, "THEME02", collection=face_collection),
        CustomShape("FACIAL_L_EyelidUpperA", None, "THEME02", collection=face_collection),
        CustomShape("FACIAL_L_EyelidLowerA", None, "THEME02", collection=face_collection),
    ]

    for custom_shape in rig_bones:
        if not (pose_bone := pose_bones.get(custom_shape.bone_name)): continue

        if custom_shape.object_name is not None:
            pose_bone.custom_shape = bpy.data.objects.get(custom_shape.object_name)

        pose_bone.color.palette = custom_shape.palette_name
        pose_bone.use_custom_shape_bone_size = custom_shape.scale_to_bone_length
        pose_bone.custom_shape_wire_width = custom_shape.wire_width
        pose_bone.custom_shape_scale_xyz = (custom_shape.scale, custom_shape.scale, custom_shape.scale)
        pose_bone.custom_shape_translation = custom_shape.offset
        pose_bone.custom_shape_rotation_euler = custom_shape.rotation

        target_collection = custom_shape.collection or rig_collection
        target_collection.assign(pose_bone)

    for base_bone in base_bones:
        if not (pose_bone := pose_bones.get(base_bone)): continue
        if len(pose_bone.bone.collections) > 0: continue

        base_collection.assign(pose_bone)
        pose_bone.color.palette = "THEME10"

    face_root_bones = ["faceAttach", "FACIAL_C_FacialRoot", "M_Head_A_jnt", "facial_master"]
    dynamic_bone_names = ["_jnt", "hair", "whiskers", "brow", "hat", "fins", "skirt", "bow", "chain", "ear", "beard"]
    for pose_bone in pose_bones:
        existing_collections = pose_bone.bone.collections
        if any(existing_collections, lambda col: col.name == "Sockets"):
            pose_bone.custom_shape = bpy.data.objects.get("CTRL_Socket")
            pose_bone.custom_shape_scale_xyz = (0.25, 0.25, 0.25)

        if len(existing_collections) > 0:
            continue

        has_vertex_group = pose_bone.color.palette != "THEME14"

        if "tail" in pose_bone.name.casefold() and "hair" not in pose_bone.name.casefold():
            tail_collection.assign(pose_bone)

            matches = re.findall(r'\d+', pose_bone.name)

            if (int(matches[-1]) if matches else 1) % 2 == 0:
                pose_bone.custom_shape = bpy.data.objects.get("CTRL_Deform")
                pose_bone.custom_shape_scale_xyz = (0.05, 0.05, 0.05)
                pose_bone.use_custom_shape_bone_size = False
            else:
                pose_bone.custom_shape = bpy.data.objects.get("CTRL_Twist")
                pose_bone.use_custom_shape_bone_size = False
                pose_bone.custom_shape_wire_width = 2.5
            pose_bone.color.palette = "THEME06"

            continue

        if "_fix_" in pose_bone.name.casefold() and has_vertex_group:
            deform_collection.assign(pose_bone)

            pose_bone.custom_shape = bpy.data.objects.get("CTRL_Deform")
            pose_bone.color.palette = "THEME07"
            pose_bone.custom_shape_scale_xyz = (0.25, 0.25, 0.25)

            continue

        if "twist_" in pose_bone.name and has_vertex_group:
            twist_collection.assign(pose_bone)

            pose_bone.custom_shape = bpy.data.objects.get("CTRL_Twist")
            pose_bone.color.palette = "THEME01" if pose_bone.name.endswith("_r") else "THEME04"
            pose_bone.use_custom_shape_bone_size = False
            pose_bone.custom_shape_wire_width = 2.5

            continue

        if any(pose_bone.bone.parent_recursive, lambda parent: parent.name.casefold() in [name.casefold() for name in face_root_bones]):
            pose_bone.custom_shape = bpy.data.objects.get("CTRL_Face")
            pose_bone.use_custom_shape_bone_size = False
            if not is_metahuman:
                pose_bone.custom_shape_scale_xyz = (3, 3, 3)
            
            if options.simplify_face_bones and any(extra_face_bones, lambda x: x == pose_bone.name):
                extra_face_collection.assign(pose_bone)
                pose_bone.color.palette = "THEME05"
            else:
                face_collection.assign(pose_bone)
                pose_bone.color.palette = "THEME02"
            continue

        if any(dynamic_bone_names, lambda x: x in pose_bone.name.casefold()):
            dynamic_collection.assign(pose_bone)

            if options.use_dynamic_bone_shape:
                pose_bone.custom_shape = bpy.data.objects.get("CTRL_Dynamic")
                pose_bone.custom_shape_translation = (0.0, 0.025, 0.0)
            pose_bone.color.palette = "THEME07"

            continue

        extra_collection.assign(pose_bone)

    bones = target_skeleton.data.bones

    for bone_name in hide_bones:
        if not (bone := bones.get(bone_name)): continue
        bone.hide = True

    driver_hide_bones = [
        ("ik_finger_thumb_r", dont_use_ik_finger_driver),
        ("ik_finger_index_r", dont_use_ik_finger_driver),
        ("ik_finger_middle_r", dont_use_ik_finger_driver),
        ("ik_finger_ring_r", dont_use_ik_finger_driver),
        ("ik_finger_pinky_r", dont_use_ik_finger_driver),
        ("ik_finger_pole_thumb_r", dont_use_ik_finger_driver),
        ("ik_finger_pole_index_r", dont_use_ik_finger_driver),
        ("ik_finger_pole_middle_r", dont_use_ik_finger_driver),
        ("ik_finger_pole_ring_r", dont_use_ik_finger_driver),
        ("ik_finger_pole_pinky_r", dont_use_ik_finger_driver),
        ("ik_hand_target_r", dont_use_ik_finger_driver),

        ("ik_finger_thumb_l", dont_use_ik_finger_driver),
        ("ik_finger_index_l", dont_use_ik_finger_driver),
        ("ik_finger_middle_l", dont_use_ik_finger_driver),
        ("ik_finger_ring_l", dont_use_ik_finger_driver),
        ("ik_finger_pinky_l", dont_use_ik_finger_driver),
        ("ik_finger_pole_thumb_l", dont_use_ik_finger_driver),
        ("ik_finger_pole_index_l", dont_use_ik_finger_driver),
        ("ik_finger_pole_middle_l", dont_use_ik_finger_driver),
        ("ik_finger_pole_ring_l", dont_use_ik_finger_driver),
        ("ik_finger_pole_pinky_l", dont_use_ik_finger_driver),
        ("ik_hand_target_l", dont_use_ik_finger_driver),

        ("ik_hand_pole_r", hide_pole_arm_driver),
        ("ik_hand_pole_l", hide_pole_arm_driver),
        ("ik_foot_pole_r", hide_pole_leg_driver),
        ("ik_foot_pole_l", hide_pole_leg_driver),

        ("ik_hand_r", hide_ik_arm_driver),
        ("ik_hand_l", hide_ik_arm_driver),
        ("ik_foot_r", hide_ik_leg_driver),
        ("ik_foot_l", hide_ik_leg_driver),

        ("thumb_control_r", use_ik_finger_driver),
        ("index_control_r", use_ik_finger_driver),
        ("middle_control_r", use_ik_finger_driver),
        ("ring_control_r", use_ik_finger_driver),
        ("pinky_control_r", use_ik_finger_driver),

        ("thumb_control_l", use_ik_finger_driver),
        ("index_control_l", use_ik_finger_driver),
        ("middle_control_l", use_ik_finger_driver),
        ("ring_control_l", use_ik_finger_driver),
        ("pinky_control_l", use_ik_finger_driver),
    ]

    for bone_name, driver in driver_hide_bones:
        if not (bone := bones.get(bone_name)): continue
        driver.add_to(bone, "hide")


    bpy.ops.object.mode_set(mode='OBJECT')
        
    