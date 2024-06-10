using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CUE4Parse.UE4.Versions;
using FluentAvalonia.UI.Controls;
using FortnitePorting.Application;
using FortnitePorting.Export.Models;
using FortnitePorting.Models.Radio;
using FortnitePorting.Shared;
using FortnitePorting.Shared.Framework;
using FortnitePorting.ViewModels.Settings;
using NAudio.Wave;
using Newtonsoft.Json;

namespace FortnitePorting.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [JsonIgnore] public Frame ContentFrame;
    [JsonIgnore] public NavigationView NavigationView;
    
    // ViewModels
    [ObservableProperty] private ExportSettingsViewModel _exportSettings = new();
    [ObservableProperty] private InstallationSettingsViewModel _installation = new();
    [ObservableProperty] private ApplicationSettingsViewModel _application = new();
    
    // Welcome
    [ObservableProperty] private bool _finishedWelcomeScreen;
    

    // Filtered Data
    [ObservableProperty] private HashSet<string> _filteredProps = [];

    // Radio
    [ObservableProperty] private RadioPlaylistSerializeData[] _playlists = [];
    [ObservableProperty] private int _audioDeviceIndex = 0;
    [ObservableProperty] private float _volume = 1.0f;

    public ExportDataMeta CreateExportMeta() => new()
    {
        AssetsRoot = Application.AssetPath,
        Settings = ExportSettings.Blender
    };
    
    public void Navigate<T>()
    {
        Navigate(typeof(T));
    }
    
    public void Navigate(Type type)
    {
        ContentFrame.Navigate(type, null, AppSettings.Current.Application.Transition);

        var buttonName = type.Name.Replace("SettingsView", string.Empty);
        NavigationView.SelectedItem = NavigationView.MenuItems
            .Concat(NavigationView.FooterMenuItems)
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => (string) item.Tag! == buttonName);
    }
    
}