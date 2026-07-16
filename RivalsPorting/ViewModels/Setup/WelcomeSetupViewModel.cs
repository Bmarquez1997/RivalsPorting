using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using RivalsPorting.Application;
using Microsoft.Extensions.DependencyInjection;
using RivalsPorting.Framework;
using RivalsPorting.Views.Setup;

namespace RivalsPorting.ViewModels.Setup;

public partial class WelcomeSetupViewModel : ViewModelBase
{
    
    [RelayCommand]
    public async Task Continue()
    {
        Navigation.Setup.Open<ApplicationSetupView>();
    }
}
