using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Assets.Utils;
using CUE4Parse.UE4.Objects.Core.i18N;
using CUE4Parse.UE4.Objects.UObject;

namespace FortnitePorting.Models.Fortnite;

public class UFortItemSeriesDefinition : UObject
{
    public FText DisplayName;
    public FRarityCollection Colors;
    public FSoftObjectPath BackgroundTexture;
    
    public override void Deserialize(FAssetArchive Ar, long validPos)
    {
        base.Deserialize(Ar, validPos);

        DisplayName = GetOrDefault<FText>(nameof(DisplayName));
        Colors = GetOrDefault<FRarityCollection>(nameof(Colors));
        BackgroundTexture = GetOrDefault<FSoftObjectPath>(nameof(BackgroundTexture));
    }
}