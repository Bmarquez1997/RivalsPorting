using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RivalsPorting.Application;
using RivalsPorting.Framework;
using RivalsPorting.Models.Viewers;
using RivalsPorting.Services;

namespace RivalsPorting.WindowModels;

[Transient]
public partial class TexturePreviewWindowModel(SettingsService settings) : WindowModelBase
{
    [ObservableProperty] private SettingsService _settings = settings;

    [ObservableProperty] private ObservableCollection<TextureContainer> _textures = [];
    [ObservableProperty] private TextureContainer _selectedTexture;

}