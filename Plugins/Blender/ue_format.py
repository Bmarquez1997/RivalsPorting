import bpy
import bpy_extras
from bpy.props import StringProperty, BoolProperty, PointerProperty, EnumProperty, FloatProperty, FloatVectorProperty, CollectionProperty
from bpy.types import Scene

import struct
import os
import io
import time
import gzip
import json
from mathutils import Vector, Matrix, Quaternion, Euler
from math import *

try:
    import zstd
except ImportError:
    from pip._internal import main as pipmain
    pipmain(['install', 'zstd'])
    import zstd

# ---------- ADDON ---------- #

bl_info = {
    "name": "UE Format (.uemodel / .ueanim / .ueworld)",
    "author": "Half",
    "version": (1, 0, 0),
    "blender": (3, 1, 0),
    "location": "View3D > Sidebar > UE Format",
    "category": "Import",
}

class UFPanel(bpy.types.Panel):
    bl_category = "UE Format"
    bl_label = "UE Format"
    bl_region_type = 'UI'
    bl_space_type = 'VIEW_3D'
    
    def draw(self, context):
        UFPanel.draw_general_options(self, context)
        UFPanel.draw_model_options(self, context)
        UFPanel.draw_anim_options(self, context)
        UFPanel.draw_world_options(self, context)
        
    @staticmethod
    def draw_general_options(self, context):
        layout = self.layout

        box = layout.box()
        box.label(text="General", icon="SETTINGS")
        box.row().prop(bpy.context.scene.uf_settings, "scale")
       
    @staticmethod
    def draw_model_options(self, context, import_menu = False):
        layout = self.layout

        box = layout.box()
        box.label(text="Model", icon="OUTLINER_OB_MESH")
        box.row().prop(bpy.context.scene.uf_settings, "reorient_bones")
        box.row().prop(bpy.context.scene.uf_settings, "bone_length")
        
        if not import_menu:
            box.row().operator("uf.import_uemodel", icon='MESH_DATA')
        
    @staticmethod
    def draw_anim_options(self, context, import_menu = False):
        layout = self.layout

        box = layout.box()
        box.label(text="Animation", icon="ACTION")
        box.row().prop(bpy.context.scene.uf_settings, "rotation_only")
        
        if not import_menu:
            box.row().operator("uf.import_ueanim", icon='ANIM')
    @staticmethod
    def draw_world_options(self, context, import_menu = False):
        layout = self.layout

        box = layout.box()
        box.label(text="World", icon="WORLD")
        box.row().prop(bpy.context.scene.uf_settings, "instance_meshes")
        
        if not import_menu:
            box.row().operator("uf.import_ueworld", icon='SCENE_DATA')
        
class UFImportUEModel(bpy.types.Operator, bpy_extras.io_utils.ImportHelper):
    bl_idname = "uf.import_uemodel"
    bl_label = "Import Model"
    bl_context = 'scene'
    
    filename_ext = ".uemodel"
    filter_glob: StringProperty(default="*.uemodel", options={'HIDDEN'}, maxlen=255)
    files: CollectionProperty(type=bpy.types.OperatorFileListElement, options={'HIDDEN', 'SKIP_SAVE'})
    directory: StringProperty(subtype='DIR_PATH')
    
    def execute(self, context):
        for file in self.files:
            import_file(os.path.join(self.directory, file.name))
        return {'FINISHED'}

    def draw(self, context):
       UFPanel.draw_general_options(self, context)
       UFPanel.draw_model_options(self, context, True)
       
class UFImportUEAnim(bpy.types.Operator, bpy_extras.io_utils.ImportHelper):
    bl_idname = "uf.import_ueanim"
    bl_label = "Import Animation"
    bl_context = 'scene'
    
    filename_ext = ".ueanim"
    filter_glob: StringProperty(default="*.ueanim", options={'HIDDEN'}, maxlen=255)
    files: CollectionProperty(type=bpy.types.OperatorFileListElement, options={'HIDDEN', 'SKIP_SAVE'})
    directory: StringProperty(subtype='DIR_PATH')
    
    def execute(self, context):
        for file in self.files:
            import_file(os.path.join(self.directory, file.name))
        return {'FINISHED'}

    def draw(self, context):
       UFPanel.draw_general_options(self, context)
       UFPanel.draw_anim_options(self, context, True)
       
