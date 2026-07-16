import traceback
import bpy

from ..material import *
from ..enums import *
from ..utils import *
from ...utils import *
from ...logger import Log

material_hash_cache = {}
material_name_cache = {}

class MaterialImportContext:
    def __init__(self):
        for key, mat in list(material_hash_cache.items()):
            if not mat.name:
                material_hash_cache.pop(key)

        for key, mat in list(material_name_cache.items()):
            if not mat.name:
                material_name_cache.pop(key)

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

        hash_key = hash_code(material_hash)

        if existing_material := material_hash_cache.get(hash_key):
            if not as_material_data:
                material_slot.material = existing_material
                return

        # same name but different hash
        if (name_existing := material_name_cache.get(material_name.casefold())) and name_existing.get("Hash") != hash_key:
            material_name += f"_{hash_key}"
            
        if not as_material_data and material_slot.material.name.casefold() != material_name.casefold():
            material_slot.material = bpy.data.materials.new(material_name)

        if not as_material_data:
            material_slot.material["Hash"] = hash_key
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
                index = data.get("Index", 0)
                texture_suffix = f"_Texture_{index + 1}" if index > 0 else ""
                spec_suffix = f"_{index + 1}" if index > 0 else ""
                replace_or_add_parameter_from_texture(textures, f"Diffuse{texture_suffix}", data.get("Diffuse"))
                replace_or_add_parameter_from_texture(textures, f"Normals{texture_suffix}", data.get("Normal"))
                replace_or_add_parameter_from_texture(textures, f"SpecularMasks{spec_suffix}", data.get("Specular"))

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
        ignore_textures = {n.casefold() for n in texture_ignore_names}

        def mapping_lookup(mapping_list, name):
            key = name.casefold()
            for mapping in mapping_list:
                if mapping.name.casefold() == key:
                    return mapping
            return None

        # parameter handlers
        def texture_param(data, target_mappings, target_node=shader_node, add_unused_params=False, mapped_lookup=None):
            node = None
            try:
                name = data.get("Name")
                path = get_texture_path(data)
                if not path:
                    return

                texture_name = path.split(".")[1]
                mappings = (mapped_lookup.get(name.casefold()) if mapped_lookup is not None
                            else mapping_lookup(target_mappings.textures, name))

                # Unmapped / ignored textures: never build nodes or load images.
                # Creating hundreds of empty TexImage nodes per hero material was
                # expensive and blew up Blender's depsgraph after import.
                if mappings is None or texture_name.casefold() in ignore_textures:
                    return

                node = nodes.new(type="ShaderNodeTexImage")
                node.image = self.import_image(path)
                if node.image is None:
                    nodes.remove(node)
                    return

                node.image.alpha_mode = 'CHANNEL_PACKED'
                node.image.colorspace_settings.name = "sRGB" if get_texture_srgb(data) else "Non-Color"
                node.interpolation = "Smart"
                node.hide = True

                x, y = get_socket_pos(target_node, target_node.inputs.find(mappings.slot))
                node.location = x - 300, y
                links.new(node.outputs[0], target_node.inputs[mappings.slot])

                if mappings.alpha_slot:
                    links.new(node.outputs[1], target_node.inputs[mappings.alpha_slot])
                if mappings.switch_slot:
                    target_node.inputs[mappings.switch_slot].default_value = 1
                if mappings.coords != "UV0":
                    uv = nodes.new(type="ShaderNodeUVMap")
                    uv.location = node.location.x - 250, node.location.y
                    uv.uv_map = mappings.coords
                    links.new(uv.outputs[0], node.inputs[0])
            except KeyError:
                if node is not None:
                    nodes.remove(node)
                pass
            except Exception:
                traceback.print_exc()

        def scalar_param(data, target_mappings, target_node=shader_node, add_unused_params=False, mapped_lookup=None):
            try:
                name = data.get("Name")
                value = data.get("Value")

                mappings = (mapped_lookup.get(name.casefold()) if mapped_lookup is not None
                            else mapping_lookup(target_mappings.scalars, name))
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

        def vector_param(data, target_mappings, target_node=shader_node, add_unused_params=False, mapped_lookup=None):
            try:
                name = data.get("Name")
                value = data.get("Value")

                mappings = (mapped_lookup.get(name.casefold()) if mapped_lookup is not None
                            else mapping_lookup(target_mappings.vectors, name))
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

        def component_mask_param(data, target_mappings, target_node=shader_node, add_unused_params=False, mapped_lookup=None):
            try:
                name = data.get("Name")
                value = data.get("Value")

                mappings = (mapped_lookup.get(name.casefold()) if mapped_lookup is not None
                            else mapping_lookup(target_mappings.component_masks, name))
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

        def switch_param(data, target_mappings, target_node=shader_node, add_unused_params=False, mapped_lookup=None):
            try:
                name = data.get("Name")
                value = data.get("Value")

                mappings = (mapped_lookup.get(name.casefold()) if mapped_lookup is not None
                            else mapping_lookup(target_mappings.switches, name))
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
            tex_lookup = {m.name.casefold(): m for m in mappings.textures}
            scalar_lookup = {m.name.casefold(): m for m in mappings.scalars}
            vector_lookup = {m.name.casefold(): m for m in mappings.vectors}
            mask_lookup = {m.name.casefold(): m for m in mappings.component_masks}
            switch_lookup = {m.name.casefold(): m for m in mappings.switches}

            for texture in textures:
                texture_param(texture, mappings, target_node, add_unused_params, tex_lookup)

            for scalar in scalars:
                scalar_param(scalar, mappings, target_node, add_unused_params, scalar_lookup)

            for vector in vectors:
                vector_param(vector, mappings, target_node, add_unused_params, vector_lookup)

            for component_mask in component_masks:
                component_mask_param(component_mask, mappings, target_node, add_unused_params, mask_lookup)

            for switch in switches:
                switch_param(switch, mappings, target_node, add_unused_params, switch_lookup)

        def move_texture_node(target_node, slot_name):
            if texture_node := get_node(shader_node, slot_name):
                x, y = get_socket_pos(target_node, target_node.inputs.find(slot_name))
                texture_node.location = x - 300, y
                links.new(texture_node.outputs[0], target_node.inputs[slot_name])
                links.new(target_node.outputs[slot_name], shader_node.inputs[slot_name])
                
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

        # TODO: Proper cape/two sided material handling
        if any(hero_master_names, lambda x: x in base_material_path):
            replace_shader_node("MR Hero")
            socket_mappings = hero_mappings

        if "Hair" in base_material_path:
            replace_shader_node("MR Hair")
            socket_mappings = hair_mappings

        # TODO: Come back to FakeEyeShadow, verify translucent coverage
        if "Translucent" in base_material_path or "FakeEyeShadow" in base_material_path:
            replace_shader_node("MR Translucent")
            socket_mappings = translucent_mappings

        if "Common_Eye" in base_material_path or "Eye_Opt" in base_material_path:
            replace_shader_node("MR Eye")
            socket_mappings = eye_mappings

        if any(eye_glass_master_names, lambda x: x in base_material_path) or (self.type == EExportType.OUTFIT and "SimpleGlass" in base_material_path):
            replace_shader_node("MR Eye Glass")
            socket_mappings = eye_glass_mappings

        if "RimOnly" in base_material_path:
            replace_shader_node("MR Rim")
        
        # TODO: Common_Cape, Symbiote (1035)
        # Cloak, Punisher

        setup_params(socket_mappings, shader_node, True)

        links.new(shader_node.outputs[0], output_node.inputs[0])

        material_hash_cache[hash_key] = material
        material_name_cache[material_name.casefold()] = material

        # post parameter handling
        
        if any(vertex_crunch_names, lambda x: x.lower() in material_name.lower()) or get_param(scalars, "HT_CrunchVerts") == 1 or any(toon_outline_names, lambda x: x in material_name):
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
                pre_eye_node.location = -600, -100
                setup_params(pre_eye_mappings, pre_eye_node, False)

                if node := get_node(shader_node, "ScleraBaseColor"):
                    links.new(pre_eye_node.outputs["Sclera UV"], node.inputs[0])
                else:
                    add_default_texture("T_EyeSclera_D", "sRGB", shader_node, "ScleraBaseColor", pre_eye_node, "Sclera UV")
                    
                if node := get_node(shader_node, "IrisBaseColor"):
                    links.new(pre_eye_node.outputs["Iris UV"], node.inputs[0])
                else:
                    add_default_texture("T_Common_Eyes_03_D", "sRGB", shader_node, "IrisBaseColor", pre_eye_node, "Iris UV")

                if node := get_node(shader_node, "IrisHeight"):
                    links.new(pre_eye_node.outputs["Iris UV"], node.inputs[0])
                else:
                    add_default_texture("T_Iris001_01_H", "Non-Color", shader_node, "IrisHeight", pre_eye_node, "Iris UV")

                if node := get_node(shader_node, "IrisBaseAO"):
                    links.new(pre_eye_node.outputs["Iris UV"], node.inputs[0])
                else:
                    add_default_texture("T_Iris001_01_AO", "sRGB", shader_node, "IrisBaseAO", pre_eye_node, "Iris UV")

                links.new(pre_eye_node.outputs["Sclera UV"], shader_node.inputs["Sclera UV"])
                links.new(pre_eye_node.outputs["Iris UV"], shader_node.inputs["Iris UV"])

                if diffuse_node := get_node(shader_node, "ScleraBaseColor"):
                    nodes.active = diffuse_node
            
            case "MR Eye Glass":
                pre_eye_glass_node = nodes.new(type="ShaderNodeGroup")
                pre_eye_glass_node.node_tree = bpy.data.node_groups.get("MR Pre Eye Glass")
                pre_eye_glass_node.location = -500, -75
                setup_params(pre_eye_glass_mappings, pre_eye_glass_node, False)

                if node := get_node(shader_node, "HighlightMask"):
                    links.new(pre_eye_glass_node.outputs["Highlight UV"], node.inputs[0])
                else:
                    add_default_texture("T_Common_EyesHighLight_01_M", "sRGB", shader_node, "HighlightMask", pre_eye_glass_node, "Highlight UV")

                if diffuse_node := get_node(shader_node, "HighlightMask"):
                    nodes.active = diffuse_node
            
            case "FP Layer":
                if diffuse_node := get_node(shader_node, "BaseColor"):
                    nodes.active = diffuse_node
            
            case "MR Hero":
                if diffuse_node := get_node(shader_node, "BaseColor"):
                    nodes.active = diffuse_node
                    
                if get_param(switches, "UseDyeing"):
                    dye_node = nodes.new(type="ShaderNodeGroup")
                    dye_node.node_tree = bpy.data.node_groups.get("MR ColorID Dye")
                    dye_node.location = -500, -75
                    setup_params(dye_mat_mappings, dye_node, False)
                    
                    move_texture_node(dye_node, "BaseColor")
                    
                    if diffuse_node := get_node(dye_node, "BaseColor"):
                        nodes.active = diffuse_node

            case "FP Layer":
                if diffuse_node := get_node(shader_node, "BaseColor"):
                    nodes.active = diffuse_node

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
                bpy.ops.mesh.primitive_cube_add()
                mat_mesh = bpy.context.active_object
                mat_mesh.name = name
                mat_mesh.data.materials.append(bpy.data.materials.new(name))
                self.import_material(mat_mesh.material_slots[material.get("Slot")], material, {})
            else:
                self.import_material(None, material, {}, True)