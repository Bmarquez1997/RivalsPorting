class MappingCollection:
    def __init__(self, textures=(), scalars=(), vectors=(), switches=(), component_masks=()):
        self.textures = textures
        self.scalars = scalars
        self.vectors = vectors
        self.switches = switches
        self.component_masks = component_masks


class SlotMapping:
    def __init__(self, name, slot=None, alpha_slot=None, switch_slot=None, value_func=None, coords="UV0"):
        self.name = name
        self.slot = name if slot is None else slot
        self.alpha_slot = alpha_slot
        self.switch_slot = switch_slot
        self.value_func = value_func
        self.coords = coords

default_mappings = MappingCollection(
    textures=[
        SlotMapping("Diffuse"),
        SlotMapping("D", "Diffuse"),
        SlotMapping("Base Color", "Diffuse"),
        SlotMapping("BaseColorMap", "Diffuse"),
        SlotMapping("BaseColorMap_a", "Diffuse"),
        SlotMapping("BaseColorMap_b", "Diffuse"),
        SlotMapping("BaseColorMap_c", "Diffuse"),
        SlotMapping("Base_BaseColor", "Diffuse"),
        SlotMapping("Albedo", "Diffuse"),
        SlotMapping("DiffuseMap", "Diffuse", alpha_slot="Alpha"),
        SlotMapping("Param", "Diffuse", alpha_slot="Alpha"),
        SlotMapping("BaseColor", "Diffuse"),
        SlotMapping("Concrete", "Diffuse"),
        SlotMapping("Trunk_BaseColor", "Diffuse"),
        SlotMapping("Diffuse Top", "Diffuse"),
        SlotMapping("BaseColor_Trunk", "Diffuse"),
        SlotMapping("CliffTexture", "Diffuse"),
        SlotMapping("PM_Diffuse", "Diffuse"),
        SlotMapping("___Diffuse", "Diffuse"),
        SlotMapping("CloudTexture", "Diffuse", alpha_slot="Alpha"),
        SlotMapping("DIFF", "Diffuse", alpha_slot="Alpha"),
        SlotMapping("op_tex", "Alpha"),

        SlotMapping("Background Diffuse", alpha_slot="Background Diffuse Alpha"),
        SlotMapping("BG Diffuse Texture", "Background Diffuse", alpha_slot="Background Diffuse Alpha"),

        SlotMapping("M"),
        SlotMapping("Mask", "M"),
        SlotMapping("AO", "M"),
        SlotMapping("M Mask", "M"),

        SlotMapping("SpecularMasks"),
        SlotMapping("S", "SpecularMasks"),
        SlotMapping("SRM", "SpecularMasks"),
        SlotMapping("S Mask", "SpecularMasks"),
        SlotMapping("Specular Mask", "SpecularMasks"),
        SlotMapping("CombinedMask_a", "SpecularMasks"),
        SlotMapping("CombinedMask_b", "SpecularMasks"),
        SlotMapping("CombinedMask_c", "SpecularMasks"),
        SlotMapping("Base_MROH", "SpecularMasks"),
        SlotMapping("MROMap", "SpecularMasks"),
        SlotMapping("ORM", "SpecularMasks"),
        SlotMapping("MRO", "SpecularMasks"),
        SlotMapping("BaseMask", "SpecularMasks"),
        SlotMapping("SpecularMask", "SpecularMasks"),
        SlotMapping("Concrete_SpecMask", "SpecularMasks"),
        SlotMapping("Trunk_Specular", "SpecularMasks"),
        SlotMapping("Specular Top", "SpecularMasks"),
        SlotMapping("SMR_Trunk", "SpecularMasks"),
        SlotMapping("Cliff Spec Texture", "SpecularMasks"),
        SlotMapping("PM_SpecularMasks", "SpecularMasks"),
        SlotMapping("__PBR Masks", "SpecularMasks"),

        SlotMapping("Normals"),
        SlotMapping("N", "Normals"),
        SlotMapping("NormalMap_a", "Normals"),
        SlotMapping("NormalMap_b", "Normals"),
        SlotMapping("NormalMap_c", "Normals"),
        SlotMapping("Base_Normal", "Normals"),
        SlotMapping("BaseNormal", "Normals"),
        SlotMapping("Noemal_tex", "Normals"),
        SlotMapping("Normal", "Normals"),
        SlotMapping("NormalMap", "Normals"),
        SlotMapping("ConcreteTextureNormal", "Normals"),
        SlotMapping("Trunk_Normal", "Normals"),
        SlotMapping("Normals Top", "Normals"),
        SlotMapping("Normal_Trunk", "Normals"),
        SlotMapping("CliffNormal", "Normals"),
        SlotMapping("PM_Normals", "Normals"),
        SlotMapping("_Normal", "Normals"),
        
        SlotMapping("AnisotropicTangentWeight", alpha_slot="AnisotropicTangentWeight Alpha"),
        SlotMapping("AnisotropigTangentWeight", "AnisotropicTangentWeight", alpha_slot="AnisotropicTangentWeight Alpha"),

        SlotMapping("Emissive", "Emission"),
        SlotMapping("EmissiveMask", "Emission"),
        SlotMapping("InterEmiss - CellMask", "Emission"),
        SlotMapping("EmissiveTexture", "Emission"),
        SlotMapping("L1_Emissive", "Emission", coords="UV2"),
        SlotMapping("PM_Emissive", "Emission"),
        SlotMapping("Visor_Emissive", "Emission"),
        SlotMapping("EmissiveDistanceField"),
        SlotMapping("Visor_EmissiveDistanceField"),

        SlotMapping("MaskTexture"),
        SlotMapping("OpacityMask", "MaskTexture"),

        SlotMapping("SkinFX_Mask"),
        SlotMapping("SkinFX Mask", "SkinFX_Mask"),
        SlotMapping("TechArtMask", "SkinFX_Mask"),

        SlotMapping("Thin Film Texture"),

        SlotMapping("IceGradient"),

        SlotMapping("ClothFuzz Texture"),

        SlotMapping("Flipbook", "Flipbook Color", alpha_slot="Flipbook Alpha"),
        SlotMapping("MouthFlipbook", "Flipbook Color", alpha_slot="Flipbook Alpha"),
    ],
    scalars=[
        SlotMapping("RoughnessMin", "Roughness Min"),
        SlotMapping("SpecRoughnessMin", "Roughness Min"),
        SlotMapping("RawRoughnessMin", "Roughness Min"),
        SlotMapping("Rough Min", "Roughness Min"),
        SlotMapping("RoughnessMax", "Roughness Max"),
        SlotMapping("SpecRoughnessMax", "Roughness Max"),
        SlotMapping("RawRoughnessMax", "Roughness Max"),
        SlotMapping("Rough Max", "Roughness Max"),
        SlotMapping("emissive mult", "Emission Strength"),
        SlotMapping("DayMult", "Emission Strength"),

        SlotMapping("ThinFilm_Intensity"),
        SlotMapping("ThinFilmIntensity", "ThinFilm_Intensity"),
        SlotMapping("ThinFilm_RoughnessScale"),
        SlotMapping("ThinFilmRoughnessScale", "ThinFilm_RoughnessScale"),
        SlotMapping("ThinFilm_Exponent"),
        SlotMapping("ThinFilmExponent", "ThinFilm_Exponent"),
        SlotMapping("ThinFilm_Offset"),
        SlotMapping("ThinFilmOffset", "ThinFilm_Offset"),
        SlotMapping("ThinFilm_Scale"),
        SlotMapping("ThinFilmScale", "ThinFilm_Scale"),
        SlotMapping("ThinFilm_Warp"),
        SlotMapping("ThinFilmWarp", "ThinFilm_Warp"),

        SlotMapping("Ice Fresnel"),
        SlotMapping("Ice Brightness"),
        SlotMapping("Ice Emissive Brightness"),
        SlotMapping("Crystal_FresEX"),

        SlotMapping("EmissiveFresnelPower"),
        SlotMapping("Emissive Fres EX", "EmissiveFresnelPower"),
        SlotMapping("Invert Emissive Fresnel", "InvertEmissiveFresnel"),

        SlotMapping("AnisotropyMaxWeight"),

        SlotMapping("Fuzz Tiling"),
        SlotMapping("ClothFuzzTiling", "Fuzz Tiling"),
        SlotMapping("Fuzz Exponent"),
        SlotMapping("ClothFuzzExponent", "Fuzz Exponent"),
        SlotMapping("Fuzz Fresnel Blend"),
        SlotMapping("Cloth Base Color Intensity"),
        SlotMapping("Cloth_BaseColorIntensity", "Cloth Base Color Intensity"),
        SlotMapping("Cloth Roughness"),
        SlotMapping("Cloth_Roughness", "Cloth Roughness"),

        SlotMapping("Undercoat Roughness"),
        SlotMapping("UndercoatRoughness"),
        SlotMapping("Undercoat Metallic Multiplier"),
        SlotMapping("UndercoatMetallicMultiplier"),
        SlotMapping("Roughness Map Now affects Clearcoat roughness", "Use Roughness Map"),

        SlotMapping("SubImages"),
        SlotMapping("Flipbook X"),
        SlotMapping("Flipbook Y"),
        SlotMapping("Flipbook Scale"),
        SlotMapping("Use Second UV Channel", "Use Second UV"),

        SlotMapping("SubUV_Frames"),
        SlotMapping("Affects Base Color"),
        SlotMapping("Multiply Flipbook Emissive")

    ],
    vectors=[
        SlotMapping("color", "Background Diffuse"),
        SlotMapping("Skin Boost Color And Exponent", "Skin Color", alpha_slot="Skin Boost"),
        SlotMapping("SkinTint", "Skin Color", alpha_slot="Skin Boost"),
        SlotMapping("SkinColor", "Skin Color", alpha_slot="Skin Boost"),
        SlotMapping("EmissiveMultiplier", "Emission Multiplier"),
        SlotMapping("Emissive Multiplier", "Emission Multiplier"),
        SlotMapping("Emissive Color", "Emission Multiplier"),
        SlotMapping("EmissiveColor", "Emission Multiplier"),
        SlotMapping("Emissive", "Emission Multiplier"),

        SlotMapping("ThinFilm_Channel"),
        SlotMapping("ThinFilmMaskChannel", "ThinFilm_Channel"),
        SlotMapping("Ice Channel"),
        SlotMapping("Cloth Channel"),
        SlotMapping("ClothFuzzMaskChannel", "Cloth Channel"),

        SlotMapping("Fuzz Tint"),
        SlotMapping("ClothFuzzTint", "Fuzz Tint"),
        SlotMapping("Cloth Fuzz Tint", "Fuzz Tint"),

        SlotMapping("FlipbookTint"),
        
        SlotMapping("CloatcoatMaskChannel")
    ],
    switches=[
        SlotMapping("SwizzleRoughnessToGreen"),
        SlotMapping("UseEmissiveFresnel"),
        SlotMapping("Use Emissive Fresnel", "UseEmissiveFresnel"),
        SlotMapping("InvertEmissiveFresnel"),
        SlotMapping("UseAnisotropicShading"),
        SlotMapping("Use Thin Film"),
        SlotMapping("UseThinFilm", "Use Thin Film"),
        SlotMapping("Use Cloth Fuzz"),
        SlotMapping("UseClothFuzz", "Use Cloth Fuzz"),
        SlotMapping("Use Ice"),
        SlotMapping("Use Clear Coat"),
        SlotMapping("UseClearCoat", "Use Clear Coat"),
        SlotMapping("Use Sub UV texture", "Use Flipbook")
    ],
    component_masks=[
        SlotMapping("ThinFilm_Channel"),
        SlotMapping("ThinFilmMaskChannel", "ThinFilm_Channel"),
        SlotMapping("Ice Channel"),
        SlotMapping("Cloth Channel"),
        SlotMapping("ClothFuzzMaskChannel", "Cloth Channel"),
        SlotMapping("Clear Coat Channel"),
        SlotMapping("ClearCoatChannel"),
        SlotMapping("CloatcoatMaskChannel"),
        SlotMapping("ClearcoatMaskChannel"),
    ]
)

