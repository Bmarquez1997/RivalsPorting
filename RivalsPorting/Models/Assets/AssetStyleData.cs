using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CUE4Parse_Conversion.Textures;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.i18N;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.Utils;
using RivalsPorting.Extensions;
using RivalsPorting.Framework;
using RivalsPorting.Models.Clipboard;
using RivalsPorting.Views;

namespace RivalsPorting.Models.Assets;

public abstract partial class BaseStyleData : ObservableObject
{
    [ObservableProperty] private string _styleName;
    [ObservableProperty] private Bitmap? _styleDisplayImage;
    [ObservableProperty] private bool _showName = true;
    
    [RelayCommand]
    public virtual async Task CopyIcon()
    {
        await AvaloniaClipboard.SetImageAsync(StyleDisplayImage);
    }
    
    [RelayCommand]
    public virtual async Task CopyPath()
    {
        Info.Message("Unsupported Asset", "Cannot copy the path of this type of asset.");
    }
    
    [RelayCommand]
    public virtual async Task NavigateTo()
    {
        Info.Message("Unsupported Asset", "Cannot navigate to this type of asset.");
    }
}

public partial class AssetStyleData : BaseStyleData
{
    [ObservableProperty] private FStructFallback _styleData;
    
    public AssetStyleData(FStructFallback styleData, Bitmap previewImage)
    {
        StyleData = styleData;
        
        var name = StyleData.GetOrDefault("SkinName", StyleData.GetOrDefault("VariantName", new FText("Unnamed"))).Text.ToLower().TitleCase();
        if (string.IsNullOrWhiteSpace(name)) name = "Unnamed";
        StyleName = name;
        
        StyleDisplayImage = previewImage;
    }
    
    public AssetStyleData(string name, FStructFallback styleData, Bitmap previewImage)
    {
        StyleData = styleData;
        StyleName = name;
        StyleDisplayImage = previewImage;
    }
}

public partial class AssetColorStyleData : AssetStyleData
{
    [ObservableProperty] private FStructFallback _colorData;
    [ObservableProperty] private bool _isParamSet;
    
    public AssetColorStyleData(string name, FStructFallback styleData, FStructFallback colorData, Bitmap previewImage, bool isParamSet = false) : base(name, styleData, previewImage)
    {
        ColorData = colorData;
        IsParamSet = isParamSet;
    }
}

public partial class ObjectStyleData : BaseStyleData
{
    [ObservableProperty] private UObject _styleData;
    [ObservableProperty] private EExportType _associatedExportType = EExportType.None;
    
    public ObjectStyleData(string name, UObject styleData, Bitmap? previewImage)
    {
        ShowName = false;
        StyleData = styleData;
        StyleName = name;
        StyleDisplayImage = previewImage;
    }
    
    public override async Task CopyPath()
    {
        await App.Clipboard.SetTextAsync(StyleData.GetPathName());
    }

    public override async Task NavigateTo()
    {
        Navigation.App.Open<FilesView>();

        var assetPath = UEParse.Provider.FixPath(StyleData.GetPathName().SubstringBefore("."));
        FilesVM.JumpTo(assetPath);
        
        AppWM.Window.BringToTop();
    }
}

public class AnimStyleData : ObjectStyleData
{
    public AnimStyleData(string name, UObject styleData) : base(name, styleData, null)
    {
        ShowName = true;
    }
}

/// <summary>Holds an animation soft path so the montage/sequence is only loaded on export.</summary>
public class SoftAnimStyleData : BaseStyleData
{
    public string AnimPath { get; }

    public SoftAnimStyleData(string name, string animPath)
    {
        StyleName = name;
        AnimPath = animPath;
        ShowName = true;
    }

    public override async Task CopyPath()
    {
        await App.Clipboard.SetTextAsync(AnimPath);
    }

    public override async Task NavigateTo()
    {
        Navigation.App.Open<FilesView>();
        FilesVM.JumpTo(UEParse.Provider.FixPath(AnimPath.SubstringBefore(".")));
        AppWM.Window.BringToTop();
    }
}

/// <summary>Holds a texture soft path so the texture is only loaded on export.</summary>
public class SoftTextureStyleData : BaseStyleData
{
    public string TexturePath { get; }

    public SoftTextureStyleData(string name, string texturePath, Bitmap? previewImage = null)
    {
        StyleName = name;
        TexturePath = texturePath;
        StyleDisplayImage = previewImage;
        ShowName = true;
    }

    public override async Task CopyPath()
    {
        await App.Clipboard.SetTextAsync(TexturePath);
    }

    public override async Task NavigateTo()
    {
        Navigation.App.Open<FilesView>();
        FilesVM.JumpTo(UEParse.Provider.FixPath(TexturePath.SubstringBefore(".")));
        AppWM.Window.BringToTop();
    }
}

/// <summary>Lobby (ShowActor) vs in-game (LikeActor) model selection for outfits.</summary>
public class ModelStyleData : BaseStyleData
{
    public bool UseGameModel { get; }

    public ModelStyleData(string name, bool useGameModel, Bitmap? previewImage)
    {
        StyleName = name;
        UseGameModel = useGameModel;
        StyleDisplayImage = previewImage;
        ShowName = true;
    }
}

/// <summary>Hero shape/form selection for multi-shape outfits (e.g. Cloak vs Dagger).</summary>
public class FormStyleData : BaseStyleData
{
    public string HeroId { get; }
    public string ShapeId { get; }

    public FormStyleData(string name, string heroId, string shapeId, Bitmap? previewImage)
    {
        StyleName = name;
        HeroId = heroId;
        ShapeId = shapeId;
        StyleDisplayImage = previewImage;
        ShowName = true;
    }
}