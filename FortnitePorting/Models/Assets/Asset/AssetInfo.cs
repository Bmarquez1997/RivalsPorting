using System;
using System.Collections.Generic;
using System.Linq;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.i18N;
using FortnitePorting.Shared.Extensions;

namespace FortnitePorting.Models.Assets.Asset;

public partial class AssetInfo : Base.BaseAssetInfo
{
    public new AssetItem Asset
    {
        get => (AssetItem) base.Asset;
        private init => base.Asset = value;
    }
    
    public AssetInfo(AssetItem asset)
    {
        // Add skin handling in here?
        Asset = asset;

        // if (!CUE4ParseVM.Provider.TryLoadObject<UDataTable>(
        //         "Marvel/Content/Marvel/Data/DataTable/HeroGallery/UISkinTable", out var skinsTable))
        // {
        //     return;
        // }
        // var skinsTable =
        //     CUE4ParseVM.Provider.LoadObject<UDataTable>("Marvel/Content/Marvel/Data/DataTable/HeroGallery/UISkinTable");
        //
        // var styles = skinsTable.RowMap.Values
        //     .Where(style => MatchesCharacterID(Asset.CreationData.ID, style))
        //     .ToArray();
        //
        // if (styles.Length == 0) return;
        //
        // var styleInfo = new AssetStyleInfo("Skins", styles, Asset.IconDisplayImage);
        // if (styleInfo.StyleDatas.Count > 0) StyleInfos.Add(styleInfo);

        // if (Asset.CreationData.Object is null) return;
        //
        // var styles = Asset.CreationData.Object.GetOrDefault("ItemVariants", Array.Empty<UObject>());
        // foreach (var style in styles)
        // {
        //     var channel = style.GetOrDefault("VariantChannelName", new FText("Style")).Text.ToLower().TitleCase();
        //     var optionsName = style.ExportType switch
        //     {
        //         "FortCosmeticCharacterPartVariant" => "PartOptions",
        //         "FortCosmeticMaterialVariant" => "MaterialOptions",
        //         "FortCosmeticParticleVariant" => "ParticleOptions",
        //         "FortCosmeticMeshVariant" => "MeshOptions",
        //         "FortCosmeticGameplayTagVariant" => "GenericTagOptions",
        //         _ => null
        //     };
        //
        //     if (optionsName is null) continue;
        //
        //     var options = style.Get<FStructFallback[]>(optionsName);
        //     if (options.Length == 0) continue;
        //
        //     var styleInfo = new AssetStyleInfo(channel, options, Asset.IconDisplayImage);
        //     if (styleInfo.StyleDatas.Count > 0) StyleInfos.Add(styleInfo);
        // }
    }

    public AssetInfo(AssetItem asset, FStructFallback[] styles)
    {
        // Add skin handling in here?
        Asset = asset;

        if (styles.Length == 0) return;

        //TODO: image/name/skeletalmesh handling
        var styleInfo = new AssetStyleInfo("Skins", styles, Asset.IconDisplayImage);
        if (styleInfo?.StyleDatas?.Count > 0) StyleInfos.Add(styleInfo);
    }

    private bool MatchesCharacterID(string characterID, FStructFallback skinRow)
    {
        if (skinRow.TryGetValue(out FStructFallback identifier, "Identifier")
            && identifier.TryGetValue(out string heroId, "HeroID")
            && identifier.TryGetValue(out string shapeId, "ShapeID"))
        {
            return characterID.StartsWith(heroId) && characterID.EndsWith(shapeId);
        }

        return false;
    }
    
    public AssetInfo(AssetItem asset, IEnumerable<string> stylePaths)
    {
        Asset = asset;
        if (Asset.CreationData.Object is null) return;

        var styleObjects = new List<UObject>();
        foreach (var stylePath in stylePaths)
        {
            if (CUE4ParseVM.Provider.TryLoadObject(stylePath, out var styleObject))
            {
                styleObjects.Add(styleObject);
            }
        }
        
        var styleInfo = new AssetStyleInfo("Styles", styleObjects, Asset.IconDisplayImage);
        if (styleInfo.StyleDatas.Count > 0) StyleInfos.Add(styleInfo);
    }
    
    public BaseStyleData[] GetSelectedStyles()
    {
        return Enumerable
            .SelectMany<AssetStyleInfo, BaseStyleData>(StyleInfos, info => info.SelectedItems)
            .ToArray();
    }
    
    public BaseStyleData[] GetAllStyles()
    {
        return Enumerable
            .SelectMany<AssetStyleInfo, BaseStyleData>(StyleInfos, info => info.StyleDatas)
            .ToArray();
    }
}