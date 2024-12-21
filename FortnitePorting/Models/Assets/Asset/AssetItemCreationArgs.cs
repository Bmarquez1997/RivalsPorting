using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.GameplayTags;
using FortnitePorting.Models.Assets.Base;

namespace FortnitePorting.Models.Assets.Asset;

public class AssetItemCreationArgs : BaseAssetItemCreationArgs
{
    public UObject Object { get; set; }
    public required UTexture2D Icon { get; set; }
    public FGameplayTagContainer? GameplayTags { get; set; }
    
    public bool HideRarity { get; set; } = false;
    public FLinearColor MainColor { get; set; }
    public FLinearColor SecondaryColor { get; set; }
}