hero_mappings = MappingCollection(
    textures=[
        SlotMapping("BaseColor"),
        SlotMapping("ORM"),
        SlotMapping("Normal"),
        SlotMapping("SpecularTexture"),
        SlotMapping("Emissive"),
        SlotMapping("OpacityMask")
    ],
    scalars=[
        SlotMapping("AOIntensity"),
        SlotMapping("RoughnessPower"),
        SlotMapping("Metallic"),
        SlotMapping("NormalIntensity"),
        SlotMapping("EmissiveStrength")
    ],
    vectors=[
        SlotMapping("ExtraSpecularTint"),
    ]
)

hair_mappings = MappingCollection(
    textures=[
        SlotMapping("BaseColor"),
        SlotMapping("ShadowColor(UV1)", coords="UV1"),
        SlotMapping("Normal"),
        SlotMapping("SpecularShift"),
        SlotMapping("AnisotropyBrightness", coords="UV1"),
        SlotMapping("OpacityMask"),
    ],
    scalars=[
        SlotMapping("Color Boost"),
        SlotMapping("RoughnessPower"),
        SlotMapping("NormalIntensity"),
        SlotMapping("ExtraSpecularShift"),
        SlotMapping("SpecularShiftPower"),
        SlotMapping("Anisotropy Direction"),
    ],
    vectors=[
        SlotMapping("Tangent"),
    ]
)

