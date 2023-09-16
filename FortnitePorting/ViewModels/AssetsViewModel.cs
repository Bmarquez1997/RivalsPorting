using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CUE4Parse.UE4;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.i18N;
using CUE4Parse.UE4.Objects.Engine;
using FortnitePorting.Controls;
using FortnitePorting.Extensions;
using FortnitePorting.Framework;
using AssetItem = FortnitePorting.Controls.Assets.AssetItem;

namespace FortnitePorting.ViewModels;

public partial class AssetsViewModel : ViewModelBase
{
    [ObservableProperty] private EExportType exportType = EExportType.Blender;
    [ObservableProperty] private EAssetType currentTabType = EAssetType.Outfit;
    
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasSelectedAsset))] private AssetItem? currentAsset;
    public bool HasSelectedAsset => CurrentAsset is not null;

    [ObservableProperty] private ObservableCollection<AssetItem> outfits = new();
    [ObservableProperty] private ObservableCollection<AssetItem> backpacks = new();
    [ObservableProperty] private ObservableCollection<AssetItem> pickaxes = new();
    [ObservableProperty] private ObservableCollection<AssetItem> gliders = new();
    [ObservableProperty] private ObservableCollection<AssetItem> pets = new();
    [ObservableProperty] private ObservableCollection<AssetItem> toys = new();
    [ObservableProperty] private ObservableCollection<AssetItem> sprays = new();
    [ObservableProperty] private ObservableCollection<AssetItem> loadingScreens = new();
    [ObservableProperty] private ObservableCollection<AssetItem> emotes = new();
    [ObservableProperty] private ObservableCollection<AssetItem> musicPacks = new();
    [ObservableProperty] private ObservableCollection<AssetItem> props = new();
    [ObservableProperty] private ObservableCollection<AssetItem> galleries = new();
    [ObservableProperty] private ObservableCollection<AssetItem> items = new();
    [ObservableProperty] private ObservableCollection<AssetItem> traps = new();
    [ObservableProperty] private ObservableCollection<AssetItem> vehicles = new();
    [ObservableProperty] private ObservableCollection<AssetItem> wildlife = new();
    
    [ObservableProperty] private ObservableCollection<UserControl> extraOptions = new();

    public readonly List<AssetLoader> Loaders;

    public AssetsViewModel()
    {
        Loaders = new List<AssetLoader>
        {
            new(EAssetType.Outfit)
            {
                Target = Outfits,
                Classes = new[] { "AthenaCharacterItemDefinition" },
                IconHandler = asset =>
                {
                    asset.TryGetValue(out UTexture2D? previewImage, "SmallPreviewImage", "LargePreviewImage");
                    if (asset.TryGetValue(out UObject heroDef, "HeroDefinition"))
                    {
                        heroDef.TryGetValue(out previewImage, "SmallPreviewImage", "LargePreviewImage");
                    }

                    return previewImage;
                }
            },
            new(EAssetType.Backpack)
            {
                Target = Backpacks,
                Classes = new[] { "AthenaBackpackItemDefinition" }
            },
            new(EAssetType.Pickaxe)
            {
                Target = Pickaxes,
                Classes = new[] { "AthenaPickaxeItemDefinition" },
                IconHandler = asset =>
                {
                    asset.TryGetValue(out UTexture2D? previewImage, "SmallPreviewImage", "LargePreviewImage");
                    if (asset.TryGetValue(out UObject heroDef, "WeaponDefinition"))
                    {
                        heroDef.TryGetValue(out previewImage, "SmallPreviewImage", "LargePreviewImage");
                    }

                    return previewImage;
                }
            },
            new(EAssetType.Glider)
            {
                Target = Gliders,
                Classes = new[] { "AthenaGliderItemDefinition" }
            },
            new(EAssetType.Pet)
            {
                Target = Pets,
                Classes = new[] { "AthenaPetCarrierItemDefinition" }
            },
            new(EAssetType.Toy)
            {
                Target = Toys,
                Classes = new[] { "AthenaToyItemDefinition" }
            },
            new(EAssetType.Spray)
            {
                Target = Sprays,
                Classes = new[] { "AthenaSprayItemDefinition" }
            },
            new(EAssetType.LoadingScreen)
            {
                Target = LoadingScreens,
                Classes = new[] { "FortHomebaseBannerIconItemDefinition" }
            },
            new(EAssetType.Emote)
            {
                Target = Emotes,
                Classes = new[] { "AthenaDanceItemDefinition" }
            },
            new(EAssetType.MusicPack)
            {
                Target = MusicPacks,
                Classes = new[] { "AthenaMusicPackItemDefinition" }
            },
            new(EAssetType.Prop)
            {
                Target = Props,
                Classes = new[] { "FortPlaysetPropItemDefinition" }
            },
            new(EAssetType.Gallery)
            {
                Target = Galleries,
                Classes = new[] { "FortPlaysetItemDefinition" }
            },
            new(EAssetType.Item)
            {
                Target = Items,
                Classes = new[] {"AthenaGadgetItemDefinition", "FortWeaponRangedItemDefinition", "FortWeaponMeleeItemDefinition", "FortCreativeWeaponMeleeItemDefinition", "FortCreativeWeaponRangedItemDefinition", "FortWeaponMeleeDualWieldItemDefinition" }
            },
            new(EAssetType.Trap)
            {
                Target = Traps,
                Classes = new[] { "FortTrapItemDefinition" }
            },
            new(EAssetType.Vehicle)
            {
                Target = Vehicles,
                Classes = new[] { "FortVehicleItemDefinition" },
                IconHandler = asset => GetVehicleInfo<UTexture2D>(asset, "SmallPreviewImage", "LargePreviewImage", "Icon"),
                DisplayNameHandler = asset => GetVehicleInfo<FText>(asset, "DisplayName")
            },
            new(EAssetType.Wildlife)
            {
                Target = Wildlife,
                CustomLoadingHandler = async loader =>
                {
                    var entries = new[]
                    {
                        await WildlifeEntry.Create("Boar", 
                        "FortniteGame/Plugins/GameFeatures/Irwin/Content/AI/Prey/Burt/Meshes/Burt_Mammal", 
                        "FortniteGame/Plugins/GameFeatures/Irwin/Content/Icons/T-Icon-Fauna-Boar"),

                        await WildlifeEntry.Create("Chicken",
                        "FortniteGame/Plugins/GameFeatures/Irwin/Content/AI/Prey/Nug/Meshes/Nug_Bird",
                        "FortniteGame/Plugins/GameFeatures/Irwin/Content/Icons/T-Icon-Fauna-Chicken"),

                        await WildlifeEntry.Create("Crow",
                        "FortniteGame/Plugins/GameFeatures/Irwin/Content/AI/Prey/Crow/Meshes/Crow_Bird",
                        "FortniteGame/Plugins/GameFeatures/Irwin/Content/Icons/T-Icon-Fauna-Crow"),

                        await WildlifeEntry.Create("Frog",
                        "FortniteGame/Plugins/GameFeatures/Irwin/Content/AI/Simple/Smackie/Meshes/Smackie_Amphibian",
                        "FortniteGame/Plugins/GameFeatures/Irwin/Content/Icons/T-Icon-Fauna-Frog"),

                        await WildlifeEntry.Create("Raptor",
                        "FortniteGame/Plugins/GameFeatures/Irwin/Content/AI/Predators/Robert/Meshes/Jungle_Raptor_Fauna",
                        "FortniteGame/Plugins/GameFeatures/Irwin/Content/Icons/T-Icon-Fauna-JungleRaptor"),

                        await WildlifeEntry.Create("Wolf",
                        "FortniteGame/Plugins/GameFeatures/Irwin/Content/AI/Predators/Grandma/Meshes/Grandma_Mammal",
                        "FortniteGame/Plugins/GameFeatures/Irwin/Content/Icons/T-Icon-Fauna-Wolf"),
            
                        await WildlifeEntry.Create("Llama",
                        "FortniteGame/Plugins/GameFeatures/Labrador/Content/Meshes/Labrador_Mammal",
                        "FortniteGame/Content/UI/Foundation/Textures/Icons/Athena/T-T-Icon-BR-SM-Athena-SupplyLlama-01")
                    };
                    
                    await Parallel.ForEachAsync(entries, async (data, _) =>
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => loader.Target.Add(new AssetItem(data.Mesh, data.PreviewImage, data.Name, loader.Type)), DispatcherPriority.Background);
                    });
                }
            },
        };
    }

    public AssetLoader Get(EAssetType assetType)
    {
        return Loaders.First(x => x.Type == assetType);
    }

    public override async Task Initialize()
    {
        await Loaders.First().Load();
    }

    private T? GetVehicleInfo<T>(UObject asset, params string[] names) where T : class
    {
        FStructFallback? GetMarkerDisplay(UBlueprintGeneratedClass? blueprint)
        {
            var obj = blueprint?.ClassDefaultObject.Load();
            return obj?.GetOrDefault<FStructFallback>("MarkerDisplay");
        }
        
        var output = asset.GetAnyOrDefault<T?>(names);
        if (output is not null) return output;

        var vehicle = asset.Get<UBlueprintGeneratedClass>("VehicleActorClass");
        output = GetMarkerDisplay(vehicle)?.GetAnyOrDefault<T?>(names);
        if (output is not null) return output;

        var vehicleSuper = vehicle.SuperStruct.Load<UBlueprintGeneratedClass>();
        output = GetMarkerDisplay(vehicleSuper)?.GetAnyOrDefault<T?>(names);
        return output;
    }
}

public class AssetLoader
{
    public bool Started;
    public Pauser Pause = new();
    public ObservableCollection<AssetItem> Target;
    public string[] Classes = Array.Empty<string>();
    public Func<UObject, UTexture2D?> IconHandler = asset => asset.GetAnyOrDefault<UTexture2D?>("SmallPreviewImage", "LargePreviewImage");
    public Func<UObject, FText?> DisplayNameHandler = asset => asset.GetOrDefault("DisplayName", new FText(asset.Name));
    public Func<AssetLoader, Task>? CustomLoadingHandler;
    public EAssetType Type;
    
    public AssetLoader(EAssetType type)
    {
        Type = type;
    }
    
    public async Task Load()
    {
        if (Started) return;
        Started = true;

        if (CustomLoadingHandler is not null)
        {
            CustomLoadingHandler(this);
            return;
        }

        var assets = CUE4ParseVM.AssetRegistry.Where(data => Classes.Contains(data.AssetClass.Text)).ToList();

        var randomAsset = assets.FirstOrDefault(x => x.AssetName.Text.EndsWith("Random", StringComparison.OrdinalIgnoreCase));
        if (randomAsset is not null) assets.Remove(randomAsset);

        await Parallel.ForEachAsync(assets, async (data, _) =>
        {
            await Pause.WaitIfPaused();
            await LoadAsset(data);
        });
    }

    private async Task LoadAsset(FAssetData data)
    {
        var asset = await CUE4ParseVM.Provider.LoadObjectAsync(data.ObjectPath);
        await LoadAsset(asset);
    }
    
    private async Task LoadAsset(UObject asset)
    {
        var icon = IconHandler(asset);
        if (icon is null) return;

        var displayName = DisplayNameHandler(asset)?.Text;
        if (string.IsNullOrEmpty(displayName)) displayName = asset.Name;

        await Dispatcher.UIThread.InvokeAsync(() => Target.Add(new AssetItem(asset, icon, displayName, Type)), DispatcherPriority.Background);
    }
}

public class WildlifeEntry
{
    public string Name;
    public USkeletalMesh Mesh;
    public UTexture2D PreviewImage;

    public static async Task<WildlifeEntry> Create(string name, string meshPath, string imagePath)
    {
        return new WildlifeEntry
        {
            Name = name,
            Mesh = await CUE4ParseVM.Provider.LoadObjectAsync<USkeletalMesh>(meshPath),
            PreviewImage = await CUE4ParseVM.Provider.LoadObjectAsync<UTexture2D>(imagePath)
        };
    }
}