class UFImportUEWorld(bpy.types.Operator, bpy_extras.io_utils.ImportHelper):
    bl_idname = "uf.import_ueworld"
    bl_label = "Import World"
    bl_context = 'scene'
    
    filename_ext = ".ueworld"
    filter_glob: StringProperty(default="*.ueworld", options={'HIDDEN'}, maxlen=255)
    files: CollectionProperty(type=bpy.types.OperatorFileListElement, options={'HIDDEN', 'SKIP_SAVE'})
    directory: StringProperty(subtype='DIR_PATH')
    
    def execute(self, context):
        for file in self.files:
            import_file(os.path.join(self.directory, file.name))
        return {'FINISHED'}

    def draw(self, context):
       UFPanel.draw_general_options(self, context)
       UFPanel.draw_world_options(self, context, True)
    
class UFSettings(bpy.types.PropertyGroup):
    scale: FloatProperty(name="Scale", default=0.01, min = 0.01)
    bone_length: FloatProperty(name="Bone Length", default=5, min = 0.1)
    reorient_bones: BoolProperty(name="Reorient Bones", default=False)
    rotation_only: BoolProperty(name="Rotation Only", default=False)
    instance_meshes: BoolProperty(name="Instance Meshes", default=True)
    
def draw_import_menu(self, context):
    self.layout.operator(UFImportUEModel.bl_idname, text = "Unreal Model (.uemodel)")
    self.layout.operator(UFImportUEAnim.bl_idname, text = "Unreal Animation (.ueanim)")

operators = [UFPanel, UFImportUEModel, UFImportUEAnim, UFImportUEWorld, UFSettings]

def register():
    for operator in operators:
        bpy.utils.register_class(operator)
        
    Scene.uf_settings = PointerProperty(type=UFSettings)
    bpy.types.TOPBAR_MT_file_import.append(draw_import_menu)

def unregister():
    for operator in operators:
        bpy.utils.unregister_class(operator)
        
    del Scene.uf_settings
    bpy.types.TOPBAR_MT_file_import.remove(draw_import_menu)

if __name__ == "__main__":
    register()
    
# ---------- IMPORT CLASSES ---------- #

def bytes_to_str(in_bytes):
    return in_bytes.rstrip(b'\x00').decode()

def get_case_insensitive(source, string):
    for item in source:
        if item.name.lower() == string.lower():
            return item

def get_active_armature():
    obj = bpy.context.object
    if obj is None:
        return
    
    if obj.type == "ARMATURE":
        return obj
    elif obj.type == "MESH":
        for modifier in obj.modifiers:
            if modifier.type == "ARMATURE":
                return modifier.object

class Log:
    INFO = u"\u001b[36m"
    RESET = u"\u001b[0m"

    @staticmethod
    def info(message):
        print(f"{Log.INFO}[UEFORMAT] {Log.RESET}{message}")

class FArchiveReader:
    data = None
    size = 0

    def __init__(self, data):
        self.data = io.BytesIO(data)
        self.size = len(self.data.read())
        self.data.seek(0)

    def __enter__(self):
        self.size = len(self.data.read())
        self.data.seek(0)
        return self

    def __exit__(self, type, value, traceback):
        self.data.close()

    def eof(self):
        return self.data.tell() >= self.size

    def read(self, size: int):
        return self.data.read(size)

    def read_to_end(self):
        return self.data.read(self.size - self.data.tell())

    def read_bool(self):
        return struct.unpack("?", self.data.read(1))[0]

    def read_string(self, size: int):
        string =  self.data.read(size)
        return bytes_to_str(string)

    def read_fstring(self):
        size, = struct.unpack("i", self.data.read(4))
        string = self.data.read(size)
        return bytes_to_str(string)

    def read_int(self):
        return struct.unpack("i", self.data.read(4))[0]

    def read_int_vector(self, size: int):
        return struct.unpack(str(size) + "I", self.data.read(size*4))

    def read_short(self):
        return struct.unpack("h", self.data.read(2))[0]

    def read_byte(self):
        return struct.unpack("c", self.data.read(1))[0]

    def read_float(self):
        return struct.unpack("f", self.data.read(4))[0]

    def read_float_vector(self, size: int):
        return struct.unpack(str(size) + "f", self.data.read(size*4))

    def read_byte(self):
        return struct.unpack("c", self.data.read(1))

    def read_byte_vector(self, size: int):
        return struct.unpack(str(size) + "B", self.data.read(size))

    def skip(self, size: int):
        self.data.read(size)
        
    def read_bulk_array(self, predicate):
        count = self.read_int()
        return self.read_array(count, predicate)

    def read_array(self, count, predicate):
        array = []
        for counter in range(count):
            array.append(predicate(self))
        return array

