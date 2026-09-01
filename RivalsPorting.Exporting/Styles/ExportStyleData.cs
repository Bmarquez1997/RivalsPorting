using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace RivalsPorting.Exporting.Styles;

public abstract class ExportStyleBase
{
    public string StyleName = string.Empty;
}

public class ExportObjectStyle : ExportStyleBase
{
    public UObject StyleData = null!;
    public EExportType AssociatedExportType = EExportType.None;
}

public class ExportStructStyle : ExportStyleBase
{
    public FStructFallback StyleData = null!;
}

public class ExportColorStyle : ExportStructStyle
{
    public FStructFallback ColorData = null!;
    public bool IsParamSet;
}

public class ExportRivalsFormStyle : ExportStyleBase
{
    public string HeroId = string.Empty;
    public string ShapeId = "0";
}
