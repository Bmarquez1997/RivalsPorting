using System.Collections.Generic;
using System.IO;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.FileProvider.Vfs;
using RivalsPorting.Exporting.Context;
using RivalsPorting.Exporting.Models;
using RivalsPorting.Exporting.Styles;
using RivalsPorting.Exporting.Types;

namespace RivalsPorting.Exporting.Providers;

public interface IExportAssetProvider
{
    AbstractVfsFileProvider Provider { get; }

    List<UAnimMontage> MaleLobbyMontages { get; }
    List<UAnimMontage> FemaleLobbyMontages { get; }

    Dictionary<int, FColor> BeanstalkColors { get; }
    Dictionary<int, FLinearColor> BeanstalkMaterialProps { get; }
    Dictionary<int, FVector> BeanstalkAtlasTextureUVs { get; }

    FileInfo BinkaDecoderFile { get; }
    FileInfo RadaDecoderFile { get; }
    FileInfo VgmStreamFile { get; }
    string ArchiveDirectory { get; }

    void AppendRivalsEmoteWeaponProps(ExportContext context, List<ExportProp> props, UObject asset, ExportStyleBase[] styles);
    void AppendRivalsMvp(ExportContext context, UObject levelSequence, ref ExportMesh? skeleton, List<ExportAnimSection> sections, List<ExportProp> props);
    void ImportRivalsLobbyPose(MeshExport export, ExportContext context, ExportStyleBase[] styles);
}
