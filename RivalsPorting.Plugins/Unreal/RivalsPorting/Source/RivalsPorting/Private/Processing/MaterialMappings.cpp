#include "RivalsPorting/Public/Processing/MaterialMappings.h"

const FMappingCollection FMaterialMappings::Default {
	.Textures = {
		FSlotMapping("BaseColor"),
        FSlotMapping("D", "BaseColor"),
        FSlotMapping("Base Color", "BaseColor"),
        FSlotMapping("BaseColorMap", "BaseColor"),
        FSlotMapping("BaseColorMap_a", "BaseColor"),
        FSlotMapping("BaseColorMap_b", "BaseColor"),
        FSlotMapping("BaseColorMap_c", "BaseColor"),
        FSlotMapping("Base_BaseColor", "BaseColor"),
        FSlotMapping("Albedo", "BaseColor"),
        FSlotMapping("DiffuseMap", "BaseColor"),
        FSlotMapping("Param", "BaseColor"),
        FSlotMapping("BaseColor", "BaseColor"),
        FSlotMapping("Concrete", "BaseColor"),
        FSlotMapping("Trunk_BaseColor", "BaseColor"),
        FSlotMapping("Diffuse Top", "BaseColor"),
        FSlotMapping("BaseColor_Trunk", "BaseColor"),
        FSlotMapping("CliffTexture", "BaseColor"),
        FSlotMapping("PM_Diffuse", "BaseColor"),
        FSlotMapping("___Diffuse", "BaseColor"),
        FSlotMapping("CloudTexture", "BaseColor"),
        FSlotMapping("DIFF", "BaseColor"),
        FSlotMapping("op_tex", "Alpha"),

        FSlotMapping("Background Diffuse"),
        FSlotMapping("BG Diffuse Texture", "Background Diffuse"),

        FSlotMapping("M"),
        FSlotMapping("Mask", "M"),
        FSlotMapping("AO", "M"),
        FSlotMapping("M Mask", "M"),

        FSlotMapping("MRO"),
        FSlotMapping("S", "MRO"),
        FSlotMapping("SRM", "MRO"),
        FSlotMapping("S Mask", "MRO"),
        FSlotMapping("Specular Mask", "MRO"),
        FSlotMapping("CombinedMask_a", "MRO"),
        FSlotMapping("CombinedMask_b", "MRO"),
        FSlotMapping("CombinedMask_c", "MRO"),
        FSlotMapping("Base_MROH", "MRO"),
        FSlotMapping("MROMap", "MRO"),
        FSlotMapping("ORM", "MRO"),
        FSlotMapping("BaseMask", "MRO"),
        FSlotMapping("SpecularMask", "MRO"),
        FSlotMapping("Concrete_SpecMask", "MRO"),
        FSlotMapping("Trunk_Specular", "MRO"),
        FSlotMapping("Specular Top", "MRO"),
        FSlotMapping("SMR_Trunk", "MRO"),
        FSlotMapping("Cliff Spec Texture", "MRO"),
        FSlotMapping("PM_SpecularMasks", "MRO"),
        FSlotMapping("__PBR Masks", "MRO"),

        FSlotMapping("Normals"),
        FSlotMapping("N", "Normals"),
        FSlotMapping("NormalMap_a", "Normals"),
        FSlotMapping("NormalMap_b", "Normals"),
        FSlotMapping("NormalMap_c", "Normals"),
        FSlotMapping("Base_Normal", "Normals"),
        FSlotMapping("BaseNormal", "Normals"),
        FSlotMapping("Noemal_tex", "Normals"),
        FSlotMapping("Normal", "Normals"),
        FSlotMapping("NormalMap", "Normals"),
        FSlotMapping("ConcreteTextureNormal", "Normals"),
        FSlotMapping("Trunk_Normal", "Normals"),
        FSlotMapping("Normals Top", "Normals"),
        FSlotMapping("Normal_Trunk", "Normals"),
        FSlotMapping("CliffNormal", "Normals"),
        FSlotMapping("PM_Normals", "Normals"),
        FSlotMapping("_Normal", "Normals"),

        FSlotMapping("MaskTexture"),
        FSlotMapping("OpacityMask", "MaskTexture"),
	}
};

