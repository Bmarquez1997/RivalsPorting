using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using FortnitePorting.Shared.Framework;
using FortnitePorting.ViewModels;
using FortnitePorting.Views;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace FortnitePorting.Services;

public static class ApplicationService
{
    public static AppViewModel AppVM => ViewModelRegistry.Get<AppViewModel>()!;
    public static WelcomeViewModel WelcomeVM => ViewModelRegistry.Get<WelcomeViewModel>()!;
    public static HomeViewModel HomeVM => ViewModelRegistry.Get<HomeViewModel>()!;
    
    public static IClassicDesktopStyleApplicationLifetime Application = null!;
    private static IStorageProvider StorageProvider => Application.MainWindow!.StorageProvider;
    
    public static readonly DirectoryInfo AssetsFolder = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets"));
    public static readonly DirectoryInfo LogsFolder = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs"));
    public static readonly DirectoryInfo DataFolder = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".data"));
    public static readonly DirectoryInfo CacheFolder = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".cache"));

    public static void OnStartup(object? sender, ControlledApplicationLifetimeStartupEventArgs e)
    {
        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
        
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(theme: AnsiConsoleTheme.Literate)
            .WriteTo.File(Path.Combine(LogsFolder.FullName, $"FortnitePorting-{DateTime.Now:yyyy-MM-dd-hh-mm-ss}.log"))
            .CreateLogger();
        
        AssetsFolder.Create();
        DataFolder.Create();
        LogsFolder.Create();
        CacheFolder.Create();
    }
    
    public static async Task<string?> BrowseFolderDialog(string startLocation = "")
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false, SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(startLocation)});
        var folder = folders.ToArray().FirstOrDefault();

        return folder?.Path.AbsolutePath.Replace("%20", " ");
    }

    public static async Task<string?> BrowseFileDialog(params FilePickerFileType[] fileTypes)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false, FileTypeFilter = fileTypes });
        var file = files.ToArray().FirstOrDefault();

        return file?.Path.AbsolutePath.Replace("%20", " ");
    }

    public static async Task<string?> SaveFileDialog(FilePickerSaveOptions saveOptions = default)
    {
        var file = await StorageProvider.SaveFilePickerAsync(saveOptions);
        return file?.Path.AbsolutePath.Replace("%20", " ");
    }
}