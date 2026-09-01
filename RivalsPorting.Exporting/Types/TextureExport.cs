using System;
using System.Collections.Generic;
using System.Linq;
using CUE4Parse.GameTypes.FN.Assets.Exports.DataAssets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Texture;
using RivalsPorting.CUE4Parse.Extensions;
using RivalsPorting.CUE4Parse.Models.Unreal.VirtualTexture;
using RivalsPorting.Exporting.Models;
using RivalsPorting.Exporting.Models.Files.Meta;
using RivalsPorting.Exporting.Styles;
using RivalsPorting.Shared.Extensions;
using Path = System.IO.Path;

namespace RivalsPorting.Exporting.Types;

public class TextureExport : BaseExport
{
    public List<ExportTexture> Textures = [];
    public List<string> FolderPaths = [];

    private static readonly Dictionary<EExportType, string> TextureNames = new()
    {
        { EExportType.Spray, "DecalTexture" },
        { EExportType.Banner, "LargePreviewImage" },
        { EExportType.LoadingScreen, "BackgroundImage" },
        { EExportType.Emoticon, "SpriteSheet" }
    };
    
    public TextureExport(string name, UObject asset, EExportType exportType, ExportDataMeta metaData, IExportFileMeta? fileMeta) : this(name, asset, [], exportType, metaData, fileMeta)
    {
    }

    public TextureExport(string name, UObject asset, ExportStyleBase[] styles, EExportType exportType, ExportDataMeta metaData, IExportFileMeta? fileMeta) : base(name, exportType, metaData)
    {
        if (styles.Length > 0 && !string.Equals(styles[0].StyleName, name, StringComparison.Ordinal))
            Name = $"{name} - {styles[0].StyleName}";

        var textures = new List<UTexture>();
        var textureStyles = styles.OfType<ExportObjectStyle>()
            .Where(style => style.StyleData is UTexture)
            .ToArray();
        if (textureStyles.Length > 0)
        {
            foreach (var textureStyle in textureStyles)
            {
                textures.Add((UTexture) textureStyle.StyleData);
            }
        }
        else
        {
            switch (asset)
            {
                case UVirtualTextureBuilder virtualTextureBuilder:
                {
                    textures.AddIfNotNull(virtualTextureBuilder.Texture.Load<UVirtualTexture2D>());
                    break;
                }
                case UTexture texture:
                {
                    textures.Add(texture);
                    break;
                }
                case UBuildingTextureData textureData:
                {
                    textures.AddIfNotNull(textureData.Diffuse.Load<UTexture2D>());
                    textures.AddIfNotNull(textureData.Normal.Load<UTexture2D>());
                    textures.AddIfNotNull(textureData.Specular.Load<UTexture2D>());
                    break;
                }
                default:
                {
                    textures.AddIfNotNull(asset.GetOrDefault<UTexture2D?>(TextureNames[exportType]) ?? asset.GetDataListItem<UTexture2D>("LargeIcon", "Icon"));
                    break;
                }
            }
        }

        foreach (var texture in textures)
        {
            if (metaData.ExportLocation.IsFolder)
            {
                var exportPath = Context.Export(texture, returnRealPath: true, synchronousExport: true);
                if (Path.GetDirectoryName(exportPath) is { } exportFolder)
                    FolderPaths.Add(exportFolder);
            }
            else
            {
                Textures.Add(new ExportTexture(Context.Export(texture), texture.SRGB, texture.CompressionSettings));
            }
        }
    }
    
}
