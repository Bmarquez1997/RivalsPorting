using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.i18N;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.UObject;
using DynamicData;
using RivalsPorting.Extensions;
using RivalsPorting.Framework;
using RivalsPorting.Models.Assets.Asset;
using RivalsPorting.Models.Assets.Base;
using RivalsPorting.Models.Assets.Loading;
using RivalsPorting.Shared.Extensions;

namespace RivalsPorting.Services;

public partial class AssetLoaderService : ObservableObject, IService, IResettable
{
    [ObservableProperty] private AssetLoader? _activeLoader;
    [ObservableProperty] private ReadOnlyObservableCollection<BaseAssetItem> _activeCollection = new([]);
    
    public List<AssetLoaderCategory> Categories { get; set; } =
    [
        new(EAssetCategory.Cosmetics)
        {
            Loaders = 
            [
                new AssetLoader(EExportType.Outfit)
                {
                    ClassNames = ["Blueprint"],
                    AssetNames = ["001_ShowBP"],
                    PlaceholderIconPath = "Marvel/Content/Marvel/UI/Textures/Gallery/Logo/img_gallery_insidepage_logo",
                    LoadHiddenAssets = true,
                    AssetHandler = async loader =>
                    {
                        static string GetDataTableIconPath(FStructFallback iconStruct)
                        {
                            if (iconStruct.TryGetValue(out FStructFallback icon,
                                    "HeroHeadBig_18_9ACCBB7F4F69AA4CADA5CA94E3788DB5",
                                    "HeroHeadSpuare_11_B4C0FC694F2D5538B14839BD2DCAA5B3")
                                && icon.TryGetValue(out FSoftObjectPath texturePath, "Image_2_BDA02B484B8F00FAFED6C0A9E2AF13EF")
                                && !texturePath.AssetPathName.IsNone)
                            {
                                return texturePath.AssetPathName.Text;
                            }

                            return "Marvel/Content/Marvel/UI/Textures/Gallery/Logo/img_gallery_insidepage_logo";
                        }

                        async Task<Dictionary<HeroKey, List<FStructFallback>>> GetSkinMap()
                        {
                            var dictionary = new Dictionary<HeroKey, List<FStructFallback>>();
                            var skinsTable = await UEParse.Provider.SafeLoadPackageObjectAsync<UDataTable>(
                                "Marvel/Content/Marvel/Data/DataTable/HeroGallery/UISkinTable");
                            if (skinsTable?.RowMap == null) return dictionary;
                            
                            foreach (var skin in skinsTable.RowMap.Values)
                            {
                                if (skin.TryGetValue(out FStructFallback identifier, "Identifier"))
                                {
                                    var key = new HeroKey(identifier);
                                    if (!dictionary.ContainsKey(key))
                                        dictionary[key] = [];
                                    
                                    dictionary[key].Add(skin);
                                }
                            }
                            return dictionary;
                        }
                        
                        var heroData =
                            await UEParse.Provider.SafeLoadPackageObjectAsync<UDataTable>(
                                "Marvel/Content/Marvel/Data/DataTable/HeroGallery/UIHeroTable");
                        
                        var skinMap = await GetSkinMap();
                         
                        if (heroData?.RowMap == null) return;

                        loader.TotalAssets = heroData.RowMap.Count();
                        foreach (var (key, value) in heroData.RowMap)
                        {
                            var heroBasic = value.GetOrDefault<FStructFallback>("HeroBasic_84_5082D460476D0C101A47818F6EE3DC2E");
                            var heroIcon = value.GetOrDefault<FStructFallback>("HeroHead_80_B82E1E9744B6FE24DF708982FF5B46D0");
                            var iconPath = GetDataTableIconPath(heroIcon);
                            
                            var assetArgs = new AssetItemCreationArgs
                            {
                                ID = key.Text,
                                DisplayName = heroBasic.GetOrDefault("TName_10_93EE6AC745A8786CA1DF5A83B5253AC4", new FText(key.Text)).Text.ToLower().TitleCase(),
                                Description = heroBasic.GetOrDefault("Desc_63_F34334EF45CD2DCEF0F5CEB7B7893F3F", new FText("No Description")).Text,
                                MainColor = heroBasic.GetOrDefault("HeroInfoMainColor_60_DF3A9B7B49FBF4A7F47FDCB06DADE676", new FLinearColor(1, 1, 1, 1)),
                                SecondaryColor = heroBasic.GetOrDefault("HeroInfoSecondaryColor_66_9A43BF184D53A7114048DBA131305FFB", new FLinearColor(0, 0, 0, 1)),
                                LowResIconPath = iconPath,
                                HighResIconPath = iconPath,
                                ExportType = EExportType.Outfit,
                            };
                            var assetItem = new AssetItem(assetArgs);
                            await assetItem.LoadBitmapAsync();
                            
                            if (skinMap.TryGetValue(new HeroKey(key.Text), out var skins))
                                assetItem.AssetInfo = new AssetInfo(assetItem, skins.ToArray());
                            else
                                assetItem.AssetInfo = new AssetInfo(assetItem);

                            loader.Source.AddOrUpdate(assetItem);
                            loader.LoadedAssets++;
                        }

                        loader.LoadedAssets = loader.TotalAssets;
                    }
                },
                // new AssetLoader(EExportType.Emote)
                // {
                //     ClassNames = ["DataTable"],
                //     AllowNames = ["EmoteResTable"]
                //     // TODO: 
                //     // Each EmoteResTable contains multiple emotes, separate into assets or keep as styles?
                //     // UIHeroEmoteTable - Emote names, universal emote style handling?
                //     // UI icon: Marvel/Content/Marvel/UI/Textures/Item/Emote/item_emote_<emoteID>.uasset
                //     // ,AssetHandler = async loader => { }
                // },
                // new AssetLoader(EExportType.Backpack) //Accessory
                // {
                //     ClassNames = ["AthenaLoadingScreenItemDefinition"]
                // },
                // new AssetLoader(EExportType.Emoticon) // Mood
                // {
                //     ClassNames = ["Texture2D"],
                //     AllowNames = ["item_mood", "img_mood"],
                //     DisallowedNames = ["Marvel_LQ"],
                //     DisplayNameHandler = asset => asset.Name,
                //     LowResIconHandler = asset => 
                //         UEParse.Provider.LoadPackageObject<UTexture2D>(asset.GetPathName().Replace("Marvel/UI", "Marvel_LQ/UI")),
                //     HighResIconHandler = asset => (UTexture2D) asset 
                // },
                // new AssetLoader(EExportType.Spray)
                // {
                //     ClassNames = ["Texture2D"],
                //     AllowNames = ["item_spray", "img_spray"],
                //     DisallowedNames = ["Marvel_LQ"],
                //     DisplayNameHandler = asset => asset.Name,
                //     LowResIconHandler = asset => 
                //         UEParse.Provider.LoadPackageObject<UTexture2D>(asset.GetPathName().Replace("Marvel/UI", "Marvel_LQ/UI")),
                //     HighResIconHandler = asset => (UTexture2D) asset 
                // },
                // new AssetLoader(EExportType.Banner) // Nameplate
                // {
                //     ClassNames = ["FortHomebaseBannerIconItemDefinition"],
                //     HideRarity = true
                //     // Export Nameplate
                //     // Icon Playerhead
                // },
            ]
        },
        // new(EAssetCategory.Gameplay)
        // {
        //     Loaders = 
        //     [
        //         new AssetLoader(EExportType.Item)
        //         {
        //             ClassNames = ["AthenaGadgetItemDefinition", "FortWeaponRangedItemDefinition", 
        //                 "FortWeaponMeleeItemDefinition", "FortCreativeWeaponMeleeItemDefinition", 
        //                 "FortCreativeWeaponRangedItemDefinition", "FortWeaponMeleeDualWieldItemDefinition"],
        //             HideNames = ["_Harvest", "Weapon_Pickaxe_", "Weapons_Pickaxe_", "Dev_WID"],
        //             HidePredicate = (loader, asset, name) =>
        //             {
        //                 if (loader.FilteredAssetBag.Contains(name)) return true;
        //                 loader.FilteredAssetBag.Add(name);
        //                 return false;
        //             },
        //             AddStyleHandler = (loader, asset, name) =>
        //             {
        //                 var path = asset.GetPathName();
        //                 loader.StyleDictionary.TryAdd(name, []);
        //                 loader.StyleDictionary[name].Add(path);
        //             }
        //         },
        //     ],
        // }
    ];
    
    public void Reset()
    {
        foreach (var loader in Categories.SelectMany(category => category.Loaders))
            loader.Reset();

        AssetItem.ResetCaches();

        ActiveLoader = null;
        ActiveCollection = new ReadOnlyObservableCollection<BaseAssetItem>([]);
    }

    public async Task Load(EExportType type)
    {
        if (type is EExportType.None) return;
        
        Set(type);
        await ActiveLoader.Load();
    }

    public AssetLoader Get(EExportType type)
    {
        if (!Enum.IsDefined(type))
            type = EExportType.Outfit;
        
        return Categories.SelectMany(cat => cat.Loaders).FirstOrDefault(loader => loader.Type == type) 
               ?? throw new ArgumentOutOfRangeException(nameof(type), $"Asset type {type.Description} does not have an implemented loader.");
    }
    
    public void Set(EExportType type)
    {
        Discord.Update(type);
        ActiveLoader = Get(type);
        ActiveCollection = ActiveLoader.Filtered;
        ActiveLoader.UpdateFilterVisibility();
    }

    private class HeroKey
    {
        private readonly string _heroID;
        private readonly string _shapeID;

        public HeroKey(FStructFallback identifier)
        {
            _heroID = identifier.Get<string>("HeroID");
            _shapeID = identifier.Get<string>("ShapeID");
        }

        public HeroKey(string heroID)
        {
            _heroID = heroID.Substring(0, 4);
            _shapeID = heroID.Substring(4, 1);
        }

        public override bool Equals(object? obj)
        {
            return obj is HeroKey other && _heroID.Equals(other._heroID) && _shapeID.Equals(other._shapeID);
        }

        public override int GetHashCode()
        {
            return (_heroID + _shapeID).GetHashCode();
        }
    }
}