translucent_mappings = MappingCollection(
    textures=[
        SlotMapping("BaseColor"),
        SlotMapping("OpacityMask"),
    ],
    scalars=[
        SlotMapping("OpacityMultiplier"),
    ],
    vectors=[
        SlotMapping("BaseTint"),
    ]
)

eye_mappings = MappingCollection(
    textures=[
        SlotMapping("ScleraBaseColor"),
        SlotMapping("IrisBaseColor"),
        SlotMapping("IrisHeight"),
        SlotMapping("IrisBaseAO"),
    ],
    scalars=[
        SlotMapping("ScleraRoughness"),
        SlotMapping("IrisRoughness"),
        SlotMapping("IrisBrightness"),
    ]
)

pre_eye_mappings = MappingCollection(
    scalars=[
        SlotMapping("ScleraScale"),
        SlotMapping("IrisUVRadius"),
        SlotMapping("PupilScale")
    ]
)

eye_glass_mappings = MappingCollection(
    textures=[
        SlotMapping("HighlightMask")
    ],
    scalars=[
        SlotMapping("HighlightIntensity"),
        SlotMapping("Opacity"),
        SlotMapping("Opacity_HighLight")
    ],
    vectors=[
        SlotMapping("Matcap - Color"),
        SlotMapping("FakeHighlight_Color")
    ],
    switches=[
        SlotMapping("FakeHighlight?")
    ]
)

