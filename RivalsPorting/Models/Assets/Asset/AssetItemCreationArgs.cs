using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.GameplayTags;
using RivalsPorting.Models.Assets.Base;

namespace RivalsPorting.Models.Assets.Asset;

public class AssetItemCreationArgs : BaseAssetItemCreationArgs
{
    public UObject? Object { get; set; }
    public string? LowResIconPath { get; set; }
    public string? HighResIconPath { get; set; }
    public string? IconPath => LowResIconPath ?? HighResIconPath;
    public FGameplayTagContainer? GameplayTags { get; set; }
    
    public bool HideRarity { get; set; } = false;
    public FLinearColor MainColor { get; set; }
    public FLinearColor SecondaryColor { get; set; }
}