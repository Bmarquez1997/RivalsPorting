using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CUE4Parse_Conversion.Animations;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.Sound;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.Utils;
using FluentAvalonia.UI.Controls;
using FortnitePorting.Application;
using FortnitePorting.Controls.Assets;
using FortnitePorting.Export.Models;
using FortnitePorting.Export.Types;
using FortnitePorting.Models.API;
using FortnitePorting.Models.Assets;
using FortnitePorting.Models.Unreal;
using FortnitePorting.Services;
using FortnitePorting.Shared;
using FortnitePorting.Shared.Extensions;
using FortnitePorting.Shared.Framework;
using FortnitePorting.Shared.Models;
using FortnitePorting.Shared.Services;
using FortnitePorting.ViewModels;
using Newtonsoft.Json;
using Serilog;

namespace FortnitePorting.Export;

public static class Exporter
{
    public static async Task Export(IEnumerable<AssetInfo> assets, ExportDataMeta metaData)
    {
        await TaskService.RunAsync(async () =>
        {
            if (await ApiVM.FortnitePortingServer.PingAsync(EExportServerType.Blender) is false)
            {
                AppWM.Message("Blender Server", "The blender server for Fortnite Porting is not currently running.", InfoBarSeverity.Error, false);
                return;
            }
            
            
            var exports = assets.Select(asset => CreateExport(asset, metaData)).ToArray();
            foreach (var export in exports) export.WaitForExports();
            
            var exportData = new ExportData
            {
                MetaData = metaData,
                Exports = exports
            };
            
            var data = JsonConvert.SerializeObject(exportData);
            await ApiVM.FortnitePortingServer.SendAsync(data, EExportServerType.Blender);
        });
    }
    
    public static async Task Export(List<KeyValuePair<UObject, EExportType>> assets, ExportDataMeta metaData)
    {
        await TaskService.RunAsync(async () =>
        {
            if (await ApiVM.FortnitePortingServer.PingAsync(EExportServerType.Blender) is false)
            {
                AppWM.Message("Blender Server", "The blender server for Fortnite Porting is not currently running.", InfoBarSeverity.Error, false);
                return;
            }
            
            var exports = assets.Select(kvp => CreateExport(kvp.Key, kvp.Value, metaData)).ToArray();
            foreach (var export in exports) export.WaitForExports();
            
            var exportData = new ExportData
            {
                MetaData = metaData,
                Exports = exports
            };
            
            var data = JsonConvert.SerializeObject(exportData);
            await ApiVM.FortnitePortingServer.SendAsync(data, EExportServerType.Blender);
        });
    }
    
    public static async Task Export(IEnumerable<UObject> assets, EExportType type, ExportDataMeta metaData)
    {
        await TaskService.RunAsync(async () =>
        {
            if (await ApiVM.FortnitePortingServer.PingAsync(EExportServerType.Blender) is false)
            {
                AppWM.Message("Blender Server", "The blender server for Fortnite Porting is not currently running.", InfoBarSeverity.Error, false);
                return;
            }
            
            
            var exports = assets.Select(obj => CreateExport(obj, type, metaData)).ToArray();
            foreach (var export in exports) export.WaitForExports();
            
            var exportData = new ExportData
            {
                MetaData = metaData,
                Exports = exports
            };
            
            var data = JsonConvert.SerializeObject(exportData);
            await ApiVM.FortnitePortingServer.SendAsync(data, EExportServerType.Blender);
        });
    }
    
    public static async Task Export(UObject asset, EExportType type, ExportDataMeta metaData)
    {
        await TaskService.RunAsync(async () =>
        {
            if (await ApiVM.FortnitePortingServer.PingAsync(EExportServerType.Blender) is false)
            {
                AppWM.Message("Blender Server", "The blender server for Fortnite Porting is not currently running.", InfoBarSeverity.Error, false);
                return;
            }
            
            
            var export = CreateExport(asset, type, metaData);
            export.WaitForExports();
            
            var exportData = new ExportData
            {
                MetaData = metaData,
                Exports = [export]
            };
            
            var data = JsonConvert.SerializeObject(exportData);
            await ApiVM.FortnitePortingServer.SendAsync(data, EExportServerType.Blender);
        });
    }

    public static EExportType DetermineExportType(UObject asset) 
    {
        var exportType = asset switch
        {
            USkeletalMesh => EExportType.Mesh,
            UStaticMesh => EExportType.Mesh,
            USkeleton => EExportType.Mesh,
            UWorld => EExportType.World,
            UTexture => EExportType.Texture,
            UVirtualTextureBuilder => EExportType.Texture,
            USoundWave => EExportType.Sound,
            USoundCue => EExportType.Sound,
            UAnimSequence => EExportType.Animation,
            UAnimMontage => EExportType.Animation,
            _ => EExportType.None
        };

        if (exportType is EExportType.None)
        {
            var assetLoaders = AssetLoaderCollection.CategoryAccessor.Categories
                .SelectMany(category => category.Loaders)
                .ToArray();

            foreach (var loader in assetLoaders)
            {
                if (loader.ClassNames.Contains(asset.ExportType))
                {
                    exportType = loader.Type;
                }
            }
        }

        return exportType;
    }
    
    private static BaseExport CreateExport(UObject asset, EExportType exportType, ExportDataMeta metaData)
    {
        return CreateExport(asset.Name, asset, [], exportType, metaData);
    }

    private static BaseExport CreateExport(AssetInfo assetInfo, ExportDataMeta metaData)
    {
        var asset = assetInfo.Data.Asset;
        var styles = assetInfo.Data.GetSelectedStyles();
        var exportType = asset.CreationData.ExportType;
        
        return CreateExport(asset.CreationData.DisplayName, asset.CreationData.Object, styles, exportType, metaData);
    }
    
    private static BaseExport CreateExport(string name, UObject asset, FStructFallback[] styles, EExportType exportType, ExportDataMeta metaData)
    {
        var path = asset.GetPathName();
        AppWM.Message($"Exporting {name}", $"Exporting: {asset.Name}", id: path, autoClose: false);

        ExportProgressUpdate updateDelegate = (name, current, total) =>
        {
            var message = $"{current} / {total} \"{name}\"";
            AppWM.UpdateMessage(id: path, message: message);
            Log.Information(message);
        };

        metaData.UpdateProgress += updateDelegate;
        
        var primitiveType = exportType.GetPrimitiveType();
        BaseExport export = primitiveType switch
        {
            EPrimitiveExportType.Mesh => new MeshExport(name, asset, styles, exportType, metaData),
            EPrimitiveExportType.Texture => new TextureExport(name, asset, styles, exportType, metaData),
            EPrimitiveExportType.Sound => new SoundExport(name, asset, styles, exportType, metaData),
            EPrimitiveExportType.Animation => new AnimExport(name, asset, styles, exportType, metaData),
            _ => throw new NotImplementedException($"Exporting {primitiveType} assets is not supported yet.")
        };
        
        AppWM.CloseMessage(id: path);
        metaData.UpdateProgress -= updateDelegate;

        return export;
    }
    
    public static string FixPath(string path)
    {
        var outPath = path.SubstringBeforeLast(".");
        var extension = path.SubstringAfterLast(".");
        if (extension.Equals("umap"))
        {
            if (outPath.Contains("_Generated_"))
            {
                outPath += "." + path.SubstringBeforeLast("/_Generated").SubstringAfterLast("/");
            }
        }

        return outPath;
    }
}