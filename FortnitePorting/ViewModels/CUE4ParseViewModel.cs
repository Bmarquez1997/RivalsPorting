using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CUE4Parse.Compression;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.AssetRegistry;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.Sound;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.IO;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;
using CUE4Parse.UE4.VirtualFileSystem;
using CUE4Parse.Utils;
using EpicManifestParser;
using FortnitePorting.Application;
using FortnitePorting.Models.CUE4Parse;
using FortnitePorting.Models.Fortnite;
using FortnitePorting.Services;
using FortnitePorting.Shared;
using FortnitePorting.Shared.Framework;
using FortnitePorting.Shared.Models.CUE4Parse;
using FortnitePorting.Shared.Services;
using FortnitePorting.Windows;
using Serilog;
using UE4Config.Parsing;

namespace FortnitePorting.ViewModels;

public class CUE4ParseViewModel : ViewModelBase
{
    public bool FinishedLoading;
    
    public readonly HybridFileProvider Provider = AppSettings.Current.Installation.FortniteVersion switch
    {
        EFortniteVersion.LatestOnDemand => new HybridFileProvider(new VersionContainer(AppSettings.Current.Installation.UnrealVersion)),
        _ => new HybridFileProvider(AppSettings.Current.Installation.ArchiveDirectory, [], new VersionContainer(AppSettings.Current.Installation.UnrealVersion)),
    };
    
    public readonly List<FAssetData> AssetRegistry = [];
    public readonly List<FRarityCollection> RarityColors = [];
    public readonly Dictionary<int, FColor> BeanstalkColors = [];
    public readonly Dictionary<int, FLinearColor> BeanstalkMaterialProps = [];
    public readonly Dictionary<int, FVector> BeanstalkAtlasTextureUVs = [];
    
    private static readonly Regex FortniteArchiveRegex = new(@"^FortniteGame(/|\\)Content(/|\\)Paks(/|\\)(pakchunk(?:0|10.*|\w+)-WindowsClient|global)\.(pak|utoc)$", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    
    public override async Task Initialize()
    {
        ObjectTypeRegistry.RegisterEngine(Assembly.GetAssembly(typeof(UCustomObject))!);

        await CleanupCache();

        Provider.VfsMounted += (sender, _) =>
        {
            if (sender is not IAesVfsReader reader) return;

            HomeVM.UpdateStatus($"Loading {reader.Name}");
        };
        
        HomeVM.UpdateStatus("Loading Native Libraries");
        await InitializeOodle();
        await InitializeZlib();
        
        HomeVM.UpdateStatus("Loading Game Files");
        await InitializeProvider();
        await InitializeTextureStreaming();
        
        await LoadKeys();
        Provider.LoadLocalization(AppSettings.Current.Installation.GameLanguage);
        Provider.LoadVirtualPaths();
        await LoadMappings();
        
        Provider.PostMount();
        await LoadConsoleVariables();

        await LoadAssetRegistries();

        HomeVM.UpdateStatus("Loading Application Assets");
        await LoadApplicationAssets();

        HomeVM.UpdateStatus(string.Empty);

        FinishedLoading = true;
    }
    
    private async Task CleanupCache()
    {
        var files = CacheFolder.GetFiles("*.*chunk");
        var cutoffDate = DateTime.Now - TimeSpan.FromDays(AppSettings.Current.Application.ChunkCacheLifetime);
        foreach (var file in files)
        {
            if (file.LastWriteTime >= cutoffDate) continue;
            
            file.Delete();
        }
    }
    
    private async Task InitializeOodle()
    {
        var oodlePath = Path.Combine(DataFolder.FullName, OodleHelper.OODLE_DLL_NAME);
        if (!File.Exists(oodlePath)) await OodleHelper.DownloadOodleDllAsync(oodlePath);
        OodleHelper.Initialize(oodlePath);
    }
    
    private async Task InitializeZlib()
    {
        var zlibPath = Path.Combine(DataFolder.FullName, ZlibHelper.DLL_NAME);
        if (!File.Exists(zlibPath)) await ZlibHelper.DownloadDllAsync(zlibPath);
        ZlibHelper.Initialize(zlibPath);
    }
    
    private async Task InitializeProvider()
    {
        if (AppSettings.Current.Installation.FortniteVersion is EFortniteVersion.LatestInstalled or EFortniteVersion.LatestOnDemand)
        {
            await ApiVM.EpicGames.VerifyAuthAsync();
        }
        
        switch (AppSettings.Current.Installation.FortniteVersion)
        {
            case EFortniteVersion.LatestOnDemand:
            {
                var manifestInfo = await ApiVM.EpicGames.GetManifestInfoAsync();
                if (manifestInfo is null) break;

                var options = new ManifestParseOptions
                {
                    ChunkBaseUrl = "http://epicgames-download1.akamaized.net/Builds/Fortnite/CloudDir/",
                    ChunkCacheDirectory = CacheFolder.FullName,
                    ManifestCacheDirectory = CacheFolder.FullName,
                    Zlibng = ZlibHelper.Instance,
                    CacheChunksAsIs = true
                };
                
                var (manifest, element) = await manifestInfo.DownloadAndParseAsync(options);

                HomeVM.UpdateStatus($"Loading Fortnite On-Demand (This may take a while)");

                var fileManifests = manifest.FileManifestList.Where(fileManifest => FortniteArchiveRegex.IsMatch(fileManifest.FileName));
                foreach (var fileManifest in fileManifests)
                {
                    Provider.RegisterVfs(fileManifest.FileName, [fileManifest.GetStream()],
                        name => new FStreamArchive(name,
                            manifest.FileManifestList.First(file => file.FileName.Equals(name)).GetStream()));
                }
                
                break;
            }
            default:
            {
                Provider.Initialize();
                break;
            }
        }
    }

    private async Task InitializeTextureStreaming()
    {
        if (AppSettings.Current.Installation.FortniteVersion is not (EFortniteVersion.LatestInstalled or EFortniteVersion.LatestOnDemand)) return;
        if (AppSettings.Current.Installation.FortniteVersion == EFortniteVersion.LatestInstalled && !AppSettings.Current.Installation.TextureStreamingEnabled) return;

        try
        {
            var tocPath = await GetTocPath(AppSettings.Current.Installation.FortniteVersion);
            if (string.IsNullOrEmpty(tocPath)) return;

            var tocName = tocPath.SubstringAfterLast("/");
            var onDemandFile = new FileInfo(Path.Combine(DataFolder.FullName, tocName));
            if (!onDemandFile.Exists || onDemandFile.Length == 0)
            {
                await ApiVM.DownloadFileAsync($"https://download.epicgames.com/{tocPath}", onDemandFile.FullName);
            }

            var options = new IoStoreOnDemandOptions
            {
                ChunkBaseUri = new Uri("https://download.epicgames.com/ias/fortnite/", UriKind.Absolute),
                ChunkCacheDirectory = CacheFolder,
                Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.Current.Online.EpicAuth?.Token),
                Timeout = TimeSpan.FromSeconds(30)
            };
            
            await Provider.RegisterVfs(new IoChunkToc(onDemandFile), options);
            await Provider.MountAsync();
        }
        catch (Exception e)
        {
            AppWM.Dialog("Failed to Initialize Texture Streaming", 
                $"Please enable the \"Pre-Download Streamed Assets\" option for Fortnite in the Epic Games Launcher and disable texture streaming in installation settings to remove this popup\n\nException: {e}");
        }
    }
    
