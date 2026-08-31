using RivalsPorting.Controls.Navigation.Sidebar;
using RivalsPorting.Framework;
using RivalsPorting.ViewModels.Settings;

namespace RivalsPorting.Views.Settings;

public partial class BlenderSettingsView : SettingsSectionViewBase<BlenderSettingsViewModel>
{
    public BlenderSettingsView() : base(AppSettings.ExportSettings.Blender)
    {
        InitializeComponent();
    }

    private void OnItemSelected(object? sender, SidebarItemSelectedArgs e)
    {
        ApplySidebarSection(SectionContent, e);
    }
}
