using RivalsPorting.Controls.Navigation.Sidebar;
using RivalsPorting.Framework;
using RivalsPorting.ViewModels.Settings;

namespace RivalsPorting.Views.Settings;

public partial class UnrealSettingsView : SettingsSectionViewBase<UnrealSettingsViewModel>
{
    public UnrealSettingsView() : base(AppSettings.ExportSettings.Unreal)
    {
        InitializeComponent();
    }

    private void OnItemSelected(object? sender, SidebarItemSelectedArgs e)
    {
        ApplySidebarSection(SectionContent, e);
    }
}