const FMappingCollection FMaterialMappings::Layer {
	.Textures = {
		FSlotMapping("Diffuse"),
		FSlotMapping("SpecularMasks"),
	    FSlotMapping("Normals"),
	    FSlotMapping("EmissiveTexture"),
	    FSlotMapping("MaskTexture"),
	    FSlotMapping("Background Diffuse"),
		
	    FSlotMapping("Diffuse_Texture_2"),
	    FSlotMapping("SpecularMasks_2"),
	    FSlotMapping("Normals_Texture_2"),
	    FSlotMapping("Emissive_Texture_2"),
	    FSlotMapping("MaskTexture_2"),
	    FSlotMapping("Background Diffuse 2"),
		
	    FSlotMapping("Diffuse_Texture_3"),
	    FSlotMapping("SpecularMasks_3"),
	    FSlotMapping("Normals_Texture_3"),
	    FSlotMapping("Emissive_Texture_3"),
	    FSlotMapping("MaskTexture_3"),
	    FSlotMapping("Background Diffuse 3"),
		
	    FSlotMapping("Diffuse_Texture_4"),
	    FSlotMapping("SpecularMasks_4"),
	    FSlotMapping("Normals_Texture_4"),
	    FSlotMapping("Emissive_Texture_4"),
	    FSlotMapping("MaskTexture_4"),
	    FSlotMapping("Background Diffuse 4"),
		
	    FSlotMapping("Diffuse_Texture_5"),
	    FSlotMapping("SpecularMasks_5"),
	    FSlotMapping("Normals_Texture_5"),
	    FSlotMapping("Emissive_Texture_5"),
	    FSlotMapping("MaskTexture_5"),
	    FSlotMapping("Background Diffuse 5"),
		
	    FSlotMapping("Diffuse_Texture_6"),
	    FSlotMapping("SpecularMasks_6"),
	    FSlotMapping("Normals_Texture_6"),
	    FSlotMapping("Emissive_Texture_6"),
	    FSlotMapping("MaskTexture_6"),
	    FSlotMapping("Background Diffuse 6"),
	},
};

const FMappingCollection FMaterialMappings::Hero{
    .Textures = {
        FSlotMapping("BaseColor"),
        FSlotMapping("ORM"),
        FSlotMapping("Normal"),
        FSlotMapping("SpecularTexture"),
        FSlotMapping("Emissive"),
        FSlotMapping("OpacityMask")
    },
    .Scalars = {
        FSlotMapping("AOIntensity"),
        FSlotMapping("RoughnessPower"),
        FSlotMapping("Metallic"),
        FSlotMapping("NormalIntensity"),
        FSlotMapping("EmissiveStrength")
    },
    .Vectors = {
        FSlotMapping("ExtraSpecularTint"),
    }
};

const FMappingCollection FMaterialMappings::Hair{
    .Textures = {
        FSlotMapping("BaseColor"),
        FSlotMapping("ShadowColor(UV1)"),
        FSlotMapping("Normal"),
        FSlotMapping("SpecularShift"),
        FSlotMapping("AnisotropyBrightness"),
        FSlotMapping("OpacityMask"),
    },
    .Scalars = {
        FSlotMapping("Color Boost"),
        FSlotMapping("RoughnessPower"),
        FSlotMapping("NormalIntensity"),
        FSlotMapping("ExtraSpecularShift"),
        FSlotMapping("SpecularShiftPower"),
        FSlotMapping("Anisotropy Direction"),
    },
    .Vectors = {
        FSlotMapping("Tangent"),
    }
};

const FMappingCollection FMaterialMappings::Translucent{
    .Textures = {
        FSlotMapping("BaseColor"),
        FSlotMapping("OpacityMask"),
    },
    .Scalars = {
        FSlotMapping("OpacityMultiplier"),
    },
    .Vectors = {
        FSlotMapping("BaseTint"),
    }
};

const FMappingCollection FMaterialMappings::Eye{
    .Textures = {
        FSlotMapping("ScleraBaseColor"),
        FSlotMapping("IrisBaseColor"),
        FSlotMapping("IrisHeight"),
        FSlotMapping("IrisBaseAO"),
    },
    .Scalars = {
        FSlotMapping("ScleraRoughness"),
        FSlotMapping("IrisRoughness"),
    	FSlotMapping("IrisBrightness"),
		FSlotMapping("ScleraScale"),
		FSlotMapping("IrisUVRadius"),
		FSlotMapping("PupilScale")
    }
};

const FMappingCollection FMaterialMappings::EyeGlass{
    .Textures = {
        FSlotMapping("HighlightMask")
    },
    .Scalars = {
        FSlotMapping("HighlightIntensity"),
        FSlotMapping("Opacity"),
    	FSlotMapping("Opacity_HighLight"),
		FSlotMapping("TileX"),
		FSlotMapping("TileY"),
		FSlotMapping("RotateAngle")
    },
    .Vectors = {
        FSlotMapping("Matcap - Color"),
        FSlotMapping("HighlightTint"),
        FSlotMapping("RotateFactor")
    },
    .Switches = {
        FSlotMapping("FakeHighlight?")
    }
};