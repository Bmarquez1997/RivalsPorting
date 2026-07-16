using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RivalsPorting.Models.Installation;
using Newtonsoft.Json;
using RivalsPorting.Framework;
using RivalsPorting.Services;
using RivalsPorting.Views.Setup;
using Serilog;

namespace RivalsPorting.ViewModels.Setup;

public partial class ApplicationSetupViewModel() : ViewModelBase
{
    [ObservableProperty] private SettingsService _settings;

    public ApplicationSetupViewModel(SettingsService settings) : this()
    {
        Settings = settings;
    }
    
    [RelayCommand]
    public async Task Continue()
    {
        Navigation.Setup.Open<InstallationSetupView>();
    }
}