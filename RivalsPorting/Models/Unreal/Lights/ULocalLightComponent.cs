using CUE4Parse.UE4.Assets.Exports;

namespace RivalsPorting.CUE4Parse.Models.Unreal.Lights;

public class ULocalLightComponent : ULightComponent
{
    [UProperty] public float InverseExposureBlend;
    [UProperty] public float AttenuationRadius;
}