class UEModel:
    vertices = []
    indices = []
    normals = []
    tangents = []
    colors = []
    uvs = []
    materials = []
    morphs = []
    weights = []
    bones = []
    sockets = []
    
class Material:
    material_name = ""
    first_index = -1
    num_faces = -1

    def __init__(self, ar: FArchiveReader):
        self.material_name = ar.read_fstring()
        self.first_index = ar.read_int()
        self.num_faces = ar.read_int()

class Bone:
    name = ""
    parent_index = -1
    position = []
    rotation = []

    def __init__(self, ar: FArchiveReader):
        self.name = ar.read_fstring()
        self.parent_index = ar.read_int()
        self.position = [pos * bpy.context.scene.uf_settings.scale for pos in ar.read_float_vector(3)]
        self.rotation = ar.read_float_vector(4)

class Weight:
    bone_index = -1
    vertex_index = -1
    weight = -1

    def __init__(self, ar: FArchiveReader):
        self.bone_index = ar.read_short()
        self.vertex_index = ar.read_int()
        self.weight = ar.read_float()

class MorphTarget:
    name = ""
    deltas = []

    def __init__(self, ar: FArchiveReader):
        self.name = ar.read_fstring()

        self.deltas = ar.read_bulk_array(lambda ar: MorphTargetData(ar))

class MorphTargetData:
    position = []
    normals = []
    vertex_index = -1

    def __init__(self, ar: FArchiveReader):
        self.position = [pos * bpy.context.scene.uf_settings.scale for pos in ar.read_float_vector(3)]
        self.normals = ar.read_float_vector(3)
        self.vertex_index = ar.read_int()

class Socket:
    name = ""
    parent_name = ""
    position = []
    rotation = []
    scale = []

    def __init__(self, ar: FArchiveReader):
        self.name = ar.read_fstring()
        self.parent_name = ar.read_fstring()
        self.position = [pos * bpy.context.scene.uf_settings.scale for pos in ar.read_float_vector(3)]
        self.rotation = ar.read_float_vector(4)
        self.scale = ar.read_float_vector(3)
        
class UEAnim:
    num_frames = 0
    frames_per_second = 0
    tracks = []
    curves = []
    
class Curve:
    name = ""
    keys = []

    def __init__(self, ar: FArchiveReader):
        self.name = ar.read_fstring()
        self.keys = ar.read_bulk_array(lambda ar: FloatKey(ar))
        
class Track:
    name = ""
    position_keys = []
    rotation_keys = []
    scale_keys = []

    def __init__(self, ar: FArchiveReader):
        self.name = ar.read_fstring()
        self.position_keys = ar.read_bulk_array(lambda ar: VectorKey(ar, bpy.context.scene.uf_settings.scale))
        self.rotation_keys = ar.read_bulk_array(lambda ar: QuatKey(ar))
        self.scale_keys = ar.read_bulk_array(lambda ar: VectorKey(ar))
        
class AnimKey:
    frame = -1

    def __init__(self, ar: FArchiveReader):
        self.frame = ar.read_int()
        
class VectorKey(AnimKey):
    value = []

    def __init__(self, ar: FArchiveReader, multiplier = 1):
        super().__init__(ar)
        self.value = [float * multiplier for float in ar.read_float_vector(3)]
        
    def get_vector(self):
        return Vector(self.value)
        
class QuatKey(AnimKey):
    value = []

    def __init__(self, ar: FArchiveReader):
        super().__init__(ar)
        self.value = ar.read_float_vector(4)
    
    def get_quat(self):
        return Quaternion((self.value[3], self.value[0], self.value[1], self.value[2]))
    
class FloatKey(AnimKey):
    value = 0.0

    def __init__(self, ar: FArchiveReader):
        super().__init__(ar)
        self.value = ar.read_float()
        
class UEWorld:
    meshes = []
    actors = []
    
class HashedMesh:
    hash_number = 0
    data = []

    def __init__(self, ar: FArchiveReader):
        self.hash_number = ar.read_int()

        data_size = ar.read_int()
        self.data = ar.read(data_size)
        