pre_eye_glass_mappings = MappingCollection(
    scalars=[
        SlotMapping("TileX"),
        SlotMapping("TileY"),
        SlotMapping("RotateAngle")
    ],
    vectors=[
        SlotMapping("RotateFactor")
    ]
)

layer_mappings = MappingCollection(
    textures=[
        SlotMapping("Diffuse", alpha_slot="MaskTexture"),
        SlotMapping("SpecularMasks"),
        SlotMapping("Normals"),
        SlotMapping("EmissiveTexture"),
        SlotMapping("MaskTexture"),
        SlotMapping("Background Diffuse", alpha_slot="Background Diffuse Alpha"),

        SlotMapping("Diffuse_Texture_2", alpha_slot="MaskTexture_2"),
        SlotMapping("SpecularMasks_2"),
        SlotMapping("Normals_Texture_2"),
        SlotMapping("Emissive_Texture_2"),
        SlotMapping("MaskTexture_2"),
        SlotMapping("Background Diffuse 2", alpha_slot="Background Diffuse Alpha 2"),

        SlotMapping("Diffuse_Texture_3", alpha_slot="MaskTexture_3"),
        SlotMapping("SpecularMasks_3"),
        SlotMapping("Normals_Texture_3"),
        SlotMapping("Emissive_Texture_3"),
        SlotMapping("MaskTexture_3"),
        SlotMapping("Background Diffuse 3", alpha_slot="Background Diffuse Alpha 3"),

        SlotMapping("Diffuse_Texture_4", alpha_slot="MaskTexture_4"),
        SlotMapping("SpecularMasks_4"),
        SlotMapping("Normals_Texture_4"),
        SlotMapping("Emissive_Texture_4"),
        SlotMapping("MaskTexture_4"),
        SlotMapping("Background Diffuse 4", alpha_slot="Background Diffuse Alpha 4"),

        SlotMapping("Diffuse_Texture_5", alpha_slot="MaskTexture_5"),
        SlotMapping("SpecularMasks_5"),
        SlotMapping("Normals_Texture_5"),
        SlotMapping("Emissive_Texture_5"),
        SlotMapping("MaskTexture_5"),
        SlotMapping("Background Diffuse 5", alpha_slot="Background Diffuse Alpha 5"),

        SlotMapping("Diffuse_Texture_6", alpha_slot="MaskTexture_6"),
        SlotMapping("SpecularMasks_6"),
        SlotMapping("Normals_Texture_6"),
        SlotMapping("Emissive_Texture_6"),
        SlotMapping("MaskTexture_6"),
        SlotMapping("Background Diffuse 6", alpha_slot="Background Diffuse Alpha 6"),
    ]
)

