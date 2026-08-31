using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CUE4Parse_Conversion.Options;
using CUE4Parse_Conversion.Writers.UEFormat.Enums;
using RivalsPorting.Application;
using RivalsPorting.Exporting.Models;
using RivalsPorting.Framework;
using RivalsPorting.ViewModels.Settings;

namespace RivalsPorting.ViewModels;

public partial class ExportSettingsViewModel : ViewModelBase
{
    [ObservableProperty] private BlenderSettingsViewModel _blender = new();
    [ObservableProperty] private UnrealSettingsViewModel _unreal = new();
    [ObservableProperty] private FolderSettingsViewModel _folder = new();
    
    public ExportDataMeta CreateExportMeta(EExportLocation exportLocation = EExportLocation.Blender, string? customPath = null)
    {
        // Kept on BlenderSettings for upstream merge compatibility, but never enable for Rivals.
        Blender.MergeArmatures = false;

        return new ExportDataMeta
        {
            ExportLocation = exportLocation,
            AssetsRoot = AppSettings.Application.AssetPath,
            Settings = exportLocation switch
            {
                EExportLocation.Blender => Blender,
                EExportLocation.Unreal => Unreal,
                EExportLocation.AssetsFolder or EExportLocation.CustomFolder => Folder
            },
            CustomPath = customPath
        };
    }

    public ExportDataMeta CreateExportMeta(EExportLocation exportLocation = EExportLocation.Blender, string? customPath = null)
    {
        var viewModel = GetSettingsViewModel(exportLocation);
        return new ExportDataMeta
        {
            Version = Globals.VersionString,
            ExportLocation = exportLocation,
            AssetsRoot = AppSettings.Application.AssetPath,
            Settings = viewModel.ToExportSettings(),
            Provider = ExportAssets,
            CustomPath = customPath
        };
    }

    public override async Task OnViewExited()
    {
        if (AppSettings.ShouldSaveOnExit) 
            AppSettings.Save();
    }
}

public partial class BaseExportSettings : ViewModelBase
{
    [ObservableProperty] private EFileCompressionFormat _compressionFormat = EFileCompressionFormat.ZSTD;

    [ObservableProperty] private EImageFormat _imageFormat = EImageFormat.PNG;
    [ObservableProperty] private bool _exportMaterials = true;
    
    [ObservableProperty] private EMeshFormat _meshFormat = EMeshFormat.UEFormat;
    [ObservableProperty] private bool _exportNanite;
    [ObservableProperty] private bool _importInstancedFoliage = true;
    
    [ObservableProperty] private bool _importLobbyPoses = false;
    [ObservableProperty] private bool _importGameModel = false;
    
    [ObservableProperty] private ESoundFormat _soundFormat = ESoundFormat.WAV;

    public virtual ExportOptions CreateExportOptions() => ToExportSettings().CreateExportOptions();

    public virtual ExportSettings ToExportSettings() => new()
    {
        CompressionFormat = CompressionFormat,
        ImageFormat = ImageFormat,
        ExportMaterials = ExportMaterials,
        MeshFormat = MeshFormat,
        MeshQuality = EMeshQuality.All,
        ExportNanite = ExportNanite,
        ImportInstancedFoliage = ImportInstancedFoliage,
        ImportLobbyPoses = ImportLobbyPoses,
        SoundFormat = SoundFormat
    };
}