class Actor:
    mesh_hash = 0
    name = ""
    position = []
    rotation = []
    scale = []

    def __init__(self, ar: FArchiveReader):
        self.mesh_hash = ar.read_int()
        self.name = ar.read_fstring()
        self.position = [pos * bpy.context.scene.uf_settings.scale for pos in ar.read_float_vector(3)]
        self.rotation = ar.read_float_vector(3)
        self.scale = ar.read_float_vector(3)
        
# ---------- IMPORT FUNCTIONS ---------- #

MAGIC = "UEFORMAT"
MODEL_IDENTIFIER = "UEMODEL"
ANIM_IDENTIFIER = "UEANIM"
WORLD_IDENTIFIER = "UEWORLD"

def import_file(path: str):
    with open(path, 'rb') as file:
        return import_data(file.read())

def import_data(data, link_model: bool = True):
    with FArchiveReader(data) as ar:
        magic = ar.read_string(len(MAGIC))
        if magic != MAGIC:
            return

        identifier = ar.read_fstring()
        file_version = ar.read_byte()
        object_name = ar.read_fstring()
        Log.info(f"Importing {object_name}")

        read_archive = ar
        is_compressed = ar.read_bool()
        if is_compressed:
            compression_type = ar.read_fstring()
            uncompressed_size = ar.read_int()
            compressed_size = ar.read_int()

            if compression_type == "GZIP":
                read_archive = FArchiveReader(gzip.decompress(ar.read_to_end()))
            elif compression_type == "ZSTD":
                read_archive = FArchiveReader(zstd.ZSTD_uncompress(ar.read_to_end()))
            else:
                Log.info(f"Unknown Compression Type: {compression_type}")
                return


        if identifier == MODEL_IDENTIFIER:
            return import_uemodel_data(read_archive, object_name, link_model)
        elif identifier == ANIM_IDENTIFIER:
            return import_ueanim_data(read_archive, object_name)
        elif identifier == WORLD_IDENTIFIER:
            return import_ueworld_data(read_archive, object_name)
        
