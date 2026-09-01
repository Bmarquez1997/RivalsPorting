using System.Collections.Generic;
using System.IO;
using CUE4Parse.FileProvider.Vfs;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Objects.Core.Math;
using RivalsPorting.Exporting;
using RivalsPorting.Exporting.Context;
using RivalsPorting.Exporting.Models;
using RivalsPorting.Exporting.Providers;
using RivalsPorting.Exporting.Styles;
using RivalsPorting.Exporting.Types;
using RivalsPorting.Services;

namespace RivalsPorting.Providers;

public class ExportAssetProvider(CUE4ParseService ueParse, DependencyService dependencies) : IExportAssetProvider, IService
{
    public AbstractVfsFileProvider Provider => ueParse.Provider!;

    public List<UAnimMontage> MaleLobbyMontages => ueParse.MaleLobbyMontages;
    public List<UAnimMontage> FemaleLobbyMontages => ueParse.FemaleLobbyMontages;

    public Dictionary<int, FColor> BeanstalkColors => ueParse.BeanstalkColors;
    public Dictionary<int, FLinearColor> BeanstalkMaterialProps => ueParse.BeanstalkMaterialProps;
    public Dictionary<int, FVector> BeanstalkAtlasTextureUVs => ueParse.BeanstalkAtlasTextureUVs;

    public FileInfo BinkaDecoderFile => dependencies.BinkaDecoderFile;
    public FileInfo RadaDecoderFile => dependencies.RadaDecoderFile;
    public FileInfo VgmStreamFile => dependencies.VgmStreamFile;
    public string ArchiveDirectory => AppSettings.Installation.CurrentProfile.ArchiveDirectory;

    public void AppendRivalsEmoteWeaponProps(ExportContext context, List<ExportProp> props, UObject asset, ExportStyleBase[] styles)
        => RivalsEmoteWeaponProps.AppendForExportedAnim(context, props, asset, styles);

    public void AppendRivalsMvp(ExportContext context, UObject levelSequence, ref ExportMesh? skeleton,
        List<ExportAnimSection> sections, List<ExportProp> props)
        => RivalsMvpExport.AppendFromLevelSequence(context, levelSequence, ref skeleton, sections, props);

    public void ImportRivalsLobbyPose(MeshExport export, ExportContext context, ExportStyleBase[] styles)
        => RivalsEmoteWeaponProps.ImportLobbyPose(export, context, styles);
}