toon_mappings = MappingCollection(
    textures=[
        SlotMapping("LitDiffuse"),
        SlotMapping("Color_Lit_Map", "LitDiffuse"),
        SlotMapping("ShadedDiffuse"),
        SlotMapping("Color_Shaded_Map", "ShadedDiffuse"),
        SlotMapping("DistanceField_InkLines"),
        SlotMapping("DFL_Map", "DistanceField_InkLines"),
        SlotMapping("InkLineColor_Texture"),
        SlotMapping("SSC_Texture"),
        SlotMapping("STM_Map", "SSC_Texture"),
        SlotMapping("STT_Map"),
        SlotMapping("Normals"),
        SlotMapping("Normal_Map", "Normals")
    ],
    scalars=[
        SlotMapping("ShadedColorDarkening"),
        SlotMapping("FakeNormalBlend_Amt"),
        SlotMapping("VertexBakedNormal_Blend", "FakeNormalBlend_Amt"),
        SlotMapping("PBR_Shading", "Use PBR Shading", value_func=lambda value: int(value))
    ],
    vectors=[
        SlotMapping("InkLineColor", "InkLineColor_Texture"),
        SlotMapping("Color_Lit", "LitDiffuse"),
        SlotMapping("Color_Shaded", "ShadedDiffuse"),
        SlotMapping("SpecularTint"),
        SlotMapping("Specular Tint", "SpecularTint"),
    ]
)

