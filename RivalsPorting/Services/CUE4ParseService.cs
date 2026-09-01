using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CUE4Parse_Conversion.Textures;
using CUE4Parse_Conversion.Textures.BC;
using CUE4Parse.Compression;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.MappingsProvider;
using CUE4Parse.MappingsProvider.Usmap;
using CUE4Parse.UE4.AssetRegistry;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.IO;
using CUE4Parse.UE4.Objects.Core.i18N;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Pak;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;
using CUE4Parse.UE4.VirtualFileSystem;
using CUE4Parse.Utils;
using EpicManifestParser;
using EpicManifestParser.UE;
using RivalsPorting.Exporting;
using RivalsPorting.CUE4Parse.Models.Fortnite.Styles;
using RivalsPorting.Extensions;
using RivalsPorting.Framework;
using RivalsPorting.Models.API.Responses;
using RivalsPorting.Models.CUE4Parse;
using RivalsPorting.Models.Information;
using RivalsPorting.Shared.Extensions;
using RivalsPorting.Views;
using RivalsPorting.Views.Settings;
using Serilog;
using UE4Config.Parsing;
using FGuid = CUE4Parse.UE4.Objects.Core.Misc.FGuid;

namespace RivalsPorting.Services;

public partial class CUE4ParseService : ObservableObject, IService, IResettable
{
    [ObservableProperty] private string _status = "Loading Files";
    [ObservableProperty] private bool _finishedLoading;
    [ObservableProperty] private float _progress = 0.0f;
    [ObservableProperty] private bool _isLoading;
    public HybridFileProvider? Provider;

    public FBuildPatchAppManifest? LiveManifest;
    
    public readonly List<FPartialAssetData> AssetRegistry = [];
    public readonly List<FRarityCollection> RarityColors = [];
    public readonly Dictionary<int, FColor> BeanstalkColors = [];
    public readonly Dictionary<int, FLinearColor> BeanstalkMaterialProps = [];
    public readonly Dictionary<int, FVector> BeanstalkAtlasTextureUVs = [];
    public readonly List<UAnimMontage> MaleLobbyMontages = [];
    public readonly List<UAnimMontage> FemaleLobbyMontages = [];
    public readonly Dictionary<string, string> SetNames = [];
    
    private static readonly List<string> MaleLobbyMontagePaths = 
    [
        "FortniteGame/Content/Animation/Game/MainPlayer/Menu/BR/Male_Commando_Idle_01_M",
        "FortniteGame/Content/Animation/Game/MainPlayer/Menu/BR/Male_commando_Idle_2_M",
        "FortniteGame/Plugins/GameFeatures/BRCosmetics/Content/Animation/Game/MainPlayer/Menu/BR/Male_commando_Idle_01_M",
        "FortniteGame/Plugins/GameFeatures/BRCosmetics/Content/Animation/Game/MainPlayer/Menu/BR/Male_commando_Idle_2_M"
    ];
    
    private static readonly List<string> FemaleLobbyMontagePaths = 
    [
        "FortniteGame/Content/Animation/Game/MainPlayer/Menu/BR/Female_Commando_Idle_02_Rebirth_Montage",
        "FortniteGame/Content/Animation/Game/MainPlayer/Menu/BR/Female_Commando_Idle_03_Montage",
        "FortniteGame/Plugins/GameFeatures/BRCosmetics/Content/Animation/Game/MainPlayer/Menu/BR/Female_Commando_Idle_02_Rebirth_Montage"
    ];

    private const EGame LATEST_GAME_VERSION = EGame.GAME_MarvelRivals;
    
    public DirectoryInfo CacheFolder => new(Path.Combine(App.ApplicationDataFolder.FullName, ".cache"));

    public CUE4ParseService()
    {
        CacheFolder.Create();
    }

