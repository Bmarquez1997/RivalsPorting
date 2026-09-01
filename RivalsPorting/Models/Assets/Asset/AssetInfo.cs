using System;
using System.Collections.Generic;
using System.Linq;
using CUE4Parse_Conversion.Textures;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.i18N;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.Utils;
using RivalsPorting.CUE4Parse.Models.Fortnite;
using RivalsPorting.CUE4Parse.Models.Fortnite.Enums;
using RivalsPorting.Extensions;
using RivalsPorting.Models.Assets;
using Serilog;

namespace RivalsPorting.Models.Assets.Asset;

public partial class AssetInfo : Base.BaseAssetInfo
{
    public new AssetItem Asset
    {
        get => (AssetItem) base.Asset;
        private init => base.Asset = value;
    }
    
    public AssetInfo(AssetItem asset)
    {
        Asset = asset;
        if (Asset.CreationData.Object is null) return;
        
        var styles = Asset.CreationData.Object.GetOrDefault("ItemVariants", Array.Empty<UObject>());
        foreach (var style in styles)
        {
            var channel = style.GetOrDefault("VariantChannelName", new FText("Style")).Text.ToLower().TitleCase();
            var optionsName = style.ExportType switch
            {
                "FortCosmeticCharacterPartVariant" => "PartOptions",
                "FortCosmeticMaterialVariant" => "MaterialOptions",
                "FortCosmeticParticleVariant" => "ParticleOptions",
                "FortCosmeticMeshVariant" => "MeshOptions",
                "FortCosmeticGameplayTagVariant" => "GenericTagOptions",
                "FortCosmeticRichColorVariant" => "InlineVariant",
                "FortCosmeticMaterialParameterSetVariant" => "MaterialParameterSetChoices",
                "FortCosmeticMorphTargetVariant" => "MorphTargetOptions",
                "FortCosmeticLoadoutTagDrivenVariant" => "Variants",
                _ => null
            };

            if (optionsName is null) continue;
            
            // TODO: MaterialsToAlterForAllVariantMaterialParams handling (example in Wonder Onesie color styles)
            AssetStyleInfo styleInfo;
            if ("FortCosmeticRichColorVariant".Equals(style.ExportType) || "FortCosmeticMaterialParameterSetVariant".Equals(style.ExportType))
            {
                styleInfo = new AssetStyleInfo(channel, style, "FortCosmeticMaterialParameterSetVariant".Equals(style.ExportType));
            }
            else
            {
                var options = style.Get<FStructFallback[]>(optionsName);
                if (options.Length == 0) continue;
                    styleInfo = new AssetStyleInfo(channel, options, Asset.IconDisplayImage, "FortCosmeticLoadoutTagDrivenVariant".Equals(style.ExportType));
            }

            if (styleInfo.StyleDatas.Count > 0) StyleInfos.Add(styleInfo);
        }

        if (Asset.CreationData.ExportType is EExportType.Emote)
        {
            var maleBaseAnimation = Asset.CreationData.Object.GetOrDefault<UAnimMontage?>("Animation");
            maleBaseAnimation ??= Asset.CreationData.Object.GetOrDefault<UAnimMontage?>("FrontEndAnimation");
            
            var femaleBaseAnimation = Asset.CreationData.Object.GetOrDefault<UAnimMontage?>("AnimationFemaleOverride");
            var animationOverrides = Asset.CreationData.Object.GetOrDefault<FStructFallback[]>("AnimationOverrides", []);

            var sizedAnimations = new Dictionary<string, UAnimMontage>();

            if (maleBaseAnimation is not null)
                sizedAnimations.Add($"Male Medium ({maleBaseAnimation.Name})", maleBaseAnimation);
            
            if (femaleBaseAnimation is not null)
                sizedAnimations.Add($"Female Medium ({femaleBaseAnimation.Name})", femaleBaseAnimation);

            foreach (var animationOverride in animationOverrides)
            {
                var montage = animationOverride.GetOrDefault<UAnimMontage?>("EmoteMontage");
                if (montage is null) continue;
                
                var gender = animationOverride.GetEnumOrDefault<EFortCustomGender>("Gender").ToString();
                var bodyType = animationOverride.GetEnumOrDefault<EFortCustomBodyType>("BodyType").ToString();
                sizedAnimations.Add($"{gender} {bodyType} ({montage.Name})", montage);
            }

            if (sizedAnimations.Count > 0)
            {
                var sizedStyleDatas = sizedAnimations
                    .Select(kvp => new AnimStyleData(kvp.Key, kvp.Value))
                    .ToArray();
            
                StyleInfos.Add(new AssetStyleInfo("Animation Type", sizedStyleDatas));
            }
        }

        if (Asset.CreationData.ExportType is EExportType.Prefab)
        {
            var playsetProps = Asset.CreationData.Object.GetOrDefault<FSoftObjectPath[]>("AssociatedPlaysetProps", []); 
            
            var styleDatas = new List<ObjectStyleData>();
            foreach (var playsetPropPath in playsetProps)
            {
                if (!playsetPropPath.TryLoad(out var prop))
                    continue;
                
                var propIcon = prop.GetAnyOrDefault<UTexture2D>("SmallPreviewImage", "LargePreviewImage", "Icon", "LargeIcon");
                propIcon ??= prop.GetDataListItem<UTexture2D>("SmallPreviewImage", "LargePreviewImage", "Icon", "LargeIcon");
        
                if (propIcon?.Decode()?.ToWriteableBitmap() is not { } propBitmap)
                    continue;
                
                styleDatas.Add(new ObjectStyleData(prop.Name, prop, propBitmap)
                {
                    AssociatedExportType = EExportType.Prop
                });
            }
            
            StyleInfos.Add(new AssetStyleInfo("Individual Props", styleDatas)
            {
                RequiredSelection = false,
                MultiSelect = true,
                SelectedStyleIndex = -1
            });
        }
    }

