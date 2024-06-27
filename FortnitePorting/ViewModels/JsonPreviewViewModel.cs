using System;
using System.ComponentModel;
using System.Numerics;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CUE4Parse_Conversion.Sounds;
using CUE4Parse_Conversion.Textures;
using CUE4Parse.UE4.Assets.Exports.Texture;
using FortnitePorting.Extensions;
using FortnitePorting.Framework;
using FortnitePorting.Framework.ViewModels;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SkiaSharp;

namespace FortnitePorting.ViewModels;

public partial class JsonPreviewViewModel : ViewModelBase
{
    [ObservableProperty] private string _fileName = string.Empty;
    [ObservableProperty] private string _fileContent = string.Empty;
    [ObservableProperty] private double _fontSize;
    
    [ObservableProperty] private ThemedViewModelBase theme;

    public JsonPreviewViewModel()
    {
        Theme = ThemeVM;
    }
    
    public void Update()
    {
        FontSize = 14;
    }
    
    [RelayCommand]
    public async Task Copy()
    {
        await Clipboard.SetTextAsync(FileContent);
    }
    
    [RelayCommand]
    public async Task Save()
    {
        
    }
    
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        
        if (e.PropertyName == nameof(FontSize))
        {
            Console.Out.WriteLine("Font Changed: " + e);
        }
    }
}