    public async Task Initialize()
    {
        if (!HasValidArchivePath())
        {
            Info.Dialog("Invalid Installation Settings", "The archive directory set in Installation Settings does not exist or is empty. Please set it to your Rivals installation's archive directory (generally located at RivalsGame/Content/Paks).", buttons:
            [
                new DialogButton
                {
                    Text = "Open Installation Settings",
                    Action = () => TaskService.Run(async () =>
                    {
                        Navigation.App.Open<SettingsView>();
                        await Task.Delay(250);
                        Navigation.Settings.Open<InstallationSettingsView>();
                    })
                }
            ]);
            
            return;
        }
        
        var stages = GetType()
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Select(m => (Method: m, Attr: m.GetCustomAttribute<LoadingStageAttribute>()))
            .Where(x => x.Attr is not null)
            .OrderBy(x => x.Attr!.Stage)
            .Select(x => new LoadingStage(x.Method, x.Attr!))
            .ToList();
        
        var totalWeight = stages.Sum(x => x.Attr.Weight);
        var completedWeight = 0.0f;

        foreach (var stage in stages)
        {
            UpdateStatus(stage.Attr.Name);
            
            completedWeight += stage.Attr.Weight;
            Progress = (completedWeight / totalWeight) * 100.0f;
            
            if (stage.Method.Invoke(this, null) is not Task stageTask)
                continue;

            await stageTask;
        }

        UpdateStatus(string.Empty);
        FinishedLoading = true;
        Progress = 0;
    }

    public void Reset()
    {
        FinishedLoading = false;
        Progress = 0;
        Status = "Loading Files";

        Provider?.Dispose();
        Provider = null;
        LiveManifest = null;

        AssetRegistry.Clear();
        RarityColors.Clear();
        BeanstalkColors.Clear();
        BeanstalkMaterialProps.Clear();
        BeanstalkAtlasTextureUVs.Clear();
        MaleLobbyMontages.Clear();
        FemaleLobbyMontages.Clear();
        SetNames.Clear();
    }

    public async Task LoadCoreSessionAsync()
    {
        IsLoading = true;
        await Initialize();
        IsLoading = false;

        if (!FinishedLoading) return;

        if (AppSettings.Application.UseDefaultExportLoadType)
            await AssetLoading.Load(AppSettings.Application.DefaultExportLoadType);

        Files.Initialize();
        await FilesVM.Initialize();
    }

    public void UpdateStatus(string status)
    {
        Status = status;
        if (!string.IsNullOrEmpty(status))
            Log.Information("[STATUS] {status}", status);
    }

    private bool HasValidArchivePath()
    {
        return AppSettings.Installation.CurrentProfile.RivalsVersion switch
        {
            ERivalsVersion.LatestInstalled or ERivalsVersion.Custom => Directory.Exists(AppSettings.Installation.CurrentProfile.ArchiveDirectory),
            _ => true
        };
    }

    [LoadingStage("Initializing CUE4Parse", stage: 0, weight: 5)]
    private async Task InitializeProviderSetup()
    {
        Provider = AppSettings.Installation.CurrentProfile.RivalsVersion switch
        {
            ERivalsVersion.LatestInstalled => new HybridFileProvider(AppSettings.Installation.CurrentProfile.ArchiveDirectory, [], new VersionContainer(LATEST_GAME_VERSION)),
            _ => new HybridFileProvider(AppSettings.Installation.CurrentProfile.ArchiveDirectory, [], new VersionContainer(AppSettings.Installation.CurrentProfile.UnrealVersion)),
        };
        
        Log.Information("Installation Type: {Type}", AppSettings.Installation.CurrentProfile.RivalsVersion);
        Log.Information("Archive Path: {Path}", AppSettings.Installation.CurrentProfile.ArchiveDirectory);
        Log.Information("Unreal Version: {Version}", Provider.Versions.Game.ToString());
        Log.Information("Texture Streaming: {UseTextureStreaming}", AppSettings.Installation.CurrentProfile.UseTextureStreaming);
        
        ObjectTypeRegistry.RegisterEngine(Assembly.Load("RivalsPorting"));

        Provider.LoadOnDemandTocs = AppSettings.Installation.CurrentProfile is { TextureStreamingEnabled: true, UseTextureStreaming: true };
        Provider.LoadExtraDirectories = AppSettings.Installation.CurrentProfile.LoadInstalledBundles;
        Provider.ReadNaniteData = AppSettings.Installation.CurrentProfile.LoadNaniteData;

        Provider.VfsMounted += (sender, _) =>
        {
            if (sender is not IAesVfsReader reader) return;

            UpdateStatus(reader.Name.Equals("plugin.utoc")
                ? $"Loading GameFeature {reader.Path.SubstringBeforeLast("\\").SubstringAfterLast("\\")}"
                : $"Loading {reader.Name}");
        };
    }