    private async Task<string> GetTocPath(EFortniteVersion loadingType)
    {
        var onDemandText = string.Empty;
        switch (loadingType)
        {
            case EFortniteVersion.LatestInstalled:
            {
                var onDemandPath = Path.Combine(AppSettings.Current.Installation.ArchiveDirectory, @"..\..\..\Cloud\IoStoreOnDemand.ini");
                if (File.Exists(onDemandPath)) onDemandText = await File.ReadAllTextAsync(onDemandPath);
                break;
            }
            case EFortniteVersion.LatestOnDemand:
            {
                // TODO live cosmetic streaming
                //var onDemandFile = FortniteLive?.FileManifests.FirstOrDefault(x => x.Name.Equals("Cloud/IoStoreOnDemand.ini", StringComparison.OrdinalIgnoreCase));
                //if (onDemandFile is not null) onDemandText = onDemandFile.GetStream().ReadToEnd().BytesToString();
                break;
            }
        }

        if (string.IsNullOrEmpty(onDemandText)) return string.Empty;

        var onDemandIni = new ConfigIni();
        onDemandIni.Read(new StringReader(onDemandText));
        return onDemandIni
            .Sections.FirstOrDefault(section => section.Name?.Equals("Endpoint") ?? false)?
            .Tokens.OfType<InstructionToken>().FirstOrDefault(token => token.Key.Equals("TocPath"))?
            .Value.Replace("\"", string.Empty) ?? string.Empty;
    }

    private async Task LoadKeys()
    {
        switch (AppSettings.Current.Installation.FortniteVersion)
        {
            case EFortniteVersion.LatestInstalled:
            case EFortniteVersion.LatestOnDemand:
            {
                var aes = await ApiVM.FortniteCentral.GetKeysAsync();
                if (aes is null) return;
                
                await Provider.SubmitKeyAsync(Globals.ZERO_GUID, new FAesKey(aes.MainKey));
                foreach (var key in aes.DynamicKeys)
                {
                    await Provider.SubmitKeyAsync(new FGuid(key.GUID), new FAesKey(key.Key));
                }
                
                break;
            }
            default:
            {
                var mainKey = AppSettings.Current.Installation.MainKey;
                if (mainKey.IsEmpty) mainKey = FileEncryptionKey.Empty;
                
                await Provider.SubmitKeyAsync(Globals.ZERO_GUID, mainKey.EncryptionKey);
                
                foreach (var vfs in Provider.UnloadedVfs.ToArray())
                {
                    foreach (var extraKey in AppSettings.Current.Installation.ExtraKeys)
                    {
                        if (extraKey.IsEmpty) continue;
                        if (!vfs.TestAesKey(extraKey.EncryptionKey)) continue;
                        
                        await Provider.SubmitKeyAsync(vfs.EncryptionKeyGuid, extraKey.EncryptionKey);
                    }
                }
                break;
            }
        }
    }
    
