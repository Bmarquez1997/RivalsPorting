using RivalsPorting.Framework;
using RivalsPorting.ViewModels.Settings;

namespace RivalsPorting.Views.Settings;

public partial class AccountSettingsView : ViewBase<AccountSettingsViewModel>
{
    public AccountSettingsView() : base(AppSettings.Account)
    {
        InitializeComponent();
    }
}