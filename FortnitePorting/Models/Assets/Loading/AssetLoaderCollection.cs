using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.i18N;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.GameplayTags;
using CUE4Parse.UE4.Objects.UObject;
using DynamicData;
using FluentAvalonia.Core;
using FluentAvalonia.UI.Controls;
using FortnitePorting.Application;
using FortnitePorting.Export.Custom;
using FortnitePorting.Export.Types;
using FortnitePorting.Models.Assets.Asset;
using FortnitePorting.Models.Assets.Custom;
using FortnitePorting.Services;
using FortnitePorting.Shared;
using FortnitePorting.Shared.Extensions;
using FortnitePorting.Shared.Services;
using SkiaSharp;

namespace FortnitePorting.Models.Assets.Loading;

public partial class AssetLoaderCollection : ObservableObject
{
    public static AssetLoaderCollection CategoryAccessor = new(false);
    
    public AssetLoader[] Loaders => Categories.SelectMany(category => category.Loaders).ToArray();
    
    public List<AssetLoaderCategory> Categories { get; set; } =
    [
        new AssetLoaderCategory(EAssetCategory.Cosmetics)
        {
            Loaders = 
            [
                new AssetLoader(EExportType.Outfit)
                {
                    //Marvel/Content/Marvel/Data/DataTable/MarvelHeroSkinTable.uasset - Styles and base meshes
                    //Marvel/Content/Marvel/Data/DataTable/MarvelHeroBaseAttributeTable.uasset - Translations?
                    
                    //Marvel/Content/Marvel/Data/DataTable/HeroGallery/UIHeroTable.uasset - UI Definitions: Name, Description, icon
                    //Marvel/Content/Marvel/Data/DataTable/HeroGallery/UISkinTable.uasset - Skin definitions: Link to actor, skin names
                    
                    ClassNames = ["Blueprint"],
                    AssetNames = ["001_ShowBP"],
                    PlaceholderIconPath = "Marvel/UI/Textures/Gallery/Logo/img_gallery_insidepage_logo",
                    LoadHiddenAssets = true,
                    IconHandler = asset =>
                    {
                        var iconName = "Marvel/Content/Marvel/UI/Textures/HeroPortrait/SelectHero/img_selecthero_" +
                                       asset.Name.Substring(9, 7);
                        CUE4ParseVM.Provider.TryLoadObject(iconName, out UTexture2D previewImage);

                        return previewImage;
                    },
                    AssetHandler = async loader =>
                    {
                        async Task<UTexture2D> GetDataTableIcon(FStructFallback iconStruct)
                        {
                            if (iconStruct.TryGetValue(out FStructFallback icon,
                                "HeroHeadBig_18_9ACCBB7F4F69AA4CADA5CA94E3788DB5",
                                "HeroHeadSpuare_11_B4C0FC694F2D5538B14839BD2DCAA5B3") 
                                && icon.TryGetValue(out FSoftObjectPath texturePath, "Image_2_BDA02B484B8F00FAFED6C0A9E2AF13EF")
                                && texturePath.TryLoad(out UTexture2D texture))
                            {
                                return texture;
                            }
                            return await CUE4ParseVM.Provider.TryLoadObjectAsync<UTexture2D>("Marvel/Content/Marvel/UI/Textures/Gallery/Logo/img_gallery_insidepage_logo");
                        }

                        async Task<Dictionary<HeroKey, List<FStructFallback>>> GetSkinMap()
                        {
                            var dictionary = new Dictionary<HeroKey, List<FStructFallback>>();
                            var skinsTable = await CUE4ParseVM.Provider.TryLoadObjectAsync<UDataTable>(
                                "Marvel/Content/Marvel/Data/DataTable/HeroGallery/UISkinTable");
                            if (skinsTable?.RowMap == null) return null;
                            
                            foreach (var skin in skinsTable.RowMap.Values)
                            {
                                if (skin.TryGetValue(out FStructFallback identifier, "Identifier"))
                                {
                                    var key = new HeroKey(identifier);
                                    if (!dictionary.ContainsKey(key))
                                        dictionary[key] = new List<FStructFallback>();
                                    
                                    dictionary[key].Add(skin);
                                }
                            }
                            return dictionary;
                        }
                        
                        var heroData =
                            await CUE4ParseVM.Provider.TryLoadObjectAsync<UDataTable>(
                                "Marvel/Content/Marvel/Data/DataTable/HeroGallery/UIHeroTable");
                        
                         // put in map by hero id, add before putting in source
                         var skinMap = await GetSkinMap();
                         
                        var finished = false;
                        if (heroData?.RowMap == null) return;

                        loader.TotalAssets = heroData.RowMap.Count();
                        foreach (var (key, value) in heroData.RowMap)
                        {
                            var heroBasic = value.GetOrDefault<FStructFallback>("HeroBasic_84_5082D460476D0C101A47818F6EE3DC2E");
                            var heroIcon = value.GetOrDefault<FStructFallback>("HeroHead_80_B82E1E9744B6FE24DF708982FF5B46D0");
                            
                            var assetArgs = new AssetItemCreationArgs()
                            {
                                ID = key.Text,
                                DisplayName = heroBasic.GetOrDefault("TName_10_93EE6AC745A8786CA1DF5A83B5253AC4", new FText(key.Text)).Text.ToLower().TitleCase(),
                                Description = heroBasic.GetOrDefault("Desc_63_F34334EF45CD2DCEF0F5CEB7B7893F3F", new FText("No Description")).Text,
                                MainColor = heroBasic.GetOrDefault("HeroInfoMainColor_60_DF3A9B7B49FBF4A7F47FDCB06DADE676", new FLinearColor(1, 1, 1, 1)),
                                SecondaryColor = heroBasic.GetOrDefault("HeroInfoMainColor_60_DF3A9B7B49FBF4A7F47FDCB06DADE676", new FLinearColor(0, 0, 0, 1)),
                                Icon = await GetDataTableIcon(heroIcon),
                                ExportType = EExportType.Outfit,
                            };
                            var assetItem = new AssetItem(assetArgs);
                            
                            //TODO: fix AssetInfo creation
                            if (skinMap.ContainsKey(new HeroKey(key.Text)))
                            {
                                var skins = skinMap[new HeroKey(key.Text)];
                                assetItem.AssetInfo = new AssetInfo(assetItem, skins.ToArray());
                            }
                            else
                            {
                                assetItem.AssetInfo = new AssetInfo(assetItem);
                            }

                            loader.Source.AddOrUpdate(assetItem);
                            loader.LoadedAssets++;
                        }

                        loader.LoadedAssets = loader.TotalAssets;
                        // HeroHeadBig_18_9ACCBB7F4F69AA4CADA5CA94E3788DB5 - Icon
                        // HeroBasic_84_5082D460476D0C101A47818F6EE3DC2E:
                        // TName_10_93EE6AC745A8786CA1DF5A83B5253AC4 - Display Name
                        // EnName_45_A241DED14FF7C14AD94F109AF1ECEF52 - Display Name
                        // Desc_63_F34334EF45CD2DCEF0F5CEB7B7893F3F - Description
                        // HeroInfoMainColor_60_DF3A9B7B49FBF4A7F47FDCB06DADE676 - Primary color
                        // HeroInfoSecondaryColor_66_9A43BF184D53A7114048DBA131305FFB - Secondary color

                    }
                },
                new AssetLoader(EExportType.Emoticon)
                {
                    ClassNames = ["AthenaEmojiItemDefinition"],
                    HideNames = ["Emoji_100APlus"]
                },
                new AssetLoader(EExportType.Spray)
                {
                    ClassNames = ["AthenaSprayItemDefinition"],
                    HideNames = ["SPID_000", "SPID_001"]
                },
                new AssetLoader(EExportType.Banner)
                {
                    ClassNames = ["FortHomebaseBannerIconItemDefinition"],
                    HideRarity = true
                },
                new AssetLoader(EExportType.LoadingScreen)
                {
                    ClassNames = ["AthenaLoadingScreenItemDefinition"]
                },
                new AssetLoader(EExportType.Emote)
                {
                    ClassNames = ["AthenaDanceItemDefinition"],
                    HideNames = ["_CT", "_NPC"]
                }
            ]
        },
        new AssetLoaderCategory(EAssetCategory.Gameplay)
        {
            Loaders = 
            [
                new AssetLoader(EExportType.Item)
                {
                    ClassNames = ["AthenaGadgetItemDefinition", "FortWeaponRangedItemDefinition", 
                        "FortWeaponMeleeItemDefinition", "FortCreativeWeaponMeleeItemDefinition", 
                        "FortCreativeWeaponRangedItemDefinition", "FortWeaponMeleeDualWieldItemDefinition"],
                    HideNames = ["_Harvest", "Weapon_Pickaxe_", "Weapons_Pickaxe_", "Dev_WID"],
                    HidePredicate = (loader, asset, name) =>
                    {
                        if (loader.FilteredAssetBag.Contains(name)) return true;
                        loader.FilteredAssetBag.Add(name);
                        return false;
                    },
                    AddStyleHandler = (loader, asset, name) =>
                    {
                        var path = asset.GetPathName();
                        loader.StyleDictionary.TryAdd(name, []);
                        loader.StyleDictionary[name].Add(path);
                    }
                },
            ],
        }
    ];
    