    private readonly Dictionary<(string SkinId, string ShapeId), FStructFallback>? _outfitSkinLookup;

    public AssetInfo(AssetItem asset, FStructFallback[] styles)
    {
        Asset = asset;

        if (styles.Length == 0) return;

        var styleInfo = new AssetStyleInfo("Skins", styles, Asset.IconDisplayImage!);
        if (styleInfo.StyleDatas.Count > 0) StyleInfos.Add(styleInfo);
    }

    public AssetInfo(
        AssetItem asset,
        FStructFallback[] skins,
        IEnumerable<FormStyleData> forms,
        Dictionary<(string SkinId, string ShapeId), FStructFallback> skinLookup)
    {
        Asset = asset;
        _outfitSkinLookup = skinLookup;

        var formArray = forms.ToArray();
        if (formArray.Length > 1)
            StyleInfos.Add(new AssetStyleInfo("Forms", formArray));

        if (skins.Length > 0)
        {
            var styleInfo = new AssetStyleInfo("Skins", skins, Asset.IconDisplayImage!);
            if (styleInfo.StyleDatas.Count > 0) StyleInfos.Add(styleInfo);
        }
    }

    public AssetInfo(AssetItem asset, IEnumerable<BaseStyleData> styles, string channelName = "Styles")
    {
        Asset = asset;

        var styleArray = styles.ToArray();
        if (styleArray.Length <= 1) return;

        StyleInfos.Add(new AssetStyleInfo(channelName, styleArray));
    }
    
    public AssetInfo(AssetItem asset, IEnumerable<string> stylePaths)
    {
        Asset = asset;
        if (Asset.CreationData.Object is null) return;

        var styleObjects = new List<UObject>();
        foreach (var stylePath in stylePaths)
        {
            if (UEParse.Provider.TryLoadPackageObject(stylePath, out var styleObject))
            {
                styleObjects.Add(styleObject);
            }
        }
        
        var styleInfo = new AssetStyleInfo("Styles", styleObjects, Asset.IconDisplayImage);
        if (styleInfo.StyleDatas.Count > 0) StyleInfos.Add(styleInfo);
    }
    
    public BaseStyleData[] GetSelectedStyles()
    {
        return StyleInfos
            .SelectMany<AssetStyleInfo, BaseStyleData>(info => info.MultiSelect ? info.SelectedItems : [info.SelectedStyle])
            .ToArray();
    }
    
    public BaseStyleData[] GetAllStyles()
    {
        return StyleInfos
            .SelectMany<AssetStyleInfo, BaseStyleData>(info => info.StyleDatas)
            .ToArray();
    }

    /// <summary>
    /// Resolves the UISkinTable entry for the selected Form (ShapeID) + Skin (SkinID) combination.
    /// Falls back to the selected skin entry when no lookup match exists.
    /// </summary>
    public FStructFallback? ResolveOutfitSkin()
    {
        var selected = GetSelectedStyles();
        var skinStyle = selected.OfType<AssetStyleData>()
            .FirstOrDefault(style => style.StyleData.TryGetValue(out string _, "SkinItemID"));
        if (skinStyle is null) return null;

        var formStyle = selected.OfType<FormStyleData>().FirstOrDefault();
        var shapeId = formStyle?.ShapeId ?? "0";

        if (_outfitSkinLookup is not null
            && skinStyle.StyleData.TryGetValue(out FStructFallback identifier, "Identifier"))
        {
            var skinId = identifier.GetOrDefault("SkinID", string.Empty);
            if (!string.IsNullOrEmpty(skinId)
                && _outfitSkinLookup.TryGetValue((skinId, shapeId), out var resolved))
            {
                return resolved;
            }
        }

        return skinStyle.StyleData;
    }
}