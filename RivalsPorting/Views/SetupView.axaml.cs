using RivalsPorting.Framework;
using RivalsPorting.ViewModels;
using RivalsPorting.Views.Setup;

namespace RivalsPorting.Views;

public partial class SetupView : ViewBase<SetupViewModel>
{
    public SetupView()
    {
        InitializeComponent();
        DataContext = ViewModel;
        
        Navigation.Setup.Initialize(ContentFrame);
        Navigation.Setup.Open<WelcomeSetupView>();
    }
}
