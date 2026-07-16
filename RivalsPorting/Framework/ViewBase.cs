using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using RivalsPorting.Application;
using RivalsPorting.Services;

namespace RivalsPorting.Framework;

public abstract class ViewBase<T> : UserControl where T : ViewModelBase
{
    protected readonly T ViewModel;

    public ViewBase(T? templateViewModel = null)
    {
        ViewModel = templateViewModel ?? AppServices.Services.GetRequiredService<T>();
        DataContext = ViewModel;
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (!ViewModel.IsInitialized)
        {
            ViewModel.IsInitialized = true;
            TaskService.Run(ViewModel.Initialize);
        }

        await ViewModel.OnViewOpened();
    }

    protected override async void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        
        await ViewModel.OnViewExited();
    }
}
