using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using FortnitePorting.Framework;
using FortnitePorting.Services;
using FortnitePorting.Shared.Services;
using FortnitePorting.WindowModels;

namespace FortnitePorting.Windows;

public partial class SoundBankPreviewWindow : WindowBase<SoundBankPreviewWindowModel>
{
    public static SoundBankPreviewWindow? Instance;
    
    public SoundBankPreviewWindow(string assetName, List<string> tracks)
    {
        InitializeComponent();
        DataContext = WindowModel;
        Owner = ApplicationService.Application.MainWindow;

        WindowModel.SoundName = assetName;
        WindowModel.Tracks = GetTracks(tracks);
        TaskService.Run(WindowModel.Play);
    }

    public static void Preview(string assetName, List<string> tracks)
    {
        if (Instance is not null)
        {
            Instance.WindowModel.SoundName = assetName;
            Instance.WindowModel.Tracks = GetTracks(tracks);
            TaskService.Run(Instance.WindowModel.Play);
            Instance.BringToTop();
            return;
        }

        TaskService.RunDispatcher(() =>
        {
            Instance = new SoundBankPreviewWindow(assetName, tracks);
            Instance.Show();
            Instance.BringToTop();
        });
    }

    private static List<SoundBankTrack> GetTracks(List<string> trackPaths)
    {
        return trackPaths.Select(track => new SoundBankTrack(track)).ToList();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        Instance?.WindowModel.OutputDevice.Dispose();
        Instance = null;
    }

    private void OnSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (sender is not Slider slider) return;
        WindowModel.Scrub(TimeSpan.FromSeconds(slider.Value));
    }
    
    private void OnTrackPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control) return;
        if (control.DataContext is not SoundBankTrack track) return;

        WindowModel.ChangeTrack(WindowModel.Tracks[WindowModel.Tracks.IndexOf(track)]);
    }
}