using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CUE4Parse_Conversion.Textures;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.i18N;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.UObject;
using DynamicData;
using RivalsPorting.Extensions;
using RivalsPorting.Framework;
using RivalsPorting.Models.Assets;
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
                        async Task<(
                            Dictionary<string, List<FStructFallback>> SkinsByHero,
                            Dictionary<string, Dictionary<(string SkinId, string ShapeId), FStructFallback>> SkinLookupByHero
                        )> GetSkinMaps()
                        {
                            var skinsByHero = new Dictionary<string, List<FStructFallback>>();
                            var skinLookupByHero = new Dictionary<string, Dictionary<(string SkinId, string ShapeId), FStructFallback>>();
                            var skinsTable = await UEParse.Provider.SafeLoadPackageObjectAsync<UDataTable>(
                                "Marvel/Content/Marvel/Data/DataTable/HeroGallery/UISkinTable");
                            if (skinsTable?.RowMap == null) return (skinsByHero, skinLookupByHero);

                            foreach (var skin in skinsTable.RowMap.Values)
                            {
                                if (!skin.TryGetValue(out FStructFallback identifier, "Identifier")) continue;

                                var heroId = identifier.GetOrDefault("HeroID", string.Empty);
                                var skinId = identifier.GetOrDefault("SkinID", string.Empty);
                                var shapeId = identifier.GetOrDefault("ShapeID", "0");
                                if (string.IsNullOrEmpty(heroId) || string.IsNullOrEmpty(skinId)) continue;

                                if (!skinLookupByHero.TryGetValue(heroId, out var heroLookup))
                                {
                                    heroLookup = new Dictionary<(string SkinId, string ShapeId), FStructFallback>();
                                    skinLookupByHero[heroId] = heroLookup;
                                }

                                heroLookup[(skinId, shapeId)] = skin;

                                // Skins channel lists shape-0 entries only (one row per SkinID).
                                if (shapeId == "0")
                                {
                                    if (!skinsByHero.TryGetValue(heroId, out var heroSkins))
                                    {
                                        heroSkins = [];
                                        skinsByHero[heroId] = heroSkins;
                                    }

                                    heroSkins.Add(skin);
                                }
                            }

                            return (skinsByHero, skinLookupByHero);
                        }

                        var heroData =
                            await UEParse.Provider.SafeLoadPackageObjectAsync<UDataTable>(
                                "Marvel/Content/Marvel/Data/DataTable/HeroGallery/UIHeroTable");

                        var (skinsByHero, skinLookupByHero) = await GetSkinMaps();

                        if (heroData?.RowMap == null) return;

                        var heroGroups = new Dictionary<string, List<(string RowKey, FStructFallback Value, string ShapeId)>>();
                        foreach (var (key, value) in heroData.RowMap)
                        {
                            var heroId = key.Text.Length >= 4 ? key.Text[..4] : key.Text;
                            var shapeId = value.GetOrDefault("ShapeID_74_5AF5149B4F1C6B5D135F8F8F98CDC0CB", 0).ToString();

                            if (!heroGroups.TryGetValue(heroId, out var group))
                            {
                                group = [];
                                heroGroups[heroId] = group;
                            }

                            group.Add((key.Text, value, shapeId));
                        }

                        loader.TotalAssets = heroGroups.Count;
                        foreach (var (heroId, shapes) in heroGroups)
                        {
                            var orderedShapes = shapes
                                .OrderBy(shape => shape.ShapeId, StringComparer.Ordinal)
                                .ToList();

                            var primary = orderedShapes.FirstOrDefault(shape => shape.ShapeId == "0");
                            if (primary.Value is null)
                                primary = orderedShapes[0];

                            var heroBasic = primary.Value.GetOrDefault<FStructFallback>("HeroBasic_84_5082D460476D0C101A47818F6EE3DC2E");
                            var heroIcon = primary.Value.GetOrDefault<FStructFallback>("HeroHead_80_B82E1E9744B6FE24DF708982FF5B46D0");
                            var iconPath = GetDataTableIconPath(heroIcon);

                            var assetArgs = new AssetItemCreationArgs
                            {
                                ID = primary.RowKey,
                                DisplayName = heroBasic.GetOrDefault("TName_10_93EE6AC745A8786CA1DF5A83B5253AC4", new FText(primary.RowKey)).Text.ToLower().TitleCase(),
                                Description = heroBasic.GetOrDefault("Desc_63_F34334EF45CD2DCEF0F5CEB7B7893F3F", new FText("No Description")).Text,
                                MainColor = heroBasic.GetOrDefault("HeroInfoMainColor_60_DF3A9B7B49FBF4A7F47FDCB06DADE676", new FLinearColor(1, 1, 1, 1)),
                                SecondaryColor = heroBasic.GetOrDefault("HeroInfoSecondaryColor_66_9A43BF184D53A7114048DBA131305FFB", new FLinearColor(0, 0, 0, 1)),
                                LowResIconPath = iconPath,
                                HighResIconPath = iconPath,
                                ExportType = EExportType.Outfit,
                            };
                            var assetItem = new AssetItem(assetArgs);
                            await assetItem.LoadBitmapAsync();

                            skinsByHero.TryGetValue(heroId, out var skins);
                            skins ??= [];
                            skinLookupByHero.TryGetValue(heroId, out var skinLookup);
                            skinLookup ??= new Dictionary<(string SkinId, string ShapeId), FStructFallback>();

                            // Ignore placeholder hero shapes that have no UISkinTable rows (e.g. Deadpool/Black Cat shape 1).
                            var shapesWithSkins = skinLookup.Keys
                                .Select(key => key.ShapeId)
                                .ToHashSet(StringComparer.Ordinal);
                            var validShapes = orderedShapes
                                .Where(shape => shapesWithSkins.Contains(shape.ShapeId))
                                .ToList();

                            // Skip shape 0 only when there are multiple other shapes (e.g. C&D 1+2, Banner 1-3).
                            // Heroes with a single alt form (e.g. Magik) keep shape 0 in Forms.
                            var nonZeroShapes = validShapes.Where(shape => shape.ShapeId != "0").ToList();
                            var formShapes = nonZeroShapes.Count > 1 ? nonZeroShapes : validShapes;

                            var formStyles = new List<FormStyleData>();
                            foreach (var shape in formShapes)
                            {
                                var shapeBasic = shape.Value.GetOrDefault<FStructFallback>("HeroBasic_84_5082D460476D0C101A47818F6EE3DC2E");
                                var shapeIcon = shape.Value.GetOrDefault<FStructFallback>("HeroHead_80_B82E1E9744B6FE24DF708982FF5B46D0");
                                var shapeName = shapeBasic.GetOrDefault(
                                    "TName_10_93EE6AC745A8786CA1DF5A83B5253AC4",
                                    new FText(shape.RowKey)).Text.ToLower().TitleCase();

                                Bitmap? formPreview = assetItem.IconDisplayImage;
                                var shapeIconPath = GetDataTableIconPath(shapeIcon);
                                if (await UEParse.Provider!.SafeLoadPackageObjectAsync<UTexture2D>(shapeIconPath) is { } shapeTexture
                                    && shapeTexture.Decode()?.ToWriteableBitmap() is { } shapeBitmap)
                                {
                                    formPreview = shapeBitmap;
                                }

                                formStyles.Add(new FormStyleData(shapeName, heroId, shape.ShapeId, formPreview));
                            }

                            assetItem.AssetInfo = formStyles.Count > 1 || skins.Count > 0
                                ? new AssetInfo(assetItem, skins.ToArray(), formStyles, skinLookup)
                                : new AssetInfo(assetItem);

                            loader.Source.AddOrUpdate(assetItem);
                            loader.LoadedAssets++;
                        }

                        loader.LoadedAssets = loader.TotalAssets;
                    }
                },
                new AssetLoader(EExportType.Emote)
                {
                    ClassNames = ["DataTable"],
                    PlaceholderIconPath = "Marvel/Content/Marvel/UI/Textures/Gallery/Logo/img_gallery_insidepage_logo",
                    LoadHiddenAssets = true,
                    AssetHandler = async loader =>
                    {
                        var emoteTable = await UEParse.Provider.SafeLoadPackageObjectAsync<UDataTable>(
                            "Marvel/Content/Marvel/Data/DataTable/UI/HeroSkin/UIHeroEmoteTable");
                        var heroData = await UEParse.Provider.SafeLoadPackageObjectAsync<UDataTable>(
                            "Marvel/Content/Marvel/Data/DataTable/HeroGallery/UIHeroTable");

                        if (emoteTable?.RowMap == null) return;

                        var emoteIconIndex = BuildEmoteIconIndex();

                        var heroLookup = new Dictionary<string, HeroDisplayInfo>();
                        if (heroData?.RowMap != null)
                        {
                            foreach (var (key, value) in heroData.RowMap)
                            {
                                var heroBasic = value.GetOrDefault<FStructFallback>("HeroBasic_84_5082D460476D0C101A47818F6EE3DC2E");
                                var heroIcon = value.GetOrDefault<FStructFallback>("HeroHead_80_B82E1E9744B6FE24DF708982FF5B46D0");
                                heroLookup[key.Text] = new HeroDisplayInfo(
                                    heroBasic.GetOrDefault("TName_10_93EE6AC745A8786CA1DF5A83B5253AC4", new FText(key.Text)).Text.ToLower().TitleCase(),
                                    GetDataTableIconPath(heroIcon),
                                    heroBasic.GetOrDefault("HeroInfoMainColor_60_DF3A9B7B49FBF4A7F47FDCB06DADE676", new FLinearColor(1, 1, 1, 1)),
                                    heroBasic.GetOrDefault("HeroInfoSecondaryColor_66_9A43BF184D53A7114048DBA131305FFB", new FLinearColor(0, 0, 0, 1))
                                );
                            }
                        }

                        var emoteGroups = new Dictionary<string, List<(string RowKey, FStructFallback Emote)>>();
                        foreach (var (key, value) in emoteTable.RowMap)
                        {
                            if (!value.TryGetValue(out FStructFallback identifier, "EmoteIdentifier")) continue;

                            var heroId = identifier.GetOrDefault("HeroID", string.Empty);
                            var skinId = identifier.GetOrDefault("SkinID", string.Empty);
                            var emoteId = identifier.GetOrDefault("EmoteID", string.Empty);
                            var groupKey = $"{heroId}{skinId}{emoteId}";

                            if (!emoteGroups.TryGetValue(groupKey, out var group))
                            {
                                group = [];
                                emoteGroups[groupKey] = group;
                            }

                            group.Add((key.Text, value));
                        }

                        loader.TotalAssets = emoteGroups.Count;
                        foreach (var (groupKey, entries) in emoteGroups)
                        {
                            var orderedEntries = entries
                                .OrderBy(entry =>
                                {
                                    if (entry.Emote.TryGetValue(out FStructFallback id, "EmoteIdentifier"))
                                        return id.GetOrDefault("ShapeID", "0");
                                    return "0";
                                })
                                .ToList();

                            var primary = orderedEntries[0];
                            if (!primary.Emote.TryGetValue(out FStructFallback primaryId, "EmoteIdentifier"))
                            {
                                loader.LoadedAssets++;
                                continue;
                            }

                            var primaryAnimPath = GetEmoteAnimationPath(primary.Emote);
                            var universalStyles = CollectUniversalEmoteStyles(primary.Emote, heroLookup);
                            if (primaryAnimPath is null
                                && !orderedEntries.Any(entry => GetEmoteAnimationPath(entry.Emote) is not null)
                                && universalStyles.Count == 0)
                            {
                                // Stub rows with no AnimMT/Anim (placeholder slots) — skip
                                loader.LoadedAssets++;
                                continue;
                            }

                            var heroId = primaryId.GetOrDefault("HeroID", string.Empty);
                            var emoteId = primaryId.GetOrDefault("EmoteID", string.Empty);
                            var isLobbyEmote = emoteId == "201";

                            heroLookup.TryGetValue($"{heroId}0", out var baseHero);
                            baseHero ??= heroLookup.GetValueOrDefault($"{heroId}{primaryId.GetOrDefault("ShapeID", "0")}");

                            string displayName;
                            string? lowResIconPath;
                            string? highResIconPath;
                            var mainColor = baseHero?.MainColor ?? new FLinearColor(1, 1, 1, 1);
                            var secondaryColor = baseHero?.SecondaryColor ?? new FLinearColor(0, 0, 0, 1);

                            if (isLobbyEmote)
                            {
                                var heroName = baseHero?.Name ?? primary.Emote.GetOrDefault("HeroName", heroId);
                                displayName = $"{heroName} - Lobby";
                                lowResIconPath = baseHero?.IconPath;
                                highResIconPath = baseHero?.IconPath;
                            }
                            else
                            {
                                displayName = ResolveEmoteDisplayName(
                                    primary.Emote.GetOrDefault<FText?>("EmoteName"),
                                    primary.RowKey);

                                if (emoteIconIndex.TryGetValue(groupKey, out var indexedIcons))
                                {
                                    lowResIconPath = indexedIcons.LowRes ?? indexedIcons.HighRes;
                                    highResIconPath = indexedIcons.HighRes ?? indexedIcons.LowRes;
                                }
                                else
                                {
                                    lowResIconPath = $"Marvel/Content/Marvel_LQ/UI/Textures/Item/Emote/item_emote_{groupKey}";
                                    highResIconPath = $"Marvel/Content/Marvel/UI/Textures/Item/Emote/item_emote_{groupKey}";
                                }
                            }

                            primaryAnimPath ??= orderedEntries
                                .Select(entry => GetEmoteAnimationPath(entry.Emote))
                                .FirstOrDefault(path => path is not null);
                            primaryAnimPath ??= universalStyles.FirstOrDefault()?.AnimPath;

                            var assetArgs = new AssetItemCreationArgs
                            {
                                ID = primary.RowKey,
                                DisplayName = displayName,
                                Description = string.Empty,
                                MainColor = mainColor,
                                SecondaryColor = secondaryColor,
                                LowResIconPath = lowResIconPath,
                                HighResIconPath = highResIconPath,
                                ExportType = EExportType.Emote,
                                ObjectPath = primaryAnimPath
                            };

                            var assetItem = new AssetItem(assetArgs);
                            await assetItem.LoadBitmapAsync();

                            var styleDatas = new List<BaseStyleData>();
                            if (orderedEntries.Count > 1)
                            {
                                foreach (var (_, emote) in orderedEntries)
                                {
                                    if (!emote.TryGetValue(out FStructFallback identifier, "EmoteIdentifier")) continue;

                                    var shapeId = identifier.GetOrDefault("ShapeID", "0");
                                    var shapeHeroKey = $"{identifier.GetOrDefault("HeroID", string.Empty)}{shapeId}";
                                    var styleName = heroLookup.TryGetValue(shapeHeroKey, out var shapeHero)
                                        ? shapeHero.Name
                                        : shapeId;

                                    var animPath = GetEmoteAnimationPath(emote);
                                    if (animPath is null) continue;

                                    styleDatas.Add(new SoftAnimStyleData(styleName, animPath));
                                }
                            }
                            else if (universalStyles.Count > 0)
                            {
                                styleDatas.AddRange(universalStyles);
                            }

                            if (assetItem.CreationData.ObjectPath is null && styleDatas.Count > 0)
                                assetItem.CreationData.ObjectPath = ((SoftAnimStyleData) styleDatas[0]).AnimPath;

                            assetItem.AssetInfo = styleDatas.Count > 1
                                ? new AssetInfo(assetItem, styleDatas, orderedEntries.Count > 1 ? "Styles" : "Heroes")
                                : new AssetInfo(assetItem);

                            loader.Source.AddOrUpdate(assetItem);
                            loader.LoadedAssets++;
                        }

                        loader.LoadedAssets = loader.TotalAssets;
                    }
                },
                // new AssetLoader(EExportType.Backpack) //Accessory
                // {
                //     ClassNames = ["AthenaLoadingScreenItemDefinition"]
                // },
                new AssetLoader(EExportType.Emoticon) // Mood
                {
                    PlaceholderIconPath = "Marvel/Content/Marvel/UI/Textures/Gallery/Logo/img_gallery_insidepage_logo",
                    LoadHiddenAssets = true,
                    AssetHandler = async loader =>
                    {
                        // Moods are raw Texture2Ds — not registered as item defs in the Asset Registry.
                        // Index package files by name (same approach as emote icons).
                        await LoadTextureFileCatalogAsync(
                            loader,
                            EExportType.Emoticon,
                            namePrefixes: ["item_mood_", "img_mood_", "mood_"],
                            skipNameContains: ["_bg"]);
                    }
                },
                new AssetLoader(EExportType.Spray)
                {
                    PlaceholderIconPath = "Marvel/Content/Marvel/UI/Textures/Gallery/Logo/img_gallery_insidepage_logo",
                    LoadHiddenAssets = true,
                    AssetHandler = async loader =>
                    {
                        var sprayTable = await UEParse.Provider.SafeLoadPackageObjectAsync<UDataTable>(
                            "Marvel/Content/Marvel/Data/DataTable/UI/HeroSkin/UIHeroSprayTable");
                        if (sprayTable?.RowMap == null)
                        {
                            // Fallback: scan packages if the table is missing.
                            await LoadTextureFileCatalogAsync(
                                loader,
                                EExportType.Spray,
                                namePrefixes: ["item_spray_", "img_spray_"]);
                            return;
                        }

                        loader.TotalAssets = sprayTable.RowMap.Count;
                        foreach (var (key, value) in sprayTable.RowMap)
                        {
                            if (!value.TryGetValue(out FSoftObjectPath sprayPath, "Spray")
                                || sprayPath.AssetPathName.IsNone
                                || string.IsNullOrEmpty(sprayPath.AssetPathName.Text))
                            {
                                loader.LoadedAssets++;
                                continue;
                            }

                            var highResPath = sprayPath.AssetPathName.Text;
                            var lowResPath = ToLowResUiPath(highResPath);
                            var displayName = ResolveTextureTableName(value, key.Text, "img_spray_", "item_spray_");

                            var assetArgs = new AssetItemCreationArgs
                            {
                                ID = key.Text,
                                DisplayName = displayName,
                                Description = string.Empty,
                                MainColor = new FLinearColor(1, 1, 1, 1),
                                SecondaryColor = new FLinearColor(0, 0, 0, 1),
                                LowResIconPath = lowResPath,
                                HighResIconPath = highResPath,
                                ExportType = EExportType.Spray,
                                ObjectPath = highResPath
                            };

                            var assetItem = new AssetItem(assetArgs);
                            await assetItem.LoadBitmapAsync();
                            assetItem.AssetInfo = new AssetInfo(assetItem);

                            loader.Source.AddOrUpdate(assetItem);
                            loader.LoadedAssets++;
                        }

                        loader.LoadedAssets = loader.TotalAssets;
                    }
                },
                new AssetLoader(EExportType.Banner) // Nameplate
                {
                    PlaceholderIconPath = "Marvel/Content/Marvel/UI/Textures/Gallery/Logo/img_gallery_insidepage_logo",
                    LoadHiddenAssets = true,
                    HideRarity = true,
                    AssetHandler = async loader =>
                    {
                        var nameplateTable = await UEParse.Provider.SafeLoadPackageObjectAsync<UDataTable>(
                            "Marvel/Content/Marvel/Data/DataTable/UI/HeroSkin/UIHeroNameplateTable");
                        if (nameplateTable?.RowMap == null) return;

                        loader.TotalAssets = nameplateTable.RowMap.Count;
                        foreach (var (key, value) in nameplateTable.RowMap)
                        {
                            // Export the nameplate strip; show the playerhead as the grid icon.
                            var nameplatePath = GetSoftObjectPathText(value, "SlimTexture");
                            if (nameplatePath is null)
                            {
                                loader.LoadedAssets++;
                                continue;
                            }

                            var iconPath = GetSoftObjectPathText(value, "Avatar") ?? nameplatePath;
                            var displayName = ResolveTextureTableName(
                                value, key.Text, "img_nameplate_", "img_playerhead_");

                            var assetArgs = new AssetItemCreationArgs
                            {
                                ID = key.Text,
                                DisplayName = displayName,
                                Description = string.Empty,
                                MainColor = value.GetOrDefault("BgColor", new FLinearColor(1, 1, 1, 1)),
                                SecondaryColor = new FLinearColor(0, 0, 0, 1),
                                LowResIconPath = ToLowResUiPath(iconPath),
                                HighResIconPath = iconPath,
                                ExportType = EExportType.Banner,
                                ObjectPath = nameplatePath
                            };

                            var assetItem = new AssetItem(assetArgs);
                            await assetItem.LoadBitmapAsync();

                            var styleDatas = new List<BaseStyleData>
                            {
                                new SoftTextureStyleData(
                                    "Nameplate",
                                    nameplatePath,
                                    await LoadTexturePreviewAsync(nameplatePath) ?? assetItem.IconDisplayImage)
                            };

                            var avatarPath = GetSoftObjectPathText(value, "Avatar");
                            if (avatarPath is not null)
                            {
                                styleDatas.Add(new SoftTextureStyleData(
                                    "Playerhead",
                                    avatarPath,
                                    assetItem.IconDisplayImage));
                            }

                            assetItem.AssetInfo = styleDatas.Count > 1
                                ? new AssetInfo(assetItem, styleDatas, "Texture")
                                : new AssetInfo(assetItem);

                            loader.Source.AddOrUpdate(assetItem);
                            loader.LoadedAssets++;
                        }

                        loader.LoadedAssets = loader.TotalAssets;
                    }
                },
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

    private static string GetDataTableIconPath(FStructFallback iconStruct)
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

    private static Dictionary<string, EmoteIconPaths> BuildEmoteIconIndex()
    {
        var index = new Dictionary<string, EmoteIconPaths>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in UEParse.Provider.Files.Values)
        {
            var name = file.NameWithoutExtension;
            string id;
            if (name.StartsWith("item_emote_", StringComparison.OrdinalIgnoreCase))
                id = name["item_emote_".Length..];
            else if (name.StartsWith("item_emoto_", StringComparison.OrdinalIgnoreCase))
                id = name["item_emoto_".Length..];
            else
                continue;

            if (string.IsNullOrEmpty(id)) continue;

            var path = file.PathWithoutExtension;
            var isLq = path.Contains("Marvel_LQ", StringComparison.OrdinalIgnoreCase);
            var isStandardLocation = path.Contains("/Item/Emote/", StringComparison.OrdinalIgnoreCase);

            if (!index.TryGetValue(id, out var existing))
                existing = new EmoteIconPaths();

            if (isLq)
                PreferEmoteIconPath(ref existing.LowRes, path, isStandardLocation);
            else
                PreferEmoteIconPath(ref existing.HighRes, path, isStandardLocation);

            index[id] = existing;
        }

        return index;
    }

    private static void PreferEmoteIconPath(ref string? current, string candidate, bool candidateIsStandard)
    {
        if (current is null)
        {
            current = candidate;
            return;
        }

        var currentIsStandard = current.Contains("/Item/Emote/", StringComparison.OrdinalIgnoreCase);
        if (!currentIsStandard && candidateIsStandard)
            current = candidate;
    }

    private sealed class EmoteIconPaths
    {
        public string? LowRes;
        public string? HighRes;
    }

    private static string? GetEmoteAnimationPath(FStructFallback emote)
    {
        if (emote.TryGetValue(out FSoftObjectPath animMT, "AnimMT")
            && !animMT.AssetPathName.IsNone
            && !string.IsNullOrEmpty(animMT.AssetPathName.Text))
        {
            return animMT.AssetPathName.Text;
        }

        if (emote.TryGetValue(out FSoftObjectPath anim, "Anim")
            && !anim.AssetPathName.IsNone
            && !string.IsNullOrEmpty(anim.AssetPathName.Text))
        {
            return anim.AssetPathName.Text;
        }

        return null;
    }

    private static List<SoftAnimStyleData> CollectUniversalEmoteStyles(
        FStructFallback emote,
        Dictionary<string, HeroDisplayInfo> heroLookup)
    {
        var styles = new List<SoftAnimStyleData>();
        if (!emote.TryGetValue(out FStructFallback[] heroAnims, "HeroUniversalAnims")
            || heroAnims.Length == 0)
        {
            return styles;
        }

        foreach (var heroAnim in heroAnims)
        {
            var heroId = GetEmoteHeroId(heroAnim);
            if (string.IsNullOrEmpty(heroId)) continue;
            if (!heroAnim.TryGetValue(out FStructFallback[] shapeAnims, "ShapeUniversalAnims"))
                continue;

            foreach (var shapeAnim in shapeAnims)
            {
                var animPath = GetEmoteAnimationPath(shapeAnim);
                if (animPath is null) continue;

                var shapeId = shapeAnim.TryGetValue(out int shapeIdInt, "ShapeID")
                    ? shapeIdInt.ToString()
                    : shapeAnim.GetOrDefault("ShapeID", "0");

                var styleName = heroLookup.TryGetValue($"{heroId}{shapeId}", out var shapeHero)
                    ? shapeHero.Name
                    : heroLookup.TryGetValue($"{heroId}0", out var baseHero)
                        ? baseHero.Name
                        : heroId;

                styles.Add(new SoftAnimStyleData(styleName, animPath));
            }
        }

        return styles;
    }

    private static string GetEmoteHeroId(FStructFallback heroAnim)
    {
        if (heroAnim.TryGetValue(out string heroId, "HeroID") && !string.IsNullOrEmpty(heroId))
            return heroId;
        if (heroAnim.TryGetValue(out int heroIdInt, "HeroID"))
            return heroIdInt.ToString();
        return string.Empty;
    }

    private static string ResolveEmoteDisplayName(FText? emoteName, string fallback)
    {
        var text = emoteName?.Text;
        if (string.IsNullOrWhiteSpace(text))
            return fallback;

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && TryDecodeHexAsciiName(text, out var decoded))
        {
            return decoded.ToLower().TitleCase();
        }

        return text;
    }

    private static bool TryDecodeHexAsciiName(string hexText, out string decoded)
    {
        decoded = string.Empty;
        var hex = hexText.AsSpan(2);
        if (hex.Length == 0 || hex.Length % 2 != 0)
            return false;

        Span<char> chars = stackalloc char[hex.Length / 2];
        var count = 0;
        for (var i = 0; i < hex.Length; i += 2)
        {
            if (!byte.TryParse(hex.Slice(i, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
                return false;
            if (b is < 32 or > 126) continue;
            chars[count++] = (char) b;
        }

        if (count == 0) return false;
        decoded = new string(chars[..count]);
        return true;
    }

    private static async Task LoadTextureFileCatalogAsync(
        AssetLoader loader,
        EExportType exportType,
        string[] namePrefixes,
        string[]? skipNameContains = null)
    {
        var entries = new Dictionary<string, TextureCatalogEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in UEParse.Provider.Files.Values)
        {
            var name = file.NameWithoutExtension;
            if (!namePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                continue;
            if (skipNameContains is not null
                && skipNameContains.Any(skip => name.Contains(skip, StringComparison.OrdinalIgnoreCase)))
                continue;

            var path = file.PathWithoutExtension;
            var isLq = path.Contains("Marvel_LQ", StringComparison.OrdinalIgnoreCase);

            if (!entries.TryGetValue(name, out var existing))
                existing = new TextureCatalogEntry { Id = name };

            if (isLq)
                PreferTextureCatalogPath(ref existing.LowRes, path, namePrefixes);
            else
                PreferTextureCatalogPath(ref existing.HighRes, path, namePrefixes);

            entries[name] = existing;
        }

        loader.TotalAssets = entries.Count;
        foreach (var entry in entries.Values)
        {
            var highResPath = entry.HighRes ?? entry.LowRes;
            if (highResPath is null)
            {
                loader.LoadedAssets++;
                continue;
            }

            var lowResPath = entry.LowRes ?? ToLowResUiPath(highResPath);
            var assetArgs = new AssetItemCreationArgs
            {
                ID = entry.Id,
                DisplayName = FormatTextureDisplayName(entry.Id, namePrefixes),
                Description = string.Empty,
                MainColor = new FLinearColor(1, 1, 1, 1),
                SecondaryColor = new FLinearColor(0, 0, 0, 1),
                LowResIconPath = lowResPath,
                HighResIconPath = highResPath,
                ExportType = exportType,
                ObjectPath = highResPath
            };

            var assetItem = new AssetItem(assetArgs);
            await assetItem.LoadBitmapAsync();
            assetItem.AssetInfo = new AssetInfo(assetItem);

            loader.Source.AddOrUpdate(assetItem);
            loader.LoadedAssets++;
        }

        loader.LoadedAssets = loader.TotalAssets;
    }

    private static void PreferTextureCatalogPath(ref string? current, string candidate, string[] namePrefixes)
    {
        if (current is null)
        {
            current = candidate;
            return;
        }

        var currentScore = ScoreTextureCatalogPath(current, namePrefixes);
        var candidateScore = ScoreTextureCatalogPath(candidate, namePrefixes);
        if (candidateScore > currentScore)
            current = candidate;
    }

    private static int ScoreTextureCatalogPath(string path, string[] namePrefixes)
    {
        var score = 0;
        if (path.Contains("/Item/", StringComparison.OrdinalIgnoreCase)) score += 2;
        if (path.Contains("/Show/", StringComparison.OrdinalIgnoreCase)) score += 1;
        // Prefer inventory icons (item_*) when multiple prefixes match the same file set.
        if (namePrefixes.Any(prefix =>
                prefix.StartsWith("item_", StringComparison.OrdinalIgnoreCase)
                && path.Contains(prefix.TrimEnd('_'), StringComparison.OrdinalIgnoreCase)))
            score += 1;
        return score;
    }

    private static string ToLowResUiPath(string path)
    {
        if (path.Contains("/Marvel_LQ/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("Marvel_LQ/", StringComparison.OrdinalIgnoreCase))
            return path;

        var replaced = path
            .Replace("/Marvel/UI/", "/Marvel_LQ/UI/", StringComparison.OrdinalIgnoreCase)
            .Replace("Marvel/Content/Marvel/UI/", "Marvel/Content/Marvel_LQ/UI/", StringComparison.OrdinalIgnoreCase);
        return replaced;
    }

    private static string ResolveTextureTableName(FStructFallback row, string fallbackId, params string[] prefixes)
    {
        if (row.TryGetValue(out FText nameText, "Name") && !string.IsNullOrWhiteSpace(nameText.Text))
            return nameText.Text;
        if (row.TryGetValue(out string nameStr, "Name") && !string.IsNullOrWhiteSpace(nameStr))
            return nameStr;

        return FormatTextureDisplayName(fallbackId, prefixes);
    }

    private static string? GetSoftObjectPathText(FStructFallback row, string propertyName)
    {
        if (!row.TryGetValue(out FSoftObjectPath softPath, propertyName)
            || softPath.AssetPathName.IsNone
            || string.IsNullOrEmpty(softPath.AssetPathName.Text))
        {
            return null;
        }

        return softPath.AssetPathName.Text;
    }

    private static async Task<Bitmap?> LoadTexturePreviewAsync(string path)
    {
        if (await UEParse.Provider.SafeLoadPackageObjectAsync<UTexture2D>(path) is not { } texture)
            return null;

        return texture.Decode()?.ToWriteableBitmap();
    }

    private static string FormatTextureDisplayName(string id, params string[] prefixes)
    {
        var name = id;
        foreach (var prefix in prefixes.OrderByDescending(p => p.Length))
        {
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            name = name[prefix.Length..];
            break;
        }

        if (string.IsNullOrWhiteSpace(name))
            return id;

        return name.Replace('_', ' ').ToLower().TitleCase();
    }

    private sealed class TextureCatalogEntry
    {
        public required string Id;
        public string? LowRes;
        public string? HighRes;
    }

    private sealed record HeroDisplayInfo(string Name, string IconPath, FLinearColor MainColor, FLinearColor SecondaryColor);
}
