using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Avalonia.Platform;
using RivalsPorting.Shared.Extensions;

namespace RivalsPorting.Services;

public class DependencyService : IService
{
    public bool FinishedEnsuring;
    
    public readonly FileInfo BinkaDecoderFile = new(Path.Combine(App.DataFolder.FullName, "binka", "binkadec.exe"));
    public readonly FileInfo RadaDecoderFile = new(Path.Combine(App.DataFolder.FullName, "rada", "radadec.exe"));
    public readonly FileInfo NoodleFile = new(Path.Combine(App.DataFolder.FullName, "noodle.dll"));
    public readonly FileInfo VgmStreamFile = new(Path.Combine(App.DataFolder.FullName, "vgmstream", "vgmstream-cli.exe"));
    
    public readonly DirectoryInfo VgmStreamFolder = new(Path.Combine(App.DataFolder.FullName, "vgmstream"));

    public void Ensure()
    {
        TaskService.Run(() =>
        {
            EnsureResource("Assets/Dependencies/noodle.dll", NoodleFile);
            EnsureResource("Assets/Dependencies/binkadec.exe", BinkaDecoderFile);
            EnsureResource("Assets/Dependencies/radadec.exe", RadaDecoderFile);
            EnsureVgmStream();
            EnsureBlenderExtensions();
            EnsureUnrealPlugins();
            FinishedEnsuring = true;
        });
    }

    private void EnsureResource(string path, FileInfo targetFile)
    {
        var assetStream = AssetLoader.Open(new Uri($"avares://RivalsPorting/{path}"));
        if (targetFile is { Exists: true, Length: > 0 } && targetFile.GetHash() == assetStream.GetHash()) return;

        targetFile.Directory?.Create();
        targetFile.Delete();
        File.WriteAllBytes(targetFile.FullName, assetStream.ReadToEnd());
    }

    private void EnsureVgmStream()
    {
        if (VgmStreamFile is { Exists: true, Length: > 0 } ) return;
        
        VgmStreamFolder.Create();
        var file = Api.DownloadFile("https://github.com/vgmstream/vgmstream/releases/latest/download/vgmstream-win.zip", VgmStreamFolder);
        if (!file.Exists || file.Length == 0) return;
        
        var zip = ZipFile.Open(file.FullName, ZipArchiveMode.Read);
        foreach (var zipFile in zip.Entries)
        {
            using var zipStream = zipFile.Open();
            using var fileStream = new FileStream(Path.Combine(VgmStreamFolder.FullName, zipFile.FullName), FileMode.OpenOrCreate, FileAccess.Write);
            zipStream.CopyTo(fileStream);
        }
    }

    private void EnsureBlenderExtensions()
    {
        var blenderFolder = new DirectoryInfo(Path.Combine(App.PluginsFolder.FullName, "Blender"));
        if (blenderFolder.Exists)
            blenderFolder.Delete(true);

        var assets = AssetLoader.GetAssets(new Uri("avares://RivalsPorting.Plugins/Blender"), null);
        WriteAssets(assets, App.PluginsFolder.FullName);
        var assetsUeformat = AssetLoader.GetAssets(new Uri("avares://RivalsPorting.Plugins/UEFormat/Blender/io_scene_ueformat"), null);
        WriteAssets(assetsUeformat, Path.Combine(App.PluginsFolder.FullName, "Blender", "rivals_porting", "ueformat"), 36);
    }
    
    private void EnsureUnrealPlugins()
    {
        var unrealFolder = new DirectoryInfo(Path.Combine(App.PluginsFolder.FullName, "Unreal"));
        if (unrealFolder.Exists)
            unrealFolder.Delete(true);

        var assets = AssetLoader.GetAssets(new Uri("avares://RivalsPorting.Plugins/Unreal"), null);
        WriteAssets(assets, App.PluginsFolder.FullName);
        var assetsUeformat = AssetLoader.GetAssets(new Uri("avares://RivalsPorting.Plugins/UEFormat/Unreal/UEFormat"), null);
        WriteAssets(assetsUeformat, App.PluginsFolder.FullName, 10);
    }
    
    private void WriteAssets(IEnumerable<Uri> assets, string rootFolder, int pathStartIndex = 1)
    {
        foreach (var asset in assets)
        {
            var assetStream = AssetLoader.Open(asset);
            var targetFile = new FileInfo(Path.Combine(rootFolder, asset.AbsolutePath[pathStartIndex..]));
            if (targetFile is { Exists: true, Length: > 0 } && targetFile.GetHash() == assetStream.GetHash()) continue;
            targetFile.Directory?.Create();
            
            File.WriteAllBytes(targetFile.FullName, assetStream.ReadToEnd());
        }
    }
}