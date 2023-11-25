using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CUE4Parse.Utils;
using FortnitePorting.Framework;
using FortnitePorting.Services;

namespace FortnitePorting.ViewModels;

public partial class BlenderPluginViewModel : ViewModelBase
{
    [ObservableProperty] private bool automaticUpdate = true;
    [ObservableProperty] private ObservableCollection<BlenderInstallInfo> installations = new();
    
    private static readonly FilePickerFileType FileType = new("Blender")
    {
        Patterns = new[] { "blender.exe" }
    };
    
    private const string EnableScript = """
    import bpy
    bpy.ops.preferences.addon_enable(module='FortnitePorting')
    bpy.ops.wm.save_userpref()
    """;
    
    public async Task Add()
    {
        if (CheckBlenderRunning()) return;
        
        var path = await AppVM.BrowseFileDialog(FileType);
        if (path is null) return;

        var versionInfo = FileVersionInfo.GetVersionInfo(path);
        if (Installations.Any(x => x.BlenderVersion.Equals(versionInfo.ProductVersion))) return;
        var majorVersion = int.Parse(versionInfo.ProductVersion[..1]);
        if (majorVersion < 4)
        {
            MessageWindow.Show("Invalid Blender Version", "Only Blender versions 4.0 or higher are supported.");
            return;
        }

        var installInfo = new BlenderInstallInfo(path, versionInfo.ProductVersion);
        await Sync(installInfo);
        await TaskService.RunDispatcherAsync(() => Installations.Add(installInfo));
    }
    
    public async Task Remove(BlenderInstallInfo removeItem)
    {
        Installations.Remove(removeItem);
        await UnSync(removeItem);
    }

    
    public async Task SyncAll()
    {
        if (CheckBlenderRunning()) return;
        foreach (var blenderInstall in Installations)
        {
            await Sync(blenderInstall);
        }
    }
    
    public async Task Sync(BlenderInstallInfo installInfo)
    {
        if (CheckBlenderRunning()) return;
        
        var assets = Avalonia.Platform.AssetLoader.GetAssets(new Uri("avares://FortnitePorting/Plugins/Blender"), null);
        foreach (var asset in assets)
        {
            await using var fileStream = File.OpenWrite(Path.Combine(installInfo.AddonPath, asset.AbsolutePath.SubstringAfterLast("/")));
            var assetStream = Avalonia.Platform.AssetLoader.Open(asset);
            await assetStream.CopyToAsync(fileStream);
        }

        using var blenderProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = installInfo.BlenderPath,
                Arguments = $"-b --disable-crash-handler --python-exit-code 255 --python-expr \"{EnableScript}\"",
                UseShellExecute = false
            }
        };
        blenderProcess.OutputDataReceived += (s, e) => Console.WriteLine(e.Data);
        blenderProcess.Start();
            
        var exitedProperly = blenderProcess.WaitForExit(10000);
        if (blenderProcess.ExitCode == 255 || !exitedProperly)
        {
            MessageWindow.Show("An Error Occured", "Blender failed to enable the FortnitePorting plugin. Please enable it yourself in the add-ons tab in Blender preferences.");
        }
            
        installInfo.Update();
    }
    
    public async Task UnSync(BlenderInstallInfo installInfo)
    {
        Directory.Delete(installInfo.AddonPath);
    }
    
    public bool CheckBlenderRunning()
    {
        var blenderProcesses = Process.GetProcessesByName("blender");
        if (blenderProcesses.Length > 0)
        {
            MessageWindow.Show("Cannot Sync Plugin", "An instance of blender is open. Please close it to sync the plugin.");
            return true;
        }

        return false;
    }
}

public partial class BlenderInstallInfo : ObservableObject
{
    [ObservableProperty] private string blenderPath;
    [ObservableProperty] private string blenderVersion;
    [ObservableProperty] private string pluginVersion = "???";
    [ObservableProperty] private string addonBasePath;
    [ObservableProperty] private string addonPath;

    public BlenderInstallInfo(string path, string blenderVersion)
    {
        BlenderPath = path;
        BlenderVersion = blenderVersion;
        AddonBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "Blender Foundation", 
            "Blender", 
            BlenderVersion, 
            "scripts", 
            "addons");
        AddonPath = Path.Combine(AddonBasePath, "FortnitePorting");
        Directory.CreateDirectory(AddonPath);
    }

    public void Update()
    {
        PluginVersion = GetPluginVersion();
    }

    public string GetPluginVersion()
    {
        var initFilepath = Path.Combine(AddonPath, "__init__.py");
        if (!File.Exists(initFilepath)) return PluginVersion;
        
        var initText = File.ReadAllText(initFilepath);
        var versionMatch = Regex.Match(initText, @"""version"": \((.*)\)");
        if (!versionMatch.Success) return PluginVersion;
        
        return versionMatch.Groups[^1].Value.Replace(", ", ".");
    }
}