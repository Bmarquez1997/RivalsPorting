using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Utils;
using CUE4Parse.UE4.Objects.UObject;

namespace RivalsPorting.Models.Fortnite;

[StructFallback]
public class FStyleParameter<T>
{
    [UProperty] public T Value;
    [UProperty] public FName ParamName;
    public string Name => ParamName.Text;

} 