    [LoadingStage("Checking for Valid Keys", stage: 1, weight: 1)]
    private async Task CheckBlackHole()
    {
        if (AppSettings.Installation.CurrentProfile.RivalsVersion is not ERivalsVersion.LatestInstalled) return;
        
        var mainPakPath = Path.Combine(AppSettings.Installation.CurrentProfile.ArchiveDirectory,
            "pakchunk0-WindowsClient.pak");
        if (!File.Exists(mainPakPath)) return;

        var mainPakReader = new PakFileReader(mainPakPath);
        if (mainPakReader.TestAesKey(new FAesKey(Globals.LATEST_AES)))
        {
            Log.Information("Main key {Key} succeeded on pak {PakName}", Globals.LATEST_AES, mainPakPath);
            return;
        }
        
        BlackHole.Open(isMinigame: false);
    }
    
    [LoadingStage("Removing Outdated Cache Files", stage: 2, weight: 1)]
    private async Task CleanupCache()
    {
        var files = CacheFolder.GetFiles();

        var cutoffDate = DateTime.Now - TimeSpan.FromDays(AppSettings.Developer.ChunkCacheLifetime);
        foreach (var file in files)
        {
            if (file.LastWriteTime >= cutoffDate) continue;
            
            file.Delete();
        }
    }
    
    [LoadingStage("Loading Oodle", stage: 3, weight: 1)]
    private async Task InitializeOodle()
    {
        if (!File.Exists(Dependencies.OodleFile.FullName))
        {
            var downloadPath = Dependencies.OodleFile.FullName;
            await OodleHelper.DownloadOodleDllAsync(ref downloadPath);
        }
        
        await OodleHelper.InitializeAsync(Dependencies.OodleFile.FullName);
    }
    
    [LoadingStage("Loading Zlib", stage: 4, weight: 1)]
    private async Task InitializeZlib()
    {
        var zlibPath = Path.Combine(App.DataFolder.FullName, ZlibHelper.DLL_NAME);
        if (!File.Exists(zlibPath)) await ZlibHelper.DownloadDllAsync(zlibPath);
        
        await ZlibHelper.InitializeAsync(zlibPath);
    }
    
    [LoadingStage("Loading Detex", stage: 5, weight: 1)]
    private async Task InitializeDetex()
    {
        var detexPath = Path.Combine(App.DataFolder.FullName, DetexHelper.DLL_NAME);
        if (!File.Exists(detexPath)) await DetexHelper.LoadDllAsync(detexPath);
        DetexHelper.Initialize(detexPath);
    }
    
    [LoadingStage("Initializing Provider", stage: 6, weight: 10)]
    private async Task InitializeProvider()
    {
        await Provider.InitializeAsync();
    }