def import_uemodel_data(ar: FArchiveReader, name: str, link: bool):
    data = UEModel()

    while not ar.eof():
        header_name = ar.read_fstring()
        array_size = ar.read_int()
        byte_size = ar.read_int()
        if header_name == "VERTICES":
            data.vertices = ar.read_array(array_size, lambda ar: [vert * bpy.context.scene.uf_settings.scale for vert in ar.read_float_vector(3)])
        elif header_name == "INDICES":
            data.indices = ar.read_array(int(array_size / 3), lambda ar: ar.read_int_vector(3))
        elif header_name == "NORMALS":
            data.normals = ar.read_array(array_size, lambda ar: ar.read_float_vector(3))
        elif header_name == "TANGENTS":
            data.tangents = ar.read_array(array_size, lambda ar: ar.read_float_vector(3))
        elif header_name == "VERTEXCOLORS":
            data.colors = ar.read_array(array_size, lambda ar: ar.read_byte_vector(4))
        elif header_name == "TEXCOORDS":
            data.uvs = ar.read_array(array_size, lambda ar: ar.read_array(ar.read_int(), lambda ar: ar.read_float_vector(2)))
        elif header_name == "MATERIALS":
            data.materials = ar.read_array(array_size, lambda ar: Material(ar))
        elif header_name == "WEIGHTS":
            data.weights = ar.read_array(array_size, lambda ar: Weight(ar))
        elif header_name == "BONES":
            data.bones = ar.read_array(array_size, lambda ar: Bone(ar))
        elif header_name == "MORPHTARGETS":
            data.morphs = ar.read_array(array_size, lambda ar: MorphTarget(ar))
        elif header_name == "SOCKETS":
            data.sockets = ar.read_array(array_size, lambda ar: Socket(ar))
        else:
            ar.skip(byte_size)

    # geometry
    mesh_data = bpy.data.meshes.new(name)
    mesh_data.from_pydata(data.vertices, [], data.indices)

    mesh_object = bpy.data.objects.new(name, mesh_data)
    return_object = mesh_object
    if link:
        bpy.context.collection.objects.link(mesh_object)

    # normals
    if len(data.normals) > 0:
        mesh_data.polygons.foreach_set("use_smooth", [True] * len(mesh_data.polygons))
        mesh_data.normals_split_custom_set_from_vertices(data.normals)
        if bpy.app.version < (4, 1, 0):
            mesh_data.use_auto_smooth = True

    # weights
    if len(data.weights) > 0 and len(data.bones) > 0:
        for weight in data.weights:
            bone_name = data.bones[weight.bone_index].name
            vertex_group = mesh_object.vertex_groups.get(bone_name)
            if not vertex_group:
                vertex_group = mesh_object.vertex_groups.new(name = bone_name)
            vertex_group.add([weight.vertex_index], weight.weight, 'ADD')

    # morph targets
    if len(data.morphs) > 0:
        default_key = mesh_object.shape_key_add(from_mix=False)
        default_key.name = "Default"
        default_key.interpolation = 'KEY_LINEAR'

        for morph in data.morphs:
            key = mesh_object.shape_key_add(from_mix=False)
            key.name = morph.name
            key.interpolation = 'KEY_LINEAR'

            for delta in morph.deltas:
                key.data[delta.vertex_index].co += Vector(delta.position)
                
    # vertex colors
    if len(data.colors) > 0:
        vertex_color = mesh_data.color_attributes.new(domain='CORNER', type='BYTE_COLOR', name="COL0")
        for polygon in mesh_data.polygons:
            for vertex_index, loop_index in zip(polygon.vertices, polygon.loop_indices):
                color = data.colors[vertex_index]
                vertex_color.data[loop_index].color = color[0] / 255, color[1] / 255, color[2] / 255, color[3] / 255

    # texture coordinates
    if len(data.uvs) > 0:
        for index, uvs in enumerate(data.uvs):
            uv_layer = mesh_data.uv_layers.new(name="UV" + str(index))
            for polygon in mesh_data.polygons:
                for vertex_index, loop_index in zip(polygon.vertices, polygon.loop_indices):
                    uv_layer.data[loop_index].uv = uvs[vertex_index]

    # materials
    if len(data.materials) > 0:
        for i, material in enumerate(data.materials):
            mat = bpy.data.materials.get(material.material_name)
            if mat is None:
                mat = bpy.data.materials.new(name=material.material_name)
            mesh_data.materials.append(mat)

            start_face_index = (material.first_index // 3)
            end_face_index = start_face_index + material.num_faces
            for face_index in range(start_face_index, end_face_index):
                mesh_data.polygons[face_index].material_index = i

    # skeleton
    if len(data.bones) > 0 or len(data.sockets) > 0:
        armature_data = bpy.data.armatures.new(name=name)
        armature_data.display_type = 'STICK'

        armature_object = bpy.data.objects.new(name + "_Skeleton", armature_data)
        armature_object.show_in_front = True
        return_object = armature_object

        if link:
            bpy.context.collection.objects.link(armature_object)
        bpy.context.view_layer.objects.active = armature_object
        armature_object.select_set(True)

        mesh_object.parent = armature_object
                
    if len(data.bones) > 0:
        # create bones
        bpy.ops.object.mode_set(mode='EDIT')
        edit_bones = armature_data.edit_bones
        for bone in data.bones:
            bone_pos = Vector(bone.position)
            bone_rot = Quaternion((bone.rotation[3], bone.rotation[0], bone.rotation[1], bone.rotation[2])) # xyzw -> wxyz

            edit_bone = edit_bones.new(bone.name)
            edit_bone.length = bpy.context.scene.uf_settings.bone_length * bpy.context.scene.uf_settings.scale

            bone_matrix = Matrix.Translation(bone_pos) @ bone_rot.to_matrix().to_4x4()

            if bone.parent_index >= 0:
                parent_bone = edit_bones.get(data.bones[bone.parent_index].name)
                edit_bone.parent = parent_bone
                bone_matrix = parent_bone.matrix @ bone_matrix
    
            edit_bone.matrix = bone_matrix
            
        bpy.ops.object.mode_set(mode='OBJECT')

        # armature modifier
        armature_modifier = mesh_object.modifiers.new(armature_object.name, type='ARMATURE')
        armature_modifier.show_expanded = False
        armature_modifier.use_vertex_groups = True
        armature_modifier.object = armature_object

        # bone colors
        for bone in armature_object.pose.bones:
            if mesh_object.vertex_groups.get(bone.name) is None:
                bone.color.palette = 'THEME14'
                continue
                
            if len(bone.children) == 0:
                bone.color.palette = 'THEME03'

    # sockets
    if len(data.sockets) > 0:
        # create sockets
        bpy.ops.object.mode_set(mode='EDIT')
        for socket in data.sockets:
            socket_bone = edit_bones.new(socket.name)
            parent_bone = get_case_insensitive(edit_bones, socket.parent_name)
            if parent_bone is None:
                continue
            socket_bone.parent = parent_bone
            socket_bone.length = bpy.context.scene.uf_settings.bone_length * bpy.context.scene.uf_settings.scale
            socket_bone.matrix = parent_bone.matrix @ Matrix.Translation(socket.position) @ Quaternion((socket.rotation[3], socket.rotation[0], socket.rotation[1], socket.rotation[2])).to_matrix().to_4x4() # xyzw -> wxyz

        bpy.ops.object.mode_set(mode='OBJECT')

        # socket colors
        for socket in data.sockets:
            socket_bone = armature_object.pose.bones.get(socket.name)
            if socket_bone is not None: 
                socket_bone.color.palette = 'THEME05'

    return return_object

def import_ueanim_data(ar: FArchiveReader, name: str):
    data = UEAnim()
    
    data.num_frames = ar.read_int()
    data.frames_per_second = ar.read_float()

    while not ar.eof():
        header_name = ar.read_fstring()
        array_size = ar.read_int()
        byte_size = ar.read_int()

        if header_name == "TRACKS":
            data.tracks = ar.read_array(array_size, lambda ar: Track(ar))
        elif header_name == "CURVES":
            data.curves = ar.read_array(array_size, lambda ar: Curve(ar))
        else:
            ar.skip(byte_size)
    
    armature = get_active_armature()
    
    action = bpy.data.actions.new(name = name)
    armature.animation_data_create()
    armature.animation_data.action = action
    
    # bone anim data
    pose_bones = armature.pose.bones
    for track in data.tracks:
        bone = get_case_insensitive(pose_bones, track.name)
        if bone is None:
            continue
        
        def create_fcurves(name, count, key_count):
            path = bone.path_from_id(name)
            curves = []
            for i in range(count):
                curve = action.fcurves.new(path, index = i)
                curve.keyframe_points.add(key_count)
                curves.append(curve)
            return curves
        
        def add_key(curves, vector, key_index, frame):
            for i in range(len(vector)):
                curves[i].keyframe_points[key_index].co = frame, vector[i]
                curves[i].keyframe_points[key_index].interpolation = "LINEAR"
        
        if not bpy.context.scene.uf_settings.rotation_only:
            loc_curves = create_fcurves("location", 3, len(track.position_keys))
            scale_curves = create_fcurves("scale", 3, len(track.scale_keys))
        rot_curves = create_fcurves("rotation_quaternion", 4, len(track.rotation_keys))
        
        if not bpy.context.scene.uf_settings.rotation_only:
            for index, key in enumerate(track.position_keys):
                pos = key.get_vector()
                if bone.parent is None:
                    bone.matrix.translation = pos
                else:
                    bone.matrix.translation = bone.parent.matrix @ pos
                add_key(loc_curves, bone.location, index, key.frame)
                
            for index, key in enumerate(track.scale_keys):
                add_key(scale_curves, key.value, index, key.frame)
            
        for index, key in enumerate(track.rotation_keys):
            rot = key.get_quat()
            if bone.parent is None:
                bone.rotation_quaternion = rot
            else:
                bone.matrix = bone.parent.matrix @ rot.to_matrix().to_4x4()
            add_key(rot_curves, bone.rotation_quaternion, index, key.frame)
        
        bone.matrix_basis.identity()

def import_ueworld_data(ar: FArchiveReader, name: str):
    data = UEWorld()

    while not ar.eof():
        header_name = ar.read_fstring()
        array_size = ar.read_int()
        byte_size = ar.read_int()

        if header_name == "MESHES":
            data.meshes = ar.read_array(array_size, lambda ar: HashedMesh(ar))
        elif header_name == "ACTORS":
            data.actors = ar.read_array(array_size, lambda ar: Actor(ar))
        else:
            ar.skip(byte_size)
            
    mesh_map = {}
    for mesh in data.meshes:
        mesh_map[mesh.hash_number] = import_data(mesh.data, False)
    
    for actor in data.actors:
        mesh = mesh_map[actor.mesh_hash]
        mesh_data = mesh.data if bpy.context.scene.uf_settings.instance_meshes else mesh.data.copy()
        obj = bpy.data.objects.new(actor.name, mesh_data)
        obj.location = actor.position
        obj.rotation_mode = 'XYZ'
        obj.rotation_euler = [radians(actor.rotation[2]), radians(actor.rotation[0]), radians(actor.rotation[1])]
        obj.scale = actor.scale
        bpy.context.scene.collection.objects.link(obj)