valet_mappings = MappingCollection(
    textures=[
        SlotMapping("Diffuse"),
        SlotMapping("Mask", alpha_slot="Mask Alpha"),
        SlotMapping("Decal", alpha_slot="Decal Alpha", coords="UV1"),
        SlotMapping("Normal"),
        SlotMapping("Specular Mask"),
        SlotMapping("Scratch/Grime/EMPTY"),
    ],
    scalars=[
        SlotMapping("Scratch Intensity"),
        SlotMapping("Grime Intensity"),
        SlotMapping("Grime Spec"),
        SlotMapping("Grime Roughness"),

        SlotMapping("Layer 01 Specular"),
        SlotMapping("Layer 01 Metalness"),
        SlotMapping("Layer 01 Roughness Min"),
        SlotMapping("Layer 01 Roughness Max"),
        SlotMapping("Layer 01 Clearcoat"),
        SlotMapping("Layer 01 Clearcoat Roughness Min"),
        SlotMapping("Layer 01 Clearcoat Roughness Max"),

        SlotMapping("Layer 02 Specular"),
        SlotMapping("Layer 02 Metalness"),
        SlotMapping("Layer 02 Roughness Min"),
        SlotMapping("Layer 02 Roughness Max"),
        SlotMapping("Layer 02 Clearcoat"),
        SlotMapping("Layer 02 Clearcoat Roughness Min"),
        SlotMapping("Layer 02 Clearcoat Roughness Max"),

        SlotMapping("Layer 03 Specular"),
        SlotMapping("Layer 03 Metalness"),
        SlotMapping("Layer 03 Roughness Min"),
        SlotMapping("Layer 03 Roughness Max"),
        SlotMapping("Layer 03 Clearcoat"),
        SlotMapping("Layer 03 Clearcoat Roughness Min"),
        SlotMapping("Layer 03 Clearcoat Roughness Max"),

        SlotMapping("Layer 04 Specular"),
        SlotMapping("Layer 04 Metalness"),
        SlotMapping("Layer 04 Roughness Min"),
        SlotMapping("Layer 04 Roughness Max"),
        SlotMapping("Layer 04 Clearcoat"),
        SlotMapping("Layer 04 Clearcoat Roughness Min"),
        SlotMapping("Layer 04 Clearcoat Roughness Max"),
    ],
    vectors=[
        SlotMapping("Scratch Tint"),
        SlotMapping("Grime Tint"),

        SlotMapping("Layer 01 Color"),
        SlotMapping("Layer 02 Color"),
        SlotMapping("Layer 03 Color"),
        SlotMapping("Layer 04 Color"),
    ]
)

glass_mappings = MappingCollection(
    textures=[
        SlotMapping("Color_DarkTint"),
        SlotMapping("Diffuse", "Color"),
        SlotMapping("Diffuse Texture", "Color"),
        SlotMapping("Diffuse Texture with Alpha Mask", "Color"),
        SlotMapping("Diffuse Texture with Alpha Mask", "Color", alpha_slot="Mask"),
        SlotMapping("PM_Diffuse", "Color"),
        SlotMapping("BaseColorMap", "Color"),

        SlotMapping("Normals"),
        SlotMapping("BakedNormal", "Normals"),
        SlotMapping("PM_Normals", "Normals"),
        SlotMapping("CustomNormal", "Normals"),
    ],
    scalars=[
        SlotMapping("Specular"),
        SlotMapping("GlassSpecular", "Specular"),
        SlotMapping("Metallic"),
        SlotMapping("GlassMetallic", "Metallic"),
        SlotMapping("Roughness"),
        SlotMapping("GlassRoughness", "Roughness"),
        SlotMapping("Window Tint Amount", "Tint Amount"),
        SlotMapping("Opacity", "Tint Amount"),
        SlotMapping("Exponent"),
        SlotMapping("Fresnel Exponent", "Exponent"),
        SlotMapping("FresnelExponentTransparency", "Exponent"),
        SlotMapping("Inner Transparency"),
        SlotMapping("InnerTransparency", "Inner Transparency"),
        SlotMapping("Fresnel Inner Transparency", "Inner Transparency"),
        SlotMapping("Inner Transparency Max Tint"),
        SlotMapping("Fresnel Inner Transparency Max Tint", "Inner Transparency Max Tint"),
        SlotMapping("Outer Transparency"),
        SlotMapping("OuterTransparency", "Outer Transparency"),
        SlotMapping("Fresnel Outer Transparency", "Outer Transparency"),
        SlotMapping("Glass thickness", "Thickness"),
        SlotMapping("GlassThickness", "Thickness"),
        SlotMapping("Alpha Channel Mask Opacity", "Mask Opacity")
    ],
    vectors=[
        SlotMapping("ColorFront", "Color"),
        SlotMapping("Base Color", "Color"),
        SlotMapping("BaseColorTint", "Color_DarkTint"),
    ]
)

