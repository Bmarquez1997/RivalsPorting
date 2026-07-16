from ...utils import *


def get_texture_path(texture_param):
    if texture_param is None:
        return None
    if value := texture_param.get("Value"):
        return value
    if tex := texture_param.get("Texture"):
        return tex.get("Path")
    return texture_param.get("Path")


def get_texture_srgb(texture_param, default=False):
    if texture_param is None:
        return default
    if "sRGB" in texture_param:
        return texture_param.get("sRGB")
    if tex := texture_param.get("Texture"):
        return tex.get("sRGB", default)
    return default


def texture_export_to_param(name, export_texture):
    if export_texture is None:
        return None
    if export_texture.get("Name"):
        return export_texture
    path = get_texture_path(export_texture)
    if path is None:
        return None
    return {
        "Name": name,
        "Value": path,
        "sRGB": get_texture_srgb(export_texture),
        "CompressionSettings": export_texture.get("CompressionSettings", 0),
    }


def replace_or_add_parameter_from_texture(target, name, export_texture):
    replace_or_add_parameter(target, texture_export_to_param(name, export_texture))


def get_param(source, name):
    found = first(source, lambda param: param.get("Name").casefold() == name.casefold())
    if found is None:
        return None
    return found.get("Value")


def get_vector_param(source, name):
    found = first(source, lambda param: param.get("Name").casefold() == name.casefold())
    if found is None:
        return None
    found_value = found.get("Value")
    return Vector((found_value.get('R'), found_value.get('G'), found_value.get('B')))


def get_param_multiple(source, names):
    found = first(source, lambda param: param.get("Name").casefold() in [name.casefold() for name in names])
    if found is None:
        return None
    return found.get("Value")


def get_param_info(source, name):
    found = first(source, lambda param: param.get("Name").casefold() == name.casefold())
    if found is None:
        return None
    return found


def get_params(source, names):
    return [info.get("Value") for info in where(source, lambda param: param.get("Name").casefold() in [name.casefold() for name in names])]


def get_socket_pos(node, index):
    start_y = -100
    offset_y = -25
    return node.location.x, node.location.y + start_y + offset_y * index


def replace_or_add_parameter(list, replace_item):
    if replace_item is None:
        return
    for index, item in enumerate(list):
        if item is None:
            continue

        if item.get("Name") == replace_item.get("Name"):
            list[index] = replace_item
            return

    list.append(replace_item)
        
        
def get_node(shader_node, name):
    input = shader_node.inputs.get(name)
    if input is None:
        return None
        
    links = input.links
    if links is None or len(links) == 0:
        return None

    return links[0].from_node
