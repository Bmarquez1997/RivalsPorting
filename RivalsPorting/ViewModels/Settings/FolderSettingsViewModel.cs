using CommunityToolkit.Mvvm.ComponentModel;
using CUE4Parse_Conversion.Options;
using RivalsPorting.Exporting.Models;

namespace RivalsPorting.ViewModels.Settings;

public partial class FolderSettingsViewModel : BaseExportSettings
{
    [ObservableProperty] private EMeshQuality _meshQuality = EMeshQuality.All;
    [ObservableProperty] private bool _openFoldersOnExport;

    public override ExportSettings ToExportSettings()
    {
        var settings = base.ToExportSettings();
        settings.MeshQuality = MeshQuality;
        settings.OpenFoldersOnExport = OpenFoldersOnExport;
        return settings;
    }
}
