using RivalsPorting.Controls.Navigation.Sidebar;
using RivalsPorting.Framework;
using RivalsPorting.ViewModels.Settings;

namespace RivalsPorting.Views.Settings;

public partial class FolderSettingsView : SettingsSectionViewBase<FolderSettingsViewModel>
{
    public FolderSettingsView() : base(AppSettings.ExportSettings.Folder)
    {
        InitializeComponent();
    }

    private void OnItemSelected(object? sender, SidebarItemSelectedArgs e)
    {
        ApplySidebarSection(SectionContent, e);
    }
}
