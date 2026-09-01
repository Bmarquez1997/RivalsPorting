using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CUE4Parse.Utils;
using RivalsPorting.Application;
using RivalsPorting.Exporting.Extensions;
using RivalsPorting.Extensions;
using RivalsPorting.Framework;
using RivalsPorting.Services;
using Material.Icons;
using NAudio.Wave;

namespace RivalsPorting.WindowModels;

[Transient]
public partial class SoundBankPreviewWindowModel(SettingsService settings) : WindowModelBase
{
    [ObservableProperty] private SettingsService _settings = settings;

    [ObservableProperty] private string _soundName = string.Empty;
    [ObservableProperty] private List<SoundBankTrack> _tracks = [];
    [ObservableProperty] private SoundBankTrack? _activeTrack;

    [ObservableProperty] private TimeSpan _currentTime;
    [ObservableProperty] private TimeSpan _totalTime;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(PauseIcon))] private bool _isPaused;
    public MaterialIconKind PauseIcon => IsPaused ? MaterialIconKind.Play : MaterialIconKind.Pause;

    public WaveFileReader? AudioReader;
    public WaveOutEvent OutputDevice = new() { DeviceNumber = AppSettings.Application.AudioDeviceIndex };

    private readonly DispatcherTimer UpdateTimer = new();

    public override async Task Initialize()
    {
        UpdateTimer.Tick += OnUpdateTimerTick;
        UpdateTimer.Interval = TimeSpan.FromMilliseconds(1);
        UpdateTimer.Start();
    }

    public override async Task OnViewExited()
    {
        OutputDevice.Dispose();
        if (AudioReader is not null)
            await AudioReader.DisposeAsync();
    }

    private void OnUpdateTimerTick(object? sender, EventArgs e)
    {
        if (AudioReader is null) return;

        TotalTime = AudioReader.TotalTime;
        CurrentTime = AudioReader.CurrentTime;

        if (CurrentTime >= TotalTime)
        {
            Next();
        }
    }

    public async Task Play()
    {
        if (Tracks.Count <= 0) return;
        Play(Tracks.First());
    }

    public void Play(SoundBankTrack track)
    {
        if (!SoundExtensions.TryOpenAudioStream(track.Path, out var stream) || stream is null) return;

        AudioReader = new WaveFileReader(stream);
        SetActiveTrack(track);

        OutputDevice.Stop();
        OutputDevice.Init(AudioReader);

        TaskService.Run(() =>
        {
            OutputDevice.Play();
            while (OutputDevice.PlaybackState != PlaybackState.Stopped) { }
            OutputDevice.Stop();
        });
    }

    public void TogglePause()
    {
        IsPaused = !IsPaused;

        if (IsPaused)
        {
            OutputDevice.Pause();
        }
        else
        {
            OutputDevice.Play();
        }
    }

    public void Previous()
    {
        if (ActiveTrack is null) return;

        var previousTrackIndex = Tracks.IndexOf(ActiveTrack) - 1;
        if (previousTrackIndex < 0) previousTrackIndex = Tracks.Count - 1;
        if (AudioReader?.CurrentTime.TotalSeconds > 5)
        {
            Restart();
            return;
        }

        CurrentTime = TimeSpan.Zero;
        ChangeTrack(Tracks[previousTrackIndex]);
    }

    public void Next()
    {
        if (ActiveTrack is null) return;

        var nextTrackIndex = Tracks.IndexOf(ActiveTrack) + 1;
        if (nextTrackIndex >= Tracks.Count)
        {
            nextTrackIndex = 0;
        }

        CurrentTime = TimeSpan.Zero;
        ChangeTrack(Tracks[nextTrackIndex]);
    }

    public void ChangeTrack(SoundBankTrack newTrack)
    {
        Play(newTrack);
    }

    private void SetActiveTrack(SoundBankTrack track)
    {
        void Apply()
        {
            if (ActiveTrack != null)
                ActiveTrack.IsPlaying = false;
            ActiveTrack = track;
            track.IsPlaying = true;
        }

        if (Dispatcher.UIThread.CheckAccess())
            Apply();
        else
            Dispatcher.UIThread.Post(Apply);
    }

    public void Scrub(TimeSpan time)
    {
        if (AudioReader is null) return;
        AudioReader.CurrentTime = time;
    }

    public void Restart()
    {
        if (AudioReader is null) return;
        AudioReader.CurrentTime = TimeSpan.Zero;
        CurrentTime = TimeSpan.Zero;
    }

    public void UpdateOutputDevice()
    {
        OutputDevice.Stop();
        OutputDevice = new WaveOutEvent { DeviceNumber = AppSettings.Application.AudioDeviceIndex };
        if (AudioReader is null) return;

        OutputDevice.Init(AudioReader);

        if (!IsPaused)
        {
            OutputDevice.Play();
        }
    }
}

public partial class SoundBankTrack(string trackPath) : ObservableObject
{
    public string Name { get; } = trackPath.SubstringAfterLast('/');
    public string Path { get; } = trackPath;

    [ObservableProperty] private bool _isPlaying;
}