trunk_mappings = MappingCollection(
    textures=[
        SlotMapping("Trunk_BaseColor", "Diffuse"),
        SlotMapping("Trunk_Specular", "SpecularMasks"),
        SlotMapping("Trunk_Normal", "Normals"),
        SlotMapping("BaseColor_Trunk", "Diffuse"),
        SlotMapping("SMR_Trunk", "SpecularMasks"),
        SlotMapping("Normal_Trunk", "Normals"),
    ]
)

foliage_mappings = MappingCollection(
    textures=[
        SlotMapping("Diffuse"),
        SlotMapping("DiffuseMap", "Diffuse", alpha_slot="MaskTexture"),
        SlotMapping("Normals"),
        SlotMapping("NormalMap", "Normals"),
        SlotMapping("MaskTexture"),
    ],
    scalars=[
        SlotMapping("Roughness Leafs", "Roughness"),
        SlotMapping("Specular_Leafs", "Specular")
    ],
    vectors=[
        SlotMapping("Color1_Base"),
        SlotMapping("Color2_Lit"),
        SlotMapping("Color3_Shadows")
    ]
)

gradient_mappings = MappingCollection(
    textures=[
        SlotMapping("Diffuse"),
        SlotMapping("Layer Mask", alpha_slot="Layer Mask Alpha"),
        SlotMapping("SkinFX_Mask"),
        SlotMapping("Layer1_Gradient"),
        SlotMapping("Layer2_Gradient"),
        SlotMapping("Layer3_Gradient"),
        SlotMapping("Layer4_Gradient"),
        SlotMapping("Layer5_Gradient"),
    ],
    switches=[
        SlotMapping("use Alpha Channel as mask", "Use Layer Mask Alpha")
    ],
    component_masks=[
        SlotMapping("GmapSkinCustomization_Channel")
    ]
)

gmap_mappings = MappingCollection(
    textures=[
        SlotMapping("M")
    ]
)

bean_base_mappings = MappingCollection(
    textures=[
        SlotMapping("Body_Pattern", coords="UV1"),
    ],
    vectors=[
        SlotMapping("Body_EyesColor"),
        SlotMapping("Body_MainColor"),
        SlotMapping("Body_SecondaryColor"),
        SlotMapping("Body_FacePlateColor"),
        SlotMapping("Body_Eyes_MaterialProps"),
        SlotMapping("Body_Faceplate_MaterialProps"),
        SlotMapping("Body_GlassesEyeLashes"),
        SlotMapping("Body_MaterialProps"),
        SlotMapping("Body_Secondary_MaterialProps"),
        SlotMapping("Eyelashes_Color"),
        SlotMapping("Eyelashes_MaterialProps"),
        SlotMapping("Glasses_Frame_Color"),
        SlotMapping("Glasses_Frame_MaterialProps"),
        SlotMapping("Body_EyesColor"),
        SlotMapping("Glasses_Lense_Color"),
        SlotMapping("Glasses_Lense_MaterialProps"),
    ]
)

bean_costume_mappings = MappingCollection(
    textures=[
        SlotMapping("Metalness/Roughness/Specular/Albedo", "Metalness/Roughness/Specular", alpha_slot="Albedo"),
        SlotMapping("MaterialMasking"),
        SlotMapping("NormalMap"),
    ],
    vectors=[
        SlotMapping("Costume_MainColor"),
        SlotMapping("Costume_MainMaterialProps"),
        SlotMapping("Costume_Secondary_Color"),
        SlotMapping("Costume_SecondaryMaterialProps"),
        SlotMapping("Costume_AccentColor"),
        SlotMapping("Costume_AccentMaterialProps"),
    ]
)

bean_head_costume_mappings = MappingCollection(
    textures=[
        SlotMapping("Metalness/Roughness/Specular/Albedo", "Metalness/Roughness/Specular", alpha_slot="Albedo"),
        SlotMapping("MaterialMasking"),
        SlotMapping("NormalMap"),
    ],
    vectors=[
        SlotMapping("Head_Costume_MainColor", "Costume_MainColor"),
        SlotMapping("Head_Costume_MainMaterialProps", "Costume_MainMaterialProps"),
        SlotMapping("Head_Costume_Secondary_Color", "Costume_Secondary_Color"),
        SlotMapping("Head_Costume_SecondaryMaterialProps", "Costume_SecondaryMaterialProps"),
        SlotMapping("Head_Costume_AccentColor", "Costume_AccentColor"),
        SlotMapping("Head_Costume_AccentMaterialProps", "Costume_AccentMaterialProps"),
    ]
)

gmap_material_mappings = MappingCollection(
    textures=[
        SlotMapping("Diffuse"),
        SlotMapping("M"),
        SlotMapping("Color Mask 1"),
        SlotMapping("Color Mask 2"),
        SlotMapping("Color Mask 3"),
        SlotMapping("ColorVariety/Scratch/Dirt Mask"),
    ],
    vectors=[
        SlotMapping("Base Color: Color A"),
        SlotMapping("Base Color: Color B"),
        SlotMapping("Base Color: Color C"),
        SlotMapping("Color Mask 1-R: Color A"),
        SlotMapping("Color Mask 1-R: Color B"),
        SlotMapping("Color Mask 1-R: Color C"),
        SlotMapping("Color Mask 1-G: Color A"),
        SlotMapping("Color Mask 1-G: Color B"),
        SlotMapping("Color Mask 1-G: Color C"),
        SlotMapping("Color Mask 1-B: Color A"),
        SlotMapping("Color Mask 1-B: Color B"),
        SlotMapping("Color Mask 1-B: Color C"),
        SlotMapping("Color Mask 2-R: Color A"),
        SlotMapping("Color Mask 2-R: Color B"),
        SlotMapping("Color Mask 2-R: Color C"),
        SlotMapping("Color Mask 2-G: Color A"),
        SlotMapping("Color Mask 2-G: Color B"),
        SlotMapping("Color Mask 2-G: Color C"),
        SlotMapping("Color Mask 2-B: Color A"),
        SlotMapping("Color Mask 2-B: Color B"),
        SlotMapping("Color Mask 2-B: Color C"),
        SlotMapping("Color Mask 3-R: Color A"),
        SlotMapping("Color Mask 3-R: Color B"),
        SlotMapping("Color Mask 3-R: Color C"),
        SlotMapping("Color Mask 3-G: Color A"),
        SlotMapping("Color Mask 3-G: Color B"),
        SlotMapping("Color Mask 3-G: Color C"),
        SlotMapping("Color Mask 3-B: Color A"),
        SlotMapping("Color Mask 3-B: Color B"),
        SlotMapping("Color Mask 3-B: Color C"),
        SlotMapping("Color Variety Mask: Color A"),
        SlotMapping("Color Variety Mask: Color B"),
        SlotMapping("Color Variety Mask: Color C"),
        SlotMapping("Scratch Color A"),
        SlotMapping("Scratch Color B"),
        SlotMapping("Dirt Color A"),
        SlotMapping("Dirt Color B"),
    ],
    scalars=[
        SlotMapping("Color Variety Mask: Opacity"),
    ],
    switches=[
        SlotMapping("Use Diffuse as Base Color"),
        SlotMapping("Uses 2+ Color Masks"),
        SlotMapping("Uses 3 Color Masks"),
        SlotMapping("Uses ColorVariety/Scratch/Dirt Mask")
    ]
)