    private async Task LoadMappings()
    {
        var mappingsPath = AppSettings.Current.Installation.FortniteVersion switch
        {
            EFortniteVersion.LatestInstalled or EFortniteVersion.LatestOnDemand => await GetEndpointMappings() ?? GetLocalMappings(),
            _ when AppSettings.Current.Installation.UseMappingsFile && File.Exists(AppSettings.Current.Installation.MappingsFile) => AppSettings.Current.Installation.MappingsFile,
            _ => string.Empty
        };
        
        if (string.IsNullOrEmpty(mappingsPath)) return;
        
        Provider.MappingsContainer = new FileUsmapTypeMappingsProvider(mappingsPath);
        Log.Information("Loaded Mappings: {Path}", mappingsPath);
    }
    
    private async Task<string?> GetEndpointMappings()
    {
        var mappings =  await ApiVM.FortniteCentral.GetMappingsAsync();
        if (mappings is null) return null;
        if (mappings.Length <= 0) return null;

        var foundMappings = mappings.FirstOrDefault();
        if (foundMappings is null) return null;

        var mappingsFilePath = Path.Combine(DataFolder.FullName, foundMappings.Filename);
        if (File.Exists(mappingsFilePath)) return null;

        await ApiVM.DownloadFileAsync(foundMappings.URL, mappingsFilePath);
        return mappingsFilePath;
    }

    private string? GetLocalMappings()
    {
        var usmapFiles = DataFolder.GetFiles("*.usmap");
        if (usmapFiles.Length <= 0) return null;

        var latestUsmap = usmapFiles.MaxBy(x => x.LastWriteTime);
        return latestUsmap?.FullName;
    }
    
    private async Task LoadConsoleVariables()
    {
        var tokens = Provider.DefaultEngine.Sections.FirstOrDefault(source => source.Name == "ConsoleVariables")?.Tokens ?? [];
        foreach (var token in tokens)
        {
            if (token is not InstructionToken instructionToken) continue;
            var value = instructionToken.Value.Equals("1");
            
            switch (instructionToken.Key)
            {
                case "r.StaticMesh.KeepMobileMinLODSettingOnDesktop":
                case "r.SkeletalMesh.KeepMobileMinLODSettingOnDesktop":
                    Provider.Versions[instructionToken.Key[2..]] = value;
                    continue;
            }
        }
    }
    
    private async Task LoadAssetRegistries()
    {
        var assetRegistries = Provider.Files
            .Where(x => x.Key.Contains("AssetRegistry", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        
        foreach (var (path, file) in assetRegistries)
        {
            if (!path.EndsWith(".bin")) continue;
            if (path.Contains("Plugin", StringComparison.OrdinalIgnoreCase) || path.Contains("Editor", StringComparison.OrdinalIgnoreCase)) continue;

            HomeVM.UpdateStatus($"Loading {file.Name}");
            var assetArchive = await file.TryCreateReaderAsync();
            if (assetArchive is null) continue;

            try
            {
                var assetRegistry = new FAssetRegistryState(assetArchive);
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
    
    private async Task LoadApplicationAssets()
    {
        if (await Provider.TryLoadObjectAsync("FortniteGame/Content/Balance/RarityData") is { } rarityData)
        {
            for (var i = 0; i < rarityData.Properties.Count; i++)
                RarityColors.Add(rarityData.GetByIndex<FRarityCollection>(i));
        }

        if (await Provider.TryLoadObjectAsync("/BeanstalkCosmetics/Cosmetics/DataTables/DT_BeanstalkCosmetics_Colors") is UDataTable beanstalkColorTable)
        {
            foreach (var (name, fallback) in beanstalkColorTable.RowMap)
            {
                var index = int.Parse(name.Text);
                BeanstalkColors[index] = fallback.GetOrDefault<FColor>("Color");
            }
        }
        
        if (await Provider.TryLoadObjectAsync("/BeanstalkCosmetics/Cosmetics/DataTables/DT_BeanstalkCosmetics_MaterialTypes") is UDataTable beanstalkMaterialTypesTable)
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
        
        if (await Provider.TryLoadObjectAsync("/BeanstalkCosmetics/Cosmetics/DataTables/DT_PatternAtlasTextureSlots") is UDataTable beanstalkAtlasSlotsTable)
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
    }
}