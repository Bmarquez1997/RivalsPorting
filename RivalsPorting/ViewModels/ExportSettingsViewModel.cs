using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CUE4Parse_Conversion;
using CUE4Parse_Conversion.Animations;
using CUE4Parse_Conversion.Meshes;
using CUE4Parse_Conversion.UEFormat.Enums;
using CUE4Parse.UE4.Assets.Exports.Nanite;
using CUE4Parse.UE4.Versions;
using FluentAvalonia.UI.Controls;
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
    
    [ObservableProperty] private EAnimFormat _animFormat = EAnimFormat.UEFormat;
    [ObservableProperty] private bool _importLobbyPoses = false;
    [ObservableProperty] private bool _importGameModel = false;
    
    [ObservableProperty] private ESoundFormat _soundFormat = ESoundFormat.WAV;
    
    public virtual ExporterOptions CreateExportOptions()
    {
        return new ExporterOptions()
        {
            MeshFormat = MeshFormat,
            AnimFormat = AnimFormat,
            CompressionFormat = CompressionFormat,
            NaniteMeshFormat = ExportNanite ? ENaniteMeshFormat.NaniteSeparateFile : ENaniteMeshFormat.OnlyNormalLODs
        };
    }
}

