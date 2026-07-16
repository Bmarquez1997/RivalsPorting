using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using RivalsPorting.Application;
using RivalsPorting.Framework;
using RivalsPorting.Models.Files;
using RivalsPorting.Services;
using ReactiveUI;

namespace RivalsPorting.WindowModels;

[Transient]
public partial class FilePickerWindowModel(SettingsService settings) : WindowModelBase
{
    [ObservableProperty] private SettingsService _settings = settings;
    
    [ObservableProperty] private FileBrowserContext _context = new();
    [ObservableProperty] private string _windowName = "File Picker";
    [ObservableProperty] private string? _startPath;

    public override async Task Initialize()
    {
        Context.Initialize(startPath: StartPath);
    }
}