using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using RivalsPorting.Controls.Navigation.Sidebar;
using RivalsPorting.Framework;
using RivalsPorting.Models.Radio;
using RivalsPorting.ViewModels;
using RivalsPorting.Windows;

namespace RivalsPorting.Views;

public partial class MusicView : ViewBase<MusicViewModel>
{
    public MusicView()
    {
        InitializeComponent();
    }

    private void OnPlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (sender is not Control control) return;
        if (control.DataContext is not MusicPackItem musicPackItem) return;
        
        var window = MusicPlayerWindow.Open();
        if (window.WindowModel is not { } player) return;

        if (player.ActiveItem == musicPackItem)
            player.TogglePlayPause();
        else
            player.PlayItem(musicPackItem);
    }

    private void OnContextMenuPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control) return;
        FlyoutBase.ShowAttachedFlyout(control);
    }
}