using RivalsPorting.Controls.Navigation.Sidebar;
using RivalsPorting.Framework;
using RivalsPorting.ViewModels;

namespace RivalsPorting.Views;

public partial class SettingsView : ViewBase<SettingsViewModel>
{
    public SettingsView()
    {
        InitializeComponent();
        
        Navigation.Settings.Initialize(Sidebar, ContentFrame);
    }

    private void OnItemSelected(object? sender, SidebarItemSelectedArgs e)
    {
        Navigation.Settings.Open(e.Tag);
    }
}