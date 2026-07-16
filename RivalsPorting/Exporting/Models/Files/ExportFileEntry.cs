using CUE4Parse.UE4.Assets.Exports;
using RivalsPorting.Exporting.Models.Files.Meta;

namespace RivalsPorting.Exporting.Models.Files;

public class ExportFileEntry
{
    public EExportType Type { get; set; }
    public UObject Object { get; set; }
    public IExportFileMeta? Meta { get; set; }
}