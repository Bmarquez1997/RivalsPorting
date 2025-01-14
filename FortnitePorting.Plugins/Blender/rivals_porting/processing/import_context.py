import json
import traceback
import pyperclip

import bpy
import traceback
from math import radians
from .mappings import *
from .material import *
from .enums import *
from .utils import *
from .tasty import *
from ..utils import *
from ..logger import Log
from ...io_scene_ueformat.importer.logic import UEFormatImport
from ...io_scene_ueformat.options import UEModelOptions, UEAnimOptions

class ImportContext:

    def __init__(self, meta_data):
        self.options = meta_data.get("Settings")
        self.assets_root = meta_data.get("AssetsRoot")

    def run(self, data):
        self.name = data.get("Name")
        self.type = EExportType(data.get("Type"))
        self.scale = 0.01 if self.options.get("ScaleDown") else 1
        self.meshes = []
        self.override_materials = []
        self.override_parameters = []
        self.imported_meshes = []
        self.full_vertex_crunch_materials = []
        self.partial_vertex_crunch_materials = {}
        self.add_toon_outline = False

        if bpy.context.mode != "OBJECT":
            bpy.ops.object.mode_set(mode='OBJECT')

        ensure_blend_data()

        pyperclip.copy(json.dumps(data))

        import_type = EPrimitiveExportType(data.get("PrimitiveType"))
        match import_type:
            case EPrimitiveExportType.MESH:
                self.import_mesh_data(data)
            case EPrimitiveExportType.ANIMATION:
                self.import_anim_data(data)
                pass
            case EPrimitiveExportType.TEXTURE:
                self.import_texture_data(data)
                pass
            case EPrimitiveExportType.SOUND:
                self.import_sound_data(data)
                pass
            case EPrimitiveExportType.FONT:
                self.import_font_data(data)
                pass
            case EPrimitiveExportType.POSE_ASSET:
                self.import_pose_asset_data(data, get_selected_armature(), None)
                pass
            case EPrimitiveExportType.MATERIAL:
                self.import_material_standalone(data)
                pass

    def import_mesh_data(self, data):
        rig_type = ERigType(self.options.get("RigType"))
        
        if rig_type == ERigType.TASTY:
            self.options["MergeArmatures"] = True
            self.options["ReorientBones"] = True
        
        self.override_materials = data.get("OverrideMaterials")
        self.override_parameters = data.get("OverrideParameters")
        
        self.collection = create_or_get_collection(self.name) if self.options.get("ImportIntoCollection") else bpy.context.scene.collection

        if self.type in [EExportType.OUTFIT, EExportType.BACKPACK, EExportType.PICKAXE, EExportType.FALL_GUYS_OUTFIT]:
            target_meshes = data.get("OverrideMeshes")
            normal_meshes = data.get("Meshes")
            for mesh in normal_meshes:
                if not any(target_meshes, lambda target_mesh: target_mesh.get("Type") == mesh.get("Type")):
                    target_meshes.append(mesh)
        else:
            target_meshes = data.get("Meshes")

        self.meshes = target_meshes
        for mesh in target_meshes:
            self.import_model(mesh, can_spawn_at_3d_cursor=True)

        self.import_light_data(data.get("Lights"))
            
        if self.type in [EExportType.OUTFIT, EExportType.FALL_GUYS_OUTFIT] and self.options.get("MergeArmatures"):
            master_skeleton = get_selected_armature()
            master_mesh = get_armature_mesh(master_skeleton)
            
            for material, elements in self.partial_vertex_crunch_materials.items():
                vertex_crunch_modifier = master_mesh.modifiers.new("FP Vertex Crunch", type="NODES")
                vertex_crunch_modifier.node_group = bpy.data.node_groups.get("FP Vertex Crunch")

                set_geo_nodes_param(vertex_crunch_modifier, "Material", material)
                for name, value in elements.items():
                    set_geo_nodes_param(vertex_crunch_modifier, name, value == 1)
                    
            for material in self.full_vertex_crunch_materials:
                vertex_crunch_modifier = master_mesh.modifiers.new("FP Full Vertex Crunch", type="NODES")
                vertex_crunch_modifier.node_group = bpy.data.node_groups.get("FP Full Vertex Crunch")
                set_geo_nodes_param(vertex_crunch_modifier, "Material", material)

            if self.add_toon_outline:
                master_mesh.data.materials.append(bpy.data.materials.get("M_FP_Outline"))

                solidify = master_mesh.modifiers.new(name="Outline", type='SOLIDIFY')
                solidify.thickness = 0.001
                solidify.offset = 1
                solidify.thickness_clamp = 5.0
                solidify.use_rim = False
                solidify.use_flip_normals = True
                solidify.material_offset = len(master_mesh.data.materials) - 1
                
            if rig_type == ERigType.TASTY:
                create_tasty_rig(self, master_skeleton, TastyRigOptions(scale=self.scale, use_dynamic_bone_shape=self.options.get("UseDynamicBoneShape")))

            if anim_data := data.get("Animation"):
                self.import_anim_data(anim_data, master_skeleton)

    def gather_metadata(self, *search_props):
        out_props = {}
        for mesh in self.meshes:
            meta = mesh.get("Meta")
            if meta is None:
                continue

            for search_prop in search_props:
                if found_key := first(meta.keys(), lambda key: key == search_prop):
                    if out_props.get(found_key):
                        if meta.get(found_key):
                            Log.warn(f"{found_key}: metadata already set "
                                     "with content from different mesh but "
                                     f"also found on {mesh.get('Name')} "
                                     "which will be ignored")
                        continue
                    out_props[found_key] = meta.get(found_key)
        return out_props

    def get_metadata(self, search_prop):
        for mesh in self.meshes:
            meta = mesh.get("Meta")
            if meta is None:
                continue

            if found_key := first(meta.keys(), lambda key: key == search_prop):
                return meta.get(found_key)
        return None

    def import_model(self, mesh, parent=None, can_reorient=True, can_spawn_at_3d_cursor=False):
        path = mesh.get("Path")
        name = mesh.get("Name")
        part_type = EFortCustomPartType(mesh.get("Type"))
        num_lods = mesh.get("NumLods")
        
        if mesh.get("IsEmpty"):
            empty_object = bpy.data.objects.new(name, None)

            empty_object.parent = parent
            empty_object.rotation_euler = make_euler(mesh.get("Rotation"))
            empty_object.location = make_vector(mesh.get("Location"), unreal_coords_correction=True) * self.scale
            empty_object.scale = make_vector(mesh.get("Scale"))
            
            self.collection.objects.link(empty_object)
            
            for child in mesh.get("Children"):
                self.import_model(child, parent=empty_object)
                
            return
        
        if self.type in [EExportType.PREFAB, EExportType.WORLD] and mesh in self.meshes:
            Log.info(f"Importing Actor: {name} {self.meshes.index(mesh)} / {len(self.meshes)}")

        mesh_name = path.split(".")[1]
        if self.type in [EExportType.PREFAB, EExportType.WORLD] and (existing_mesh_data := bpy.data.meshes.get(mesh_name + "_LOD0")):
            imported_object = bpy.data.objects.new(name, existing_mesh_data)
            self.collection.objects.link(imported_object)
            
            imported_mesh = get_armature_mesh(imported_object)
        else:
            imported_object = self.import_mesh(path, can_reorient=can_reorient)
            if imported_object is None:
                Log.warn(f"Import failed for object at path: {path}")
                return imported_object
            imported_object.name = name

            imported_mesh = get_armature_mesh(imported_object)

            if EPolygonType(self.options.get("PolygonType")) == EPolygonType.QUADS and imported_mesh is not None:
                bpy.context.view_layer.objects.active = imported_mesh
                bpy.ops.object.mode_set(mode='EDIT')
                bpy.ops.mesh.tris_convert_to_quads(uvs=True)
                bpy.ops.object.mode_set(mode='OBJECT')
                bpy.context.view_layer.objects.active = imported_object

        if (override_vertex_colors := mesh.get("OverrideVertexColors")) and len(override_vertex_colors) > 0:
            imported_mesh.data = imported_mesh.data.copy()

            vertex_color = imported_mesh.data.color_attributes.new(
                domain="CORNER",
                type="BYTE_COLOR",
                name="INSTCOL0",
            )

            color_data = []
            for col in override_vertex_colors:
                color_data.append((col["R"], col["G"], col["B"], col["A"]))

            for polygon in imported_mesh.data.polygons:
                for vertex_index, loop_index in zip(polygon.vertices, polygon.loop_indices):
                    if vertex_index >= len(color_data):
                        continue
                        
                    color = color_data[vertex_index]
                    vertex_color.data[loop_index].color = color[0] / 255, color[1] / 255, color[2] / 255, color[3] / 255

        imported_object.parent = parent
        imported_object.rotation_euler = make_euler(mesh.get("Rotation"))
        imported_object.location = make_vector(mesh.get("Location"), unreal_coords_correction=True) * self.scale
        imported_object.scale = make_vector(mesh.get("Scale"))
        
        if self.options.get("ImportAt3DCursor") and can_spawn_at_3d_cursor:
            imported_object.location += bpy.context.scene.cursor.location

        self.imported_meshes.append({
            "Skeleton": imported_object,
            "Mesh": imported_mesh,
            "Type": part_type,
            "Meta": mesh.get("Meta")
        })

        # metadata handling
        # todo extract meta reading to function bc this is too big
        meta = self.gather_metadata("PoseData", "CurveTrackNames")

        # pose asset
        if imported_mesh is not None:
            bpy.context.view_layer.objects.active = imported_mesh
            self.import_pose_asset_data(meta, get_selected_armature(), part_type)

        # end

        match part_type:
            case EFortCustomPartType.BODY:
                meta.update(self.gather_metadata("SkinColor"))
            case EFortCustomPartType.HEAD:
                meta.update(self.gather_metadata("MorphNames", "HatType"))
                meta["IsHead"] = True
                shape_keys = imported_mesh.data.shape_keys
                if (morphs := meta.get("MorphNames")) and (morph_name := morphs.get(meta.get("HatType"))) and shape_keys is not None:
                    for key in shape_keys.key_blocks:
                        if key.name.casefold() == morph_name.casefold():
                            key.value = 1.0

        meta["TextureData"] = mesh.get("TextureData")
        
        for material in mesh.get("Materials"):
            index = material.get("Slot")
            if index >= len(imported_mesh.material_slots):
                continue

            self.import_material(imported_mesh.material_slots[index], material, meta)

        for override_material in mesh.get("OverrideMaterials"):
            index = override_material.get("Slot")
            if index >= len(imported_mesh.material_slots):
                continue

            overridden_material = imported_mesh.material_slots[index]
            slots = where(imported_mesh.material_slots,
                          lambda slot: slot.name == overridden_material.name)
            for slot in slots:
                self.import_material(slot, override_material, meta)

        for variant_override_material in self.override_materials:
            material_name_to_swap = variant_override_material.get("MaterialNameToSwap")
            
            slots = where(imported_mesh.material_slots,
                          lambda slot: slot.material.get("OriginalName") == material_name_to_swap)
            for slot in slots:
                self.import_material(slot, variant_override_material.get("Material"), meta)
                
        self.import_light_data(mesh.get("Lights"), imported_object)

        for child in mesh.get("Children"):
            self.import_model(child, parent=imported_object)
            
        instances = mesh.get("Instances")
        if len(instances) > 0:
            mesh_data = imported_mesh.data
            imported_object.select_set(True)
            bpy.ops.object.delete()
            
            instance_parent = bpy.data.objects.new("InstanceParent_" + name, None)
            instance_parent.parent = parent
            instance_parent.rotation_euler = make_euler(mesh.get("Rotation"))
            instance_parent.location = make_vector(mesh.get("Location"), unreal_coords_correction=True) * self.scale
            instance_parent.scale = make_vector(mesh.get("Scale"))
            bpy.context.collection.objects.link(instance_parent)
            
            for instance_index, instance_transform in enumerate(instances):
                instance_name = f"Instance_{instance_index}_" + name
                
                Log.info(f"Importing Instance: {instance_name} {instance_index} / {len(instances)}")
                
                instance_object = bpy.data.objects.new(f"Instance_{instance_index}_" + name, mesh_data)
                self.collection.objects.link(instance_object)
    
                instance_object.parent = instance_parent
                instance_object.rotation_euler = make_euler(instance_transform.get("Rotation"))
                instance_object.location = make_vector(instance_transform.get("Location"), unreal_coords_correction=True) * self.scale
                instance_object.scale = make_vector(instance_transform.get("Scale"))
            
        return imported_object
    
    def import_light_data(self, lights, parent=None):
        if not lights:
            return
        
        for point_light in lights.get("PointLights"):
            self.import_point_light(point_light, parent)
    
    def import_point_light(self, point_light, parent=None):
        name = point_light.get("Name")
        light_data = bpy.data.lights.new(name=name, type='POINT')
        light = bpy.data.objects.new(name=name, object_data=light_data)
        self.collection.objects.link(light)
        
        light.parent = parent
        light.rotation_euler = make_euler(point_light.get("Rotation"))
        light.location = make_vector(point_light.get("Location"), unreal_coords_correction=True) * self.scale
        light.scale = make_vector(point_light.get("Scale"))
        
        color = point_light.get("Color")
        light_data.color = (color["R"], color["G"], color["B"])
        light_data.energy = point_light.get("Intensity")
        light_data.use_custom_distance = True
        light_data.cutoff_distance = point_light.get("AttenuationRadius") * self.scale
        light_data.shadow_soft_size = point_light.get("Radius") * self.scale
        light_data.use_shadow = point_light.get("CastShadows")

    def import_mesh(self, path: str, can_reorient=True):
        options = UEModelOptions(scale_factor=self.scale,
                                 reorient_bones=self.options.get("ReorientBones") and can_reorient,
                                 bone_length=self.options.get("BoneLength"),
                                 import_sockets=self.options.get("ImportSockets"),
                                 import_virtual_bones=self.options.get("ImportVirtualBones"),
                                 import_collision=self.options.get("ImportCollision"),
                                 target_lod=self.options.get("TargetLOD"),
                                 allowed_reorient_children=allowed_reorient_children)

        path = path[1:] if path.startswith("/") else path

        mesh_path = os.path.join(self.assets_root, path.split(".")[0] + ".uemodel")

        return UEFormatImport(options).import_file(mesh_path)
    
    def import_texture_data(self, data):
        import_method = ETextureImportMethod(self.options.get("TextureImportMethod"))

        for path in data.get("Textures"):
            image = self.import_image(path)
            
            if import_method == ETextureImportMethod.OBJECT:
                bpy.ops.mesh.primitive_plane_add()
                plane = bpy.context.active_object
                plane.name = image.name
                plane.scale.x = image.size[0] / image.size[1]
    
                material = bpy.data.materials.new(image.name)
                material.use_nodes = True
                material.surface_render_method = "BLENDED"
                
                nodes = material.node_tree.nodes
                nodes.clear()
                links = material.node_tree.links
                links.clear()
                
                output_node = nodes.new(type="ShaderNodeOutputMaterial")
                output_node.location = (300, 0)
                
                image_node = nodes.new(type="ShaderNodeTexImage")
                image_node.image = image
                links.new(image_node.outputs[0], output_node.inputs[0])
                
                plane.data.materials.append(material)

    def format_image_path(self, path: str):
        path, name = path.split(".")
        path = path[1:] if path.startswith("/") else path
        
        ext = ""
        match EImageFormat(self.options.get("ImageFormat")):
            case EImageFormat.PNG:
                ext = "png"
            case EImageFormat.TGA:
                ext = "tga"
        
        texture_path = os.path.join(self.assets_root, path + "." + ext)
        return texture_path, name

    def import_image(self, path: str):
        path, name = self.format_image_path(path)
        if existing := bpy.data.images.get(name):
            return existing

        if not os.path.exists(path):
            return None

        return bpy.data.images.load(path, check_existing=True)

    def import_material(self, material_slot, material_data, meta, as_material_data=False):

        # object ref mat slots for instancing
        if not as_material_data:
            temp_material = material_slot.material
            material_slot.link = 'OBJECT' if self.type in [EExportType.WORLD, EExportType.PREFAB] else 'DATA'
            material_slot.material = temp_material

        material_name = material_data.get("Name")
        material_hash = material_data.get("Hash")
        additional_hash = 0

        texture_data = meta.get("TextureData")
        if texture_data is not None:
            for data in texture_data:
                additional_hash += data.get("Hash")
        
        override_parameters = where(self.override_parameters, lambda param: param.get("MaterialNameToAlter") in [material_name, "Global"])
        if override_parameters is not None:
            for parameters in override_parameters:
                additional_hash += parameters.get("Hash")

        if additional_hash != 0:
            material_hash += additional_hash
            material_name += f"_{hash_code(material_hash)}"
            
        if existing_material := first(bpy.data.materials, lambda mat: mat.get("Hash") == hash_code(material_hash)):
            if not as_material_data:
                material_slot.material = existing_material
                return

        # same name but different hash
        if (name_existing := first(bpy.data.materials, lambda mat: mat.name == material_name)) and name_existing.get("Hash") != material_hash:
            material_name += f"_{hash_code(material_hash)}"
            
        if not as_material_data and material_slot.material.name.casefold() != material_name.casefold():
            material_slot.material = bpy.data.materials.new(material_name)

        if not as_material_data:
            material_slot.material["Hash"] = hash_code(material_hash)
            material_slot.material["OriginalName"] = material_data.get("Name")

        material = bpy.data.materials.new(material_name) if as_material_data else material_slot.material
        material.use_nodes = True
        material.surface_render_method = "DITHERED"

        nodes = material.node_tree.nodes
        nodes.clear()
        links = material.node_tree.links
        links.clear()

        override_blend_mode = EBlendMode(material_data.get("OverrideBlendMode"))
        base_blend_mode = EBlendMode(material_data.get("BaseBlendMode"))
        translucency_lighting_mode = ETranslucencyLightingMode(material_data.get("TranslucencyLightingMode"))
        shading_model = EMaterialShadingModel(material_data.get("ShadingModel"))
        
        textures = material_data.get("Textures")
        scalars = material_data.get("Scalars")
        vectors = material_data.get("Vectors")
        switches = material_data.get("Switches")
        component_masks = material_data.get("ComponentMasks")

        if texture_data is not None:
            for data in texture_data:
                replace_or_add_parameter(textures, data.get("Diffuse"))
                replace_or_add_parameter(textures, data.get("Normal"))
                replace_or_add_parameter(textures, data.get("Specular"))

        if override_parameters is not None:
            for parameters in override_parameters:
                for texture in parameters.get("Textures"):
                    replace_or_add_parameter(textures, texture)
    
                for scalar in parameters.get("Scalars"):
                    replace_or_add_parameter(scalars, scalar)
    
                for vector in parameters.get("Vectors"):
                    replace_or_add_parameter(vectors, vector)

        output_node = nodes.new(type="ShaderNodeOutputMaterial")
        output_node.location = (200, 0)

        shader_node = nodes.new(type="ShaderNodeGroup")
        shader_node.node_tree = bpy.data.node_groups.get("MR Material Lite")

        def replace_shader_node(name):
            nonlocal shader_node
            nodes.remove(shader_node)
            shader_node = nodes.new(type="ShaderNodeGroup")
            shader_node.node_tree = bpy.data.node_groups.get(name)
            
        # for cleaner code sometimes bc stuff gets repetitive
        def set_param(name, value, override_shader=None):
            
            nonlocal shader_node
            target_node = override_shader or shader_node
            target_node.inputs[name].default_value = value

        def get_node(target_node, slot):
            node_links = target_node.inputs[slot].links
            if node_links is None or len(node_links) == 0:
                return None
            
            return node_links[0].from_node

        unused_parameter_height = 0

        # parameter handlers
        def texture_param(data, target_mappings, target_node=shader_node, add_unused_params=False):
            try:
                name = data.get("Name")
                path = data.get("Value")
                texture_name = path.split(".")[1]

                node = nodes.new(type="ShaderNodeTexImage")
                node.image = self.import_image(path)
                node.image.alpha_mode = 'CHANNEL_PACKED'
                node.image.colorspace_settings.name = "sRGB" if data.get("sRGB") else "Non-Color"
                node.interpolation = "Smart"
                node.hide = True

                mappings = first(target_mappings.textures, lambda x: x.name.casefold() == name.casefold())
                if mappings is None or texture_name in texture_ignore_names:
                    if add_unused_params:
                        nonlocal unused_parameter_height
                        node.label = name
                        node.location = 400, unused_parameter_height
                        unused_parameter_height -= 50
                    else:
                        nodes.remove(node)
                    return

                x, y = get_socket_pos(target_node, target_node.inputs.find(mappings.slot))
                node.location = x - 300, y
                links.new(node.outputs[0], target_node.inputs[mappings.slot])

                if mappings.alpha_slot:
                    links.new(node.outputs[1], target_node.inputs[mappings.alpha_slot])
                if mappings.switch_slot:
                    target_node.inputs[mappings.switch_slot].default_value = 1 if value else 0
                if mappings.coords != "UV0":
                    uv = nodes.new(type="ShaderNodeUVMap")
                    uv.location = node.location.x - 250, node.location.y
                    uv.uv_map = mappings.coords
                    links.new(uv.outputs[0], node.inputs[0])
            except KeyError:
                nodes.remove(node)
                pass
            except Exception:
                traceback.print_exc()

        def scalar_param(data, target_mappings, target_node=shader_node, add_unused_params=False):
            try:
                name = data.get("Name")
                value = data.get("Value")

                mappings = first(target_mappings.scalars, lambda x: x.name.casefold() == name.casefold())
                if mappings is None:
                    if add_unused_params:
                        nonlocal unused_parameter_height
                        node = nodes.new(type="ShaderNodeValue")
                        node.outputs[0].default_value = value
                        node.label = name
                        node.width = 250
                        node.location = 400, unused_parameter_height
                        unused_parameter_height -= 100
                    return

                value = mappings.value_func(value) if mappings.value_func else value
                target_socket = target_node.inputs[mappings.slot]

                match target_socket.type:
                    case "INT":
                        target_socket.default_value = int(value)
                    case "BOOL":
                        target_socket.default_value = int(value) == 1
                    case _:
                        target_socket.default_value = value
                    
                if mappings.switch_slot:
                    target_node.inputs[mappings.switch_slot].default_value = 1 if value else 0
            except KeyError as e:
                pass
            except Exception:
                traceback.print_exc()

        def vector_param(data, target_mappings, target_node=shader_node, add_unused_params=False):
            try:
                name = data.get("Name")
                value = data.get("Value")

                mappings = first(target_mappings.vectors, lambda x: x.name.casefold() == name.casefold())
                if mappings is None:
                    if add_unused_params:
                        nonlocal unused_parameter_height
                        node = nodes.new(type="ShaderNodeRGB")
                        node.outputs[0].default_value = (value["R"], value["G"], value["B"], value["A"])
                        node.label = name
                        node.width = 250
                        node.location = 400, unused_parameter_height
                        unused_parameter_height -= 200
                    return

                value = mappings.value_func(value) if mappings.value_func else value
                target_node.inputs[mappings.slot].default_value = (value["R"], value["G"], value["B"], 1.0)
                if mappings.alpha_slot:
                    target_node.inputs[mappings.alpha_slot].default_value = value["A"]
                if mappings.switch_slot:
                    target_node.inputs[mappings.switch_slot].default_value = 1 if value else 0
            except KeyError:
                pass
            except Exception:
                traceback.print_exc()

        def component_mask_param(data, target_mappings, target_node=shader_node, add_unused_params=False):
            try:
                name = data.get("Name")
                value = data.get("Value")

                mappings = first(target_mappings.component_masks, lambda x: x.name.casefold() == name.casefold())
                if mappings is None:
                    if add_unused_params:
                        nonlocal unused_parameter_height
                        node = nodes.new(type="ShaderNodeRGB")
                        node.outputs[0].default_value = (value["R"], value["G"], value["B"], value["A"])
                        node.label = name
                        node.width = 250
                        node.location = 400, unused_parameter_height
                        unused_parameter_height -= 200
                    return

                value = mappings.value_func(value) if mappings.value_func else value
                target_node.inputs[mappings.slot].default_value = (value["R"], value["G"], value["B"], value["A"])
            except KeyError:
                pass
            except Exception:
                traceback.print_exc()

        def switch_param(data, target_mappings, target_node=shader_node, add_unused_params=False):
            try:
                name = data.get("Name")
                value = data.get("Value")

                mappings = first(target_mappings.switches, lambda x: x.name.casefold() == name.casefold())
                if mappings is None:
                    if add_unused_params:
                        nonlocal unused_parameter_height
                        node = nodes.new("ShaderNodeGroup")
                        node.node_tree = bpy.data.node_groups.get("FP Switch")
                        node.inputs[0].default_value = 1 if value else 0
                        node.label = name
                        node.width = 250
                        node.location = 400, unused_parameter_height
                        unused_parameter_height -= 125
                    return

                value = mappings.value_func(value) if mappings.value_func else value
                target_socket = target_node.inputs[mappings.slot]
                match target_socket.type:
                    case "INT":
                        target_socket.default_value = 1 if value else 0
                    case "BOOL":
                        target_socket.default_value = value
            except KeyError:
                pass
            except Exception:
                traceback.print_exc()

        def setup_params(mappings, target_node, add_unused_params=False):
            for texture in textures:
                texture_param(texture, mappings, target_node, add_unused_params)

            for scalar in scalars:
                scalar_param(scalar, mappings, target_node, add_unused_params)

            for vector in vectors:
                vector_param(vector, mappings, target_node, add_unused_params)

            for component_mask in component_masks:
                component_mask_param(component_mask, mappings, target_node, add_unused_params)

            for switch in switches:
                switch_param(switch, mappings, target_node, add_unused_params)
        
        def add_default_texture(texture_name, color_space, target_node, target_slot, pre_node=None, pre_slot=None):
            default_texture_node = nodes.new(type="ShaderNodeTexImage")
            default_texture_node.image = bpy.data.images.get(texture_name)
            default_texture_node.image.alpha_mode = 'CHANNEL_PACKED'
            default_texture_node.image.colorspace_settings.name = color_space
            default_texture_node.interpolation = "Smart"
            default_texture_node.hide = True

            x, y = get_socket_pos(shader_node, shader_node.inputs.find(target_slot))
            default_texture_node.location = x - 300, y
            links.new(default_texture_node.outputs[0], target_node.inputs[target_slot])

            if pre_node is not None:
                links.new(pre_node.outputs[pre_slot], default_texture_node.inputs[0])

        # decide which material type and mappings to use
        socket_mappings = default_mappings
        base_material_path = material_data.get("BaseMaterialPath")

        if get_param_multiple(switches, layer_switch_names) and get_param_multiple(textures, extra_layer_names):
            replace_shader_node("FP Layer")
            socket_mappings = layer_mappings

            set_param("Is Transparent", override_blend_mode is not EBlendMode.BLEND_Opaque)

        is_glass = material_data.get("PhysMaterialName") == "Glass" or any(glass_master_names, lambda x: x in base_material_path) or (base_blend_mode is EBlendMode.BLEND_Translucent and translucency_lighting_mode in [ETranslucencyLightingMode.TLM_SurfacePerPixelLighting, ETranslucencyLightingMode.TLM_VolumetricPerVertexDirectional])
        if is_glass:
            replace_shader_node("FP Glass")
            socket_mappings = glass_mappings

            material.surface_render_method = "BLENDED"
            material.show_transparent_back = False

        if "Common_Body" in base_material_path or "Common_Skin" in base_material_path or (self.type == EExportType.OUTFIT and "Body" in base_material_path):
            replace_shader_node("MR Hero")
            socket_mappings = hero_mappings

        if "Common_Hair" in base_material_path:
            replace_shader_node("MR Hair")
            socket_mappings = hair_mappings

        # TODO: Come back to FakeEyeShadow
        if "Common_Translucent" in base_material_path or "FakeEyeShadow" in base_material_path:
            replace_shader_node("MR Translucent")
            socket_mappings = translucent_mappings

        if "Common_Eye" in base_material_path:
            replace_shader_node("MR Eye")
            socket_mappings = eye_mappings

        if "EyeHighlight" in base_material_path or "EyesHighLight" in base_material_path:
            replace_shader_node("MR Eye Glass")
            socket_mappings = eye_glass_mappings

        if "RimOnly" in base_material_path:
            replace_shader_node("MR Rim")

        setup_params(socket_mappings, shader_node, True)

        links.new(shader_node.outputs[0], output_node.inputs[0])

        # post parameter handling
        
        if material_name in vertex_crunch_names or get_param(scalars, "HT_CrunchVerts") == 1 or any(toon_outline_names, lambda x: x in material_name):
            self.full_vertex_crunch_materials.append(material)
            return

        match shader_node.node_tree.name:
            case "FP Material":
                set_param("AO", self.options.get("AmbientOcclusion"))
                set_param("Cavity", self.options.get("Cavity"))
                set_param("Subsurface", self.options.get("Subsurface"))
                    
                if diffuse_node := get_node(shader_node, "BaseColor"):
                    nodes.active = diffuse_node

            case "FP Glass":
                mask_slot = shader_node.inputs["Mask"]
                if len(mask_slot.links) > 0 and get_param(switches, "Use Diffuse Texture for Color [ignores alpha channel]"):
                    links.remove(mask_slot.links[0])

                if color_node := get_node(shader_node, "Color"):
                    nodes.active = color_node
                
            case "FP Toon":
                set_param("Brightness", self.options.get("ToonShadingBrightness"))
                self.add_toon_outline = True
            
            case "MR Eye":
                pre_eye_node = nodes.new(type="ShaderNodeGroup")
                pre_eye_node.node_tree = bpy.data.node_groups.get("MR Pre Eye")
                pre_eye_node.location = -600, 100
                setup_params(pre_eye_mappings, pre_eye_node, False)

                if node := get_node(shader_node, "ScleraBaseColor"):
                    links.new(pre_eye_node.outputs["Sclera UV"], node.inputs[0])
                else:
                    add_default_texture("T_EyeSclera_D", "SRGB", shader_node, "ScleraBaseColor", pre_eye_node, "Sclera UV")
                    
                if node := get_node(shader_node, "IrisBaseColor") is None:
                    links.new(pre_eye_node.outputs["Iris UV"], node.inputs[0])
                else:
                    add_default_texture("T_Common_Eyes_03_D", "SRGB", shader_node, "IrisBaseColor", pre_eye_node, "Iris UV")

                if node := get_node(shader_node, "IrisHeight") is None:
                    links.new(pre_eye_node.outputs["Iris UV"], node.inputs[0])
                else:
                    add_default_texture("T_Iris001_01_H", "Non-Color", shader_node, "IrisHeight", pre_eye_node, "Iris UV")

                if node := get_node(shader_node, "IrisBaseAO") is None:
                    links.new(pre_eye_node.outputs["Iris UV"], node.inputs[0])
                else:
                    add_default_texture("T_Iris001_01_AO", "SRGB", shader_node, "IrisBaseAO", pre_eye_node, "Iris UV")

                links.new(pre_eye_node.outputs["Sclera UV"], shader_node.inputs["Sclera UV"])
                links.new(pre_eye_node.outputs["Iris UV"], shader_node.inputs["Iris UV"])

                if diffuse_node := get_node(shader_node, "ScleraBaseColor"):
                    nodes.active = diffuse_node
            
            case "MR Eye Glass":
                pre_eye_glass_node = nodes.new(type="ShaderNodeGroup")
                pre_eye_glass_node.node_tree = bpy.data.node_groups.get("MR Pre Eye Glass")
                pre_eye_glass_node.location = -600, 0
                setup_params(pre_eye_glass_mappings, pre_eye_glass_node, False)

                if node := get_node(shader_node, "HighlightMask"):
                    links.new(pre_eye_glass_node.outputs["Highlight UV"], node.inputs[0])
                else:
                    add_default_texture("T_Common_EyesHighLight_01_M", "SRGB", shader_node, "HighlightMask", pre_eye_glass_node, "Highlight UV")

                if diffuse_node := get_node(shader_node, "HighlightMask"):
                    nodes.active = diffuse_node
            
            case "FP Layer":
                if diffuse_node := get_node(shader_node, "BaseColor"):
                    nodes.active = diffuse_node

    def import_sound_data(self, data):
        for sound in data.get("Sounds"):
            path = sound.get("Path")
            self.import_sound(path, time_to_frame(sound.get("Time")))

    def import_sound(self, path: str, time):
        path = path[1:] if path.startswith("/") else path
        file_path, name = path.split(".")
        if existing := bpy.data.sounds.get(name):
            return existing

        if not bpy.context.scene.sequence_editor:
            bpy.context.scene.sequence_editor_create()

        ext = ESoundFormat(self.options.get("SoundFormat")).name.lower()
        sound_path = os.path.join(self.assets_root, f"{file_path}.{ext}")
        sound = bpy.context.scene.sequence_editor.sequences.new_sound(name, sound_path, 0, time)
        sound["FPSound"] = True
        return sound
    
    def import_anim_data(self, data, override_skeleton=None):
        target_skeleton = override_skeleton or get_selected_armature()
        bpy.context.view_layer.objects.active = target_skeleton
        
        if target_skeleton is None:
            # TODO message server
            #MessageServer.instance.send("An armature must be selected to import an animation. Please select an armature and try again.")
            return

        if target_skeleton.get("is_tasty"):
            bpy.ops.object.mode_set(mode="POSE")
            
            if ik_finger_toggle := target_skeleton.pose.bones.get("finger_toggle"):
                ik_finger_toggle.location[0] = TOGGLE_OFF

            if world_space_pole_toggle := target_skeleton.pose.bones.get("pole_toggle"):
                world_space_pole_toggle.location[0] = TOGGLE_OFF
                
            bpy.ops.object.mode_set(mode="OBJECT")
            

        # clear old data
        if anim_data := target_skeleton.animation_data:
           anim_data.action = None
           
           for track in anim_data.nla_tracks:
               anim_data.nla_tracks.remove(track)
        else:
            target_skeleton.animation_data_create()

        active_mesh = get_armature_mesh(target_skeleton)
        if active_mesh is not None and active_mesh.data.shape_keys is not None:
            active_mesh.data.shape_keys.name = "Pose Asset Controls"
            
            if shape_key_anim_data := active_mesh.data.shape_keys.animation_data:
                shape_key_anim_data.action = None
                for track in shape_key_anim_data.nla_tracks:
                    shape_key_anim_data.nla_tracks.remove(track)
            else:
                active_mesh.data.shape_keys.animation_data_create()
            
        if bpy.context.scene.sequence_editor:
            sequences_to_remove = where(bpy.context.scene.sequence_editor.sequences, lambda seq: seq.get("FPSound"))
            for sequence in sequences_to_remove:
                bpy.context.scene.sequence_editor.sequences.remove(sequence)

        bpy.context.scene.frame_set(0)

        # start import
        target_track = target_skeleton.animation_data.nla_tracks.new(prev=None)
        target_track.name = "Sections"

        if active_mesh.data.shape_keys is not None:
            mesh_track = active_mesh.data.shape_keys.animation_data.nla_tracks.new(prev=None)
            mesh_track.name = "Sections"

        def import_sections(sections, skeleton, track, is_main_skeleton = False):
            total_frames = 0
            is_metahuman = any(skeleton.data.bones, lambda bone: bone.name == "FACIAL_C_FacialRoot")
            for section in sections:
                path = section.get("Path")

                total_frames += time_to_frame(section.get("Length"))

                anim = self.import_anim(path, skeleton)
                clear_children_bone_transforms(skeleton, anim, "faceAttach")

                section_name = section.get("Name")
                time_offset = section.get("Time")
                loop_count = 999 if self.options.get("LoopAnimation") and section.get("Loop") else 1
                frame = time_to_frame(time_offset)

                if len(track.strips) > 0 and frame < track.strips[-1].frame_end:
                    frame = int(track.strips[-1].frame_end)

                strip = track.strips.new(section_name, frame, anim)
                strip.repeat = loop_count

                if (curves := section.get("Curves")) and len(curves) > 0 and active_mesh.data.shape_keys is not None and is_main_skeleton:
                    key_blocks = active_mesh.data.shape_keys.key_blocks
                    for key_block in key_blocks:
                        key_block.value = 0

                    for curve in curves:
                        curve_name = curve.get("Name")
                        if target_block := key_blocks.get(curve_name.replace("CTRL_expressions_", "")):
                            for key in curve.get("Keys"):
                                target_block.value = key.get("Value")
                                target_block.keyframe_insert(data_path="value", frame=key.get("Time") * 30)

                        if is_metahuman and (curve_mappings := metahuman_mappings.get(curve_name)):
                            for curve_mapping in curve_mappings:
                                if target_block := key_blocks.get(curve_mapping.replace("CTRL_expressions_", "")):
                                    for key in curve.get("Keys"):
                                        target_block.value = key.get("Value")
                                        target_block.keyframe_insert(data_path="value", frame=key.get("Time") * 30)

                    if active_mesh.data.shape_keys.animation_data.action is not None:
                        strip = mesh_track.strips.new(section_name, frame, active_mesh.data.shape_keys.animation_data.action)
                        strip.name = section_name
                        strip.repeat = loop_count
                        active_mesh.data.shape_keys.animation_data.action = None
            return total_frames

        total_frames = import_sections(data.get("Sections"), target_skeleton, target_track, True)
        if self.options.get("UpdateTimelineLength"):
            bpy.context.scene.frame_end = total_frames

        props = data.get("Props")
        if len(props) > 0:
            if master_skeleton := first(target_skeleton.children, lambda child: child.name == "Master_Skeleton"):
                bpy.data.objects.remove(master_skeleton)

            master_skeleton = self.import_model(data.get("Skeleton"), can_reorient=False)
            master_skeleton.name = "Master_Skeleton"
            master_skeleton.parent = target_skeleton
            master_skeleton.animation_data_create()

            master_track = master_skeleton.animation_data.nla_tracks.new(prev=None)
            master_track.name = "Sections"

            import_sections(data.get("Sections"), master_skeleton, master_track)

            for prop in props:
                mesh = self.import_model(prop.get("Mesh"))
                constraint_object(mesh, master_skeleton, prop.get("SocketName"), [0, 0, 0])
                mesh.rotation_euler = make_euler(prop.get("RotationOffset"))
                mesh.location = make_vector(prop.get("LocationOffset"), unreal_coords_correction=True) * 0.01
                mesh.scale = make_vector(prop.get("Scale"))

                if (anims := prop.get("AnimSections")) and len(anims) > 0:
                    mesh.animation_data_create()
                    mesh_track = mesh.animation_data.nla_tracks.new(prev=None)
                    mesh_track.name = "Sections"
                    import_sections(anims, mesh, mesh_track)

            master_skeleton.hide_set(True)

        if self.options.get("ImportSounds"):
            for sound in data.get("Sounds"):
                path = sound.get("Path")
                self.import_sound(path, time_to_frame(sound.get("Time")))

    def import_anim(self, path: str, override_skeleton=None):
        path = path[1:] if path.startswith("/") else path
        file_path, name = path.split(".")
        if (existing := bpy.data.actions.get(name)) and existing["Skeleton"] == override_skeleton.name:
            return existing

        anim_path = os.path.join(self.assets_root, file_path + ".ueanim")
        options = UEAnimOptions(link=False,
                                override_skeleton=override_skeleton,
                                scale_factor=self.scale)
        anim = UEFormatImport(options).import_file(anim_path)
        anim["Skeleton"] = override_skeleton.name
        return anim
    
    def import_font_data(self, data):
        self.import_font(data.get("Path"))
        
    def import_font(self, path: str):
        path = path[1:] if path.startswith("/") else path
        file_path, name = path.split(".")
        font_path = os.path.join(self.assets_root, file_path + ".ttf")
        bpy.ops.font.open(filepath=font_path, check_existing=True)
        
    def import_pose_asset_data(self, data, selected_armature, part_type):
        pose_data = data.get("PoseData")
        if not pose_data:
            return

        tracks = data.get("CurveTrackNames")

        original_selected_object = bpy.context.active_object
        if selected_armature is None:
            return

        selected_mesh: bpy.types.Object = get_armature_mesh(selected_armature)

        shape_keys = selected_mesh.data.shape_keys
        original_shape_key_lock = selected_mesh.show_only_shape_key
        original_mode = bpy.context.active_object.mode
        muted_constraints = []
        try:
            bpy.ops.object.mode_set(mode="OBJECT")
            armature_modifier: bpy.types.ArmatureModifier = first(
                selected_mesh.modifiers, lambda mod: mod.type == "ARMATURE"
            )

            # Turn off shape key lock if it's on otherwise shape keys fail to
            # import.
            selected_mesh.show_only_shape_key = False

            # Temporarily mutate all bone constraints since pose assets
            # are bone based and we don't want constraints to influence
            # the final pose.
            muted_constraints = disable_constraints(selected_armature)

            # Swap parents where applicable
            bone_swap_orig_parents(selected_armature)

            if not shape_keys:
                # Create Basis shape key
                selected_mesh.shape_key_add(name="Basis", from_mix=False)

            root_bone_name = "neck_01"
            root_bone = get_case_insensitive(
                selected_armature.pose.bones, root_bone_name
            )
            if not root_bone:
                Log.warn(
                    f"{selected_mesh.name}: Failed to find root bone "
                    f"'{root_bone_name}' in '{selected_armature.name}', all "
                    "bones will be considered during import of "
                    "PoseData"
                )

            loc_scale = self.scale
            pose_names = []
            for pose in pose_data:
                if not (pose_name := pose.get("Name")):
                    Log.warn(
                        f"{selected_mesh.name}: skipping pose data "
                        f"with no pose name:\n{pose}"
                    )
                    continue
                pose_names.append(pose_name)

                # If there are no influences, don't bother
                if not (influences := pose.get("Keys")):
                    continue

                # Enter pose mode
                bpy.context.view_layer.objects.active = selected_armature
                bpy.ops.object.mode_set(mode="POSE")

                # Reset all transforms to default
                bpy.ops.pose.select_all(action="SELECT")
                bpy.ops.pose.transforms_clear()
                bpy.ops.pose.select_all(action="DESELECT")

                # Move bones accordingly
                contributed = False
                for bone in influences:
                    if not (bone_name := bone.get("Name")):
                        Log.warn(
                            f"{selected_mesh.name} - {pose_name}: "
                            f"empty bone name for pose:\n{pose}"
                        )
                        continue

                    pose_bone: bpy.types.PoseBone = get_case_insensitive(
                        selected_armature.pose.bones, bone_name
                    )
                    if not pose_bone:
                        # For cases where pose data tries to move a non-existent bone
                        # i.e. Poseidon has no 'Tongue' but it's in the pose asset
                        if not part_type or part_type is EFortCustomPartType.HEAD:
                            # There are likely many missing bones in non-Head parts, but we
                            # process as many as we can.
                            Log.warn(
                                f"{selected_mesh.name} - {pose_name}: "
                                f"'{bone_name}' influence skipped "
                                "since it was not found in "
                                f"'{selected_armature.name}'"
                            )
                        continue

                    if root_bone and not bone_has_parent(pose_bone, root_bone):
                        Log.warn(
                            f"{selected_mesh.name} - {pose_name}: "
                            f"skipped '{pose_bone.name}' since it does "
                            f"not have '{root_bone.name}' as a parent"
                        )
                        continue

                    # Verify that the current bone and all of its children
                    # have at least one vertex group associated with it
                    if not bone_hierarchy_has_vertex_groups(
                        pose_bone, selected_mesh.vertex_groups
                    ):
                        continue

                    # Reset bone to identity
                    pose_bone.matrix_basis.identity()

                    rotation = bone.get("Rotation")
                    if not rotation.get("IsNormalized"):
                        Log.warn(
                            f"{selected_mesh.name} - {pose_name}: "
                            f"rotation not normalized for '{bone_name}' in "
                            "pose"
                        )

                    edit_bone = pose_bone.bone
                    post_quat = (
                        Quaternion(post_quat)
                        if (post_quat := edit_bone.get("post_quat"))
                        else Quaternion()
                    )

                    q = post_quat.copy()
                    q.rotate(make_quat(rotation))
                    quat = post_quat.copy()
                    quat.rotate(q.conjugated())
                    pose_bone.rotation_quaternion = (
                        quat.conjugated() @ pose_bone.rotation_quaternion
                    )

                    loc = make_vector(
                        bone.get("Location"), unreal_coords_correction=True
                    )
                    loc.rotate(post_quat.conjugated())

                    pose_bone.location = pose_bone.location + loc * loc_scale
                    pose_bone.scale = Vector((1, 1, 1)) + make_vector(bone.get("Scale"))

                    pose_bone.rotation_quaternion.normalize()
                    contributed = True

                # Do not create shape keys if nothing changed
                if not contributed:
                    continue

                # Create blendshape from armature
                bpy.ops.object.mode_set(mode="OBJECT")
                bpy.context.view_layer.objects.active = selected_mesh
                selected_mesh.select_set(True)
                bpy.ops.object.modifier_apply_as_shapekey(
                    keep_modifier=True, modifier=armature_modifier.name
                )

                # Use name from pose data
                selected_mesh.data.shape_keys.key_blocks[-1].name = pose_name

            if not tracks:
                return

            bpy.ops.object.mode_set(mode="OBJECT")
            bpy.context.view_layer.objects.active = selected_mesh
            selected_mesh.select_set(True)

            # Now that base blendshapes are imported, cycle through all
            # PoseData again to create blendshapes based on CurveData.
            for pose in pose_data:
                if not (curves := pose.get("CurveData")):
                    continue

                if not (pose_name := pose.get("Name")):
                    Log.warn(
                        f"{selected_mesh.name} - {pose_name}: skipping pose "
                        f"data from curve data with no pose name:\n{pose}"
                    )
                    continue

                # Not sure what it means when there's curve data on a pose
                # also containing bone transforms. So if there's an existing
                # shape key, just prepend curves_ to distinguish it.
                # Also, if it exists in the original set of shape keys but it
                # failed to import for some reason (i.e. missing bone), also
                # distinguish that name to prevent confusion since the name
                # of that key may not do what the user expects
                # (i.e. tongue_up_pose on Fish Thicc created from CurveData).
                # If we import outside the context of a full character import
                # (i.e. part_type is None), then only prepend curves_ for
                # existing shape keys.
                if pose_name in selected_mesh.data.shape_keys.key_blocks or \
                   (part_type is EFortCustomPartType.HEAD and pose_name in pose_names):
                    pose_name = f"curves_{pose_name}"

                # Verify length of CurveData matches that of tracks
                if len(curves) != len(tracks):
                    Log.warn(
                        f"{selected_mesh.name} - {pose_name}: skipped since "
                        "length of curve data for pose does not match the "
                        "length of Tracks array"
                    )
                    continue

                # If all curve values are basically 0, skip
                if all(curves, lambda curve_value: abs(curve_value) < 0.00001):
                    continue

                contributed = False
                for track_idx, curve_value in enumerate(curves):
                    # Sometimes curve_value is a very small number (1.7881586e-06).
                    # Probably best to just normalize it to 0
                    if abs(curve_value) < 0.00001:
                        curve_value = 0.0

                    # Even if the curve_value is zero, let it be set anyway
                    # since this is essentially a free reset to zero for the
                    # relevant shape keys.
                    shape_key_name = tracks[track_idx]
                    if shape_key := selected_mesh.data.shape_keys.key_blocks.get(
                        shape_key_name
                    ):
                        # Influence above / below 1.0 is possible with curve data
                        if curve_value < shape_key.slider_min:
                            shape_key.slider_min = curve_value - 1.0
                        if curve_value > shape_key.slider_max:
                            shape_key.slider_max = curve_value + 1.0
                        shape_key.value = curve_value
                        contributed = True
                    else:
                        if not part_type or part_type is EFortCustomPartType.HEAD:
                            Log.warn(
                                f"{selected_mesh.name} - {pose_name}: did not "
                                "apply influence to missing shape key: "
                                f"'{shape_key_name}'"
                            )

                # Do not create shape keys if nothing changed
                if not contributed:
                    continue

                selected_mesh.shape_key_add(name=pose_name, from_mix=True)

            # Set all shape keys in the track back to 0
            for shape_key_name in tracks:
                if shape_key := selected_mesh.data.shape_keys.key_blocks.get(
                    shape_key_name
                ):
                    shape_key.slider_min = 0.0
                    shape_key.slider_max = 1.0
                    shape_key.value = 0.0
        except Exception as e:
            Log.error(f"Failed to import PoseAsset data from {selected_mesh.name}: {e}")
        finally:
            # Final reset before re-entering regular import mode.
            bpy.context.view_layer.objects.active = selected_armature
            bpy.ops.object.mode_set(mode="POSE")
            bpy.ops.pose.select_all(action="SELECT")
            bpy.ops.pose.transforms_clear()
            bpy.ops.pose.select_all(action="DESELECT")

            bone_swap_orig_parents(selected_armature)
            for constraint in muted_constraints:
                constraint.mute = False

            selected_mesh.show_only_shape_key = original_shape_key_lock
            bpy.ops.object.mode_set(mode=original_mode)
            bpy.context.view_layer.objects.active = original_selected_object

    def import_material_standalone(self, data):
        is_object_import = EMaterialImportMethod.OBJECT == EMaterialImportMethod(self.options.get("MaterialImportMethod"))
        materials = data.get("Materials")

        if materials is None:
            return
        
        if is_object_import:
            self.collection = create_or_get_collection("Materials") if self.options.get("ImportIntoCollection") else bpy.context.scene.collection
            
        for material in materials:
            name = material.get("Name")
            Log.info(f"Importing Material: {name}")
            if is_object_import:
                bpy.ops.mesh.primitive_ico_sphere_add()
                mat_mesh = bpy.context.active_object
                mat_mesh.name = name
                mat_mesh.data.materials.append(bpy.data.materials.new(name))
                self.import_material(mat_mesh.material_slots[material.get("Slot")], material, {})
            else:
                self.import_material(None, material, {}, True)