    [ObservableProperty] private ObservableCollection<NavigationViewItem> _navItems = [];
    [ObservableProperty] private NavigationViewItem _selectedNavItem;
    
    [ObservableProperty] private AssetLoader _activeLoader;
    [ObservableProperty] private ReadOnlyObservableCollection<Base.BaseAssetItem> _activeCollection;

    public AssetLoaderCollection(bool isForUi = true)
    {
        if (!isForUi) return;
        
        TaskService.RunDispatcher(() =>
        {
            foreach (var category in Categories)
            {
                NavItems.Add(new NavigationViewItem
                {
                    Tag = category.Category,
                    Content = category.Category.GetDescription(),
                    SelectsOnInvoked = false,
                    IconSource = new ImageIconSource
                    {
                        Source = ImageExtensions.AvaresBitmap($"avares://RivalsPorting/Assets/FN/{category.Category.ToString()}.png")
                    },
                    MenuItemsSource = category.Loaders.Select(loader => new NavigationViewItem
                    {
                        Tag = loader.Type, 
                        Content = loader.Type.GetDescription(), 
                        IconSource = new ImageIconSource
                        {
                            Source = ImageExtensions.AvaresBitmap($"avares://RivalsPorting/Assets/FN/{loader.Type.ToString()}.png")
                        },
                    })
                });
            }
        });
    }
    
    public async Task Load(EExportType type)
    {
        Set(type);
        await ActiveLoader.Load();
    }
    
    public void Set(EExportType type)
    {
        DiscordService.Update(type);
        ActiveLoader = Get(type);
        ActiveCollection = ActiveLoader.Filtered;
        ActiveLoader.UpdateFilterVisibility();
    }

    public AssetLoader Get(EExportType type)
    {
        foreach (var category in Categories)
        {
            if (category.Loaders.FirstOrDefault(loader => loader.Type == type) is { } assetLoader)
            {
                return assetLoader;
            }
        }

        return null!; // if this happens it's bc im stupid
    }
    
    private class HeroKey
    {
        private string _heroID { get; }
        private string _shapeID { get; }

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
            return obj != null && _heroID.Equals(((HeroKey)obj)._heroID) && _shapeID.Equals(((HeroKey)obj)._shapeID);
        }

        public override int GetHashCode()
        {
            return (_heroID + _shapeID).GetHashCode();
        }
    }
}