using RivalsPorting.Framework;
using RivalsPorting.ViewModels.Settings;

namespace RivalsPorting.Views.Settings;

public partial class DeveloperSettingsView : ViewBase<DeveloperSettingsViewModel>
{
    public DeveloperSettingsView() : base(AppSettings.Developer)
    {
        InitializeComponent();
    }
}