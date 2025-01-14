﻿using System.Collections.Generic;
using System.Linq;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Material;
using FortnitePorting.Export.Models;
using FortnitePorting.Models.Assets;
using FortnitePorting.Shared.Extensions;

namespace FortnitePorting.Export.Types;

public class MaterialExport : BaseExport
{
    public readonly List<ExportMaterial> Materials = [];
    
    public MaterialExport(string name, UObject asset, BaseStyleData[] styles, EExportType exportType, ExportDataMeta metaData) : base(name, asset, styles, exportType, metaData)
    {
        Materials.AddIfNotNull(Exporter.Material((UMaterialInterface)asset, 0));
    }
}