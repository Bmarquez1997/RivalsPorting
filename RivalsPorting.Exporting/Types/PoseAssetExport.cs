using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Rig;
using CUE4Parse.UE4.Objects.Engine.Animation;
using RivalsPorting.Exporting.Models;
using RivalsPorting.Exporting.Models.Files.Meta;

namespace RivalsPorting.Exporting.Types;

public class PoseAssetExport : BaseExport
{
    public string PoseAsset = string.Empty;

    public PoseAssetExport(string name, UObject asset, EExportType exportType, ExportDataMeta metaData, IExportFileMeta? fileMeta) : base(name, exportType, metaData)
    {
        if (metaData.ExportLocation.IsFolder)
        {
            return;
        }

        PoseAsset = asset switch
        {
            UPoseAsset poseAsset => Context.Export(poseAsset),
            UDNAAsset dnaAsset => Context.Export(dnaAsset),
            _ => string.Empty
        };
    }
    
}