    // [LoadingStage("Loading Texture Streaming", stage: 7, weight: 5)]
    // private async Task InitializeTextureStreaming()
    // {
    //     if (AppSettings.Installation.CurrentProfile.RivalsVersion is not (ERivalsVersion.LatestInstalled or ERivalsVersion.LatestOnDemand)) return;
    //     if (!AppSettings.Installation.CurrentProfile.UseTextureStreaming) return;
    //
    //     try
    //     {
    //         var tocPath = await GetTocPath(AppSettings.Installation.CurrentProfile.RivalsVersion);
    //         if (string.IsNullOrEmpty(tocPath)) return;
    //         
    //         Log.Information("Found toc path: {tocPath}", tocPath);
    //
    //         var tocName = tocPath.SubstringAfterLast("/");
    //         var onDemandFile = new FileInfo(Path.Combine(CacheFolder.FullName, tocName));
    //         if (!onDemandFile.Exists || onDemandFile.Length == 0)
    //         {
    //             await Api.DownloadFileAsync($"https://download.epicgames.com/{tocPath}", onDemandFile.FullName);
    //         }
    //         
    //         await Provider.RegisterVfsAsync(new IoChunkToc(onDemandFile.FullName, Provider.Versions));
    //         await Provider.MountAsync();
    //     }
    //     catch (Exception e)
    //     {
    //         Info.Message("Failed to Initialize Texture Streaming", 
    //             $"Please enable the \"Pre-Download Streamed Assets\" option for Rivals in the Epic Games Launcher and disable texture streaming in installation settings to remove this popup.");
    //     }
    // }
    
    [LoadingStage("Submitting Keys", stage: 8, weight: 20)]
    private async Task LoadKeys()
    {
        switch (AppSettings.Installation.CurrentProfile.RivalsVersion)
        {
            case ERivalsVersion.LatestInstalled:
            {
                Log.Information("Submitting Main Key {Key}", Globals.LATEST_AES);
                await Provider.SubmitKeyAsync(Globals.ZERO_GUID, new FAesKey(Globals.LATEST_AES));

                await SubmitExtraKeys(Globals.LATEST_EXTRA_AES.Select(key => new FileEncryptionKey(key)));
                
                break;
            }
            default:
            {
                await LoadLocalKeys();
                break;
            }
        }
    }
    
    [LoadingStage("Loading Virtual Paths", stage: 9, weight: 15)]
    private async Task LoadVirtualPaths()
    {
        Provider.LoadVirtualPaths();
        Provider.PostMount();
        
        if (!Provider.TryChangeCulture(Provider.GetLanguageCode(AppSettings.Installation.CurrentProfile.GameLanguage)))
        {
            Info.Message("Internationalization", $"Failed to load language \"{AppSettings.Installation.CurrentProfile.GameLanguage.Description}\"");
        }
    }

    [LoadingStage("Loading Mappings", stage: 10, weight: 1)]
    private async Task LoadMappings()
    {
        var mappingsPath = AppSettings.Installation.CurrentProfile.RivalsVersion switch
        {
            ERivalsVersion.LatestInstalled => await GetEndpointMappings() ?? GetLocalMappings(),
            _ when AppSettings.Installation.CurrentProfile.UseMappingsFile && File.Exists(AppSettings.Installation.CurrentProfile.MappingsFile) => AppSettings.Installation.CurrentProfile.MappingsFile,
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(mappingsPath))
        {
            Log.Information("Failed to load mappings, path is empty");
            return;
        }
        
        Provider.MappingsContainer = new FileUsmapTypeMappingsProvider(mappingsPath, StringComparer.Ordinal);
        Log.Information("Loaded Mappings: {Path}", mappingsPath);
    }
    
    [LoadingStage("Loading Required Assets", stage: 11, weight: 5)]
    private async Task LoadApplicationAssets()
    {
        if (await Provider.SafeLoadPackageObjectAsync("FortniteGame/Content/Balance/RarityData") is { } rarityData)
        {
            for (var i = 0; i < rarityData.Properties.Count; i++)
                RarityColors.Add(rarityData.GetByIndex<FRarityCollection>(i));
        }

        if (await Provider.SafeLoadPackageObjectAsync("/BeanstalkCosmetics/Cosmetics/DataTables/DT_BeanstalkCosmetics_Colors") is UDataTable beanstalkColorTable)
        {
            foreach (var (name, fallback) in beanstalkColorTable.RowMap)
            {
                var index = int.Parse(name.Text);
                BeanstalkColors[index] = fallback.GetOrDefault<FColor>("Color");
            }
        }
        
        if (await Provider.SafeLoadPackageObjectAsync("/BeanstalkCosmetics/Cosmetics/DataTables/DT_BeanstalkCosmetics_MaterialTypes") is UDataTable beanstalkMaterialTypesTable)
        {
            foreach (var (name, fallback) in beanstalkMaterialTypesTable.RowMap)
            {
                var index = int.Parse(name.Text);
                var color = new FLinearColor();
                foreach (var property in fallback.Properties)
                {
                    if (property.Tag is null) continue;
                    
                    var actualName = property.Name.Text.SubstringBefore("_");
                    switch (actualName)
                    {
                        case "Metallic":
                        {
                            color.R = (float) property.Tag.GetValue<double>();
                            break;
                        }
                        case "Roughness":
                        {
                            color.G = (float) property.Tag.GetValue<double>();
                            break;
                        }
                        case "Emissive":
                        {
                            color.B = (float) property.Tag.GetValue<double>();
                            break;
                        }
                    }
                }
                
                BeanstalkMaterialProps[index] = color;
            }
        }
        
        if (await Provider.SafeLoadPackageObjectAsync("/BeanstalkCosmetics/Cosmetics/DataTables/DT_PatternAtlasTextureSlots") is UDataTable beanstalkAtlasSlotsTable)
        {
            foreach (var (name, fallback) in beanstalkAtlasSlotsTable.RowMap)
            {
                var index = int.Parse(name.Text);
                foreach (var property in fallback.Properties)
                {
                    if (property.Tag is null) continue;
                    
                    var actualName = property.Name.Text.SubstringBefore("_");
                    if (!actualName.Equals("UV")) continue;
                    
                    BeanstalkAtlasTextureUVs[index] = property.Tag.GetValue<FVector>();
                }
            }
        }

        if (await Provider.SafeLoadPackageObjectAsync(
                "FortniteGame/Content/Athena/Items/Cosmetics/Metadata/CosmeticSets") is UDataTable cosmeticSetsTable)
        {
            foreach (var (tagName, data) in cosmeticSetsTable.RowMap)
            {
                if (data.GetOrDefault<FText?>("DisplayName") is not { } displayName) continue;
                SetNames[tagName.Text] = displayName.Text;
            }
        }
        
        foreach (var path in MaleLobbyMontagePaths)
        {
            MaleLobbyMontages.AddIfNotNull(await Provider.SafeLoadPackageObjectAsync<UAnimMontage>(path));
        }
        
        foreach (var path in FemaleLobbyMontagePaths)
        {
            FemaleLobbyMontages.AddIfNotNull(await Provider.SafeLoadPackageObjectAsync<UAnimMontage>(path));
        }
    }
    
    
    [LoadingStage("Loading Asset Registries", stage: 12, weight: 10)]
    private async Task LoadAssetRegistries()
    {
        var assetRegistries = Provider.Files
            .Where(x => x.Key.Contains("AssetRegistry", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        
        foreach (var (path, file) in assetRegistries)
        {
            if (!path.EndsWith(".bin")) continue;
            if (path.Contains("Editor", StringComparison.OrdinalIgnoreCase)) continue;

            UpdateStatus($"Loading {file.Path}");
            var assetArchive = await file.SafeCreateReaderAsync();
            if (assetArchive is null) continue;

            try
            {
                var assetRegistry = new FPartialAssetRegistryState(assetArchive);
                AssetRegistry.AddRange(assetRegistry.PreallocatedAssetDataBuffers);
                Log.Information("Loaded Asset Registry: {FilePath}", file.Path);
            }
            catch (Exception e)
            {
                Log.Warning("Failed to load asset registry: {FilePath}", file.Path);
                Log.Error(e.ToString());
            }
        }
    }

    // private async Task<string> GetTocPath(ERivalsVersion loadingType)
    // {
    //     var onDemandText = string.Empty;
    //     switch (loadingType)
    //     {
    //         case ERivalsVersion.LatestInstalled:
    //         {
    //             var onDemandPath = Path.Combine(AppSettings.Installation.CurrentProfile.ArchiveDirectory, @"..\..\..\Cloud\IoStoreOnDemand.ini");
    //             if (File.Exists(onDemandPath)) onDemandText = await File.ReadAllTextAsync(onDemandPath);
    //             break;
    //         }
    //         case ERivalsVersion.LatestOnDemand:
    //         {
    //             var onDemandFile = LiveManifest?.Files.FirstOrDefault(x => x.FileName.Equals("Cloud/IoStoreOnDemand.ini", StringComparison.OrdinalIgnoreCase));
    //             if (onDemandFile is not null) onDemandText = onDemandFile.GetStream().ReadToEnd().BytesToString();
    //             break;
    //         }
    //     }
    //
    //     if (string.IsNullOrEmpty(onDemandText)) return string.Empty;
    //
    //     var onDemandIni = new ConfigIni();
    //     onDemandIni.Read(new StringReader(onDemandText));
    //     return onDemandIni
    //         .Sections.FirstOrDefault(section => section.Name?.Equals("Endpoint") ?? false)?
    //         .Tokens.OfType<InstructionToken>().FirstOrDefault(token => token.Key.Equals("TocPath"))?
    //         .Value.Replace("\"", string.Empty) ?? string.Empty;
    // }
    
    private async Task LoadLocalKeys()
    {
        var mainKey = AppSettings.Installation.CurrentProfile.MainKey;
        if (mainKey.IsEmpty) mainKey = FileEncryptionKey.Empty;
                
        
        Log.Information("Submitting Local Main Key {Key}", mainKey.KeyString);
        await Provider.SubmitKeyAsync(Globals.ZERO_GUID, mainKey.EncryptionKey);

        await LoadLocalExtraKeys();
    }
    
    private async Task LoadLocalExtraKeys()
    {
        await SubmitExtraKeys(AppSettings.Installation.CurrentProfile.ExtraKeys);
    }

    private async Task SubmitExtraKeys(IEnumerable<FileEncryptionKey> extraKeys)
    {
        foreach (var vfs in Provider.UnloadedVfs.ToArray())
        {
            foreach (var extraKey in extraKeys)
            {
                if (extraKey.IsEmpty) continue;
                if (!vfs.TestAesKey(extraKey.EncryptionKey)) continue;
                        
                Log.Information("Submitting Extra Key {Key} with GUID {Guid} for {FileName}", extraKey.KeyString, vfs.EncryptionKeyGuid, vfs.Name);
                await Provider.SubmitKeyAsync(vfs.EncryptionKeyGuid, extraKey.EncryptionKey);
            }
        }
    }
    
    private async Task<string?> GetEndpointMappings()
    {
        var mappings = await Api.Repository.Mappings();
        if (mappings is null || mappings.Length <= 0) return null;

        var foundMappings = mappings.FirstOrDefault();
        if (foundMappings is null || string.IsNullOrWhiteSpace(foundMappings.URL)) return null;

        var fileName = !string.IsNullOrWhiteSpace(foundMappings.Filename)
            ? foundMappings.Filename
            : foundMappings.URL.SubstringAfterLast("/");
        var mappingsFilePath = Path.Combine(App.DataFolder.FullName, fileName);
        if (File.Exists(mappingsFilePath)) return mappingsFilePath;

        var createdFile = await Api.DownloadFileAsync(foundMappings.URL, mappingsFilePath);
        if (createdFile is null) return null;
            
        File.SetCreationTime(mappingsFilePath, foundMappings.Uploaded);

        return mappingsFilePath;
    }

    private string? GetLocalMappings()
    {
        var usmapFiles = App.DataFolder.GetFiles("*.usmap");
        if (usmapFiles.Length <= 0) return null;

        var latestUsmap = usmapFiles.MaxBy(x => x.CreationTime);
        return latestUsmap?.FullName;
    }

    public async Task<(Bitmap Icon, string? DisplayName, string? ExportType)> ResolveGameFileAsync(string gameFilePath)
    {
        return await Task.Run(() =>
        {
            Bitmap? icon = null;
            string? displayName = null;
            string? exportType = null;
            var fileName = gameFilePath.SubstringAfterLast("/").SubstringBefore(".");

            if (!Provider.TryLoadPackage(Provider.FixPath(gameFilePath), out var package))
            {
                icon = ImageExtensions.AvaresBitmap("avares://RivalsPorting/Assets/Unreal/DataAsset_64x.png");
                displayName = fileName;
                return (icon, displayName, exportType);
            }

            for (var i = 0; i < package.ExportMapLength; i++)
            {
                var pointer = new FPackageIndex(package, i + 1).ResolvedObject;
                if (pointer?.Object is null) continue;
                if (!pointer.Name.Text.Equals(fileName) &&
                    !pointer.Name.Text.Equals(fileName + "_C")) continue;

                var obj = ((AbstractUePackage) package).ConstructObject(pointer.Class, package);
                exportType = obj.ExportType;

                if (obj is UTexture2D && pointer.TryLoad(out var textureObj) &&
                    textureObj is UTexture2D texture &&
                    texture.Decode(maxMipSize: 128) is { } decodedTexture)
                {
                    icon = decodedTexture.ToWriteableBitmap();
                    break;
                }

                var assetLoader = AssetLoading.Categories
                    .SelectMany(category => category.Loaders)
                    .FirstOrDefault(loader => loader.ClassNames.Contains(obj.ExportType));
                if (assetLoader is not null && pointer.TryLoad(out var assetObj))
                {
                    icon = (assetLoader.LowResIconHandler(assetObj) ?? assetLoader.HighResIconHandler(assetObj))
                        ?.Decode(maxMipSize: 128)?.ToWriteableBitmap();
                    displayName = assetLoader.DisplayNameHandler(assetObj);
                    break;
                }

                displayName = obj.GetAnyOrDefault<FText?>("DisplayName", "ItemName")?.Text;

                if (obj.GetEditorIconBitmap() is { } editorIcon)
                {
                    icon = editorIcon;
                    break;
                }

                if (Exporter.DetermineExportType(obj) is var fnExportType and not EExportType.None
                    && $"avares://RivalsPorting/Assets/FN/{fnExportType}.png" is { } exportIconPath
                    && AssetLoader.Exists(new Uri(exportIconPath)))
                {
                    icon = ImageExtensions.AvaresBitmap(exportIconPath);
                    break;
                }
            }

            // fallback: resolve export type from first export if named export didn't set it
            if (exportType is null && new FPackageIndex(package, 1).ResolvedObject is { } zeroPointer)
            {
                var zeroObj = ((AbstractUePackage) package).ConstructObject(zeroPointer.Class, package);
                exportType = zeroObj.ExportType;
            }

            icon ??= ImageExtensions.AvaresBitmap("avares://RivalsPorting/Assets/Unreal/DataAsset_64x.png");
            displayName ??= fileName;
            return (icon, displayName, exportType);
        });
    }
}

public class LoadingStageAttribute : Attribute
{
    public string Name { get; }
    public int Stage { get; }
    public float Weight { get; }

    public LoadingStageAttribute(string name, int stage, float weight)
    {
        Name = name;
        Stage = stage;
        Weight = weight;
    }
}
record LoadingStage(MethodInfo Method, LoadingStageAttribute Attr);