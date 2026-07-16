using System.Linq;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using RivalsPorting.Controls;
using RivalsPorting.Controls.Navigation.Sidebar;
using RivalsPorting.Framework;
using RivalsPorting.Services;
using RivalsPorting.ViewModels.Settings;

namespace RivalsPorting.Views.Settings;

public partial class BlenderSettingsView : ViewBase<BlenderSettingsViewModel>
{
    private readonly EntranceTransition _transition = new();
    private CancellationTokenSource _cts = new();
    
    public BlenderSettingsView() : base(AppSettings.ExportSettings.Blender)
    {
        InitializeComponent();
    }

    private void OnItemSelected(object? sender, SidebarItemSelectedArgs e)
    {
        if (e.Tag is not Control control) return;
        
        SectionContent.Content = control;
        
        _cts.Cancel();
        _cts = new CancellationTokenSource();

        SectionContent.Content = control;
        TaskService.RunDispatcher(async () => await _transition.Start(null, SectionContent, true, _cts.Token));
    }
}