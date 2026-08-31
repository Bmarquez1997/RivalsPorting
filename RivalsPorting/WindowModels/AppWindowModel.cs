using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RivalsPorting.Framework;
using RivalsPorting.Models.API;
using RivalsPorting.Models.API.Responses;
using RivalsPorting.Models.Information;
using RivalsPorting.Services;
using RivalsPorting.Shared.Extensions;
using RivalsPorting.Views;
using Serilog;

namespace RivalsPorting.WindowModels;

public partial class AppWindowModel(
    InfoService info,
    SettingsService settings,
    SupabaseService supabase,
    CUE4ParseService ueParse,
    BlackHoleService blackHole,
    ChatService chat,
    APIService api,
    AppService app) : WindowModelBase
{
    [ObservableProperty] private InfoService _info = info;
    [ObservableProperty] private SettingsService _settings = settings;
    [ObservableProperty] private SupabaseService _supaBase = supabase;
    [ObservableProperty] private CUE4ParseService _UEParse = ueParse;
    [ObservableProperty] private BlackHoleService _blackHole = blackHole;
    [ObservableProperty] private ChatService _chat = chat;

    private readonly APIService _api = api;
    private readonly AppService _app = app;

    [ObservableProperty] private string _versionString = Globals.Version.Identifier switch
    {
        "dev" => "dev-build",
        var hash when CommitShaMatch().IsMatch(hash) => hash,
        _ => Globals.VersionString
    };
    [ObservableProperty] private int _unreadNewsCount;
    [ObservableProperty] private int _chatNotifications;
    [ObservableProperty] private int _unsubmittedPolls;
    [ObservableProperty] private SetupView? _setupViewContent;
    [ObservableProperty] private RepositoryVersion? _updateVersion;
    [ObservableProperty] private BroadcastResponse[] _broadcasts = [];

    private const string PORTLE_URL = "https://cdn.fortniteporting.app/portle/Portle.exe";

    public override async Task Initialize()
    {
        if (!_settings.Installation.FinishedSetup)
        {
            await TaskService.RunDispatcherAsync(() =>
            {
                SetupViewContent = new SetupView();
            });
        }

        var broadcastResponse = await _api.RivalsPorting.Broadcasts();
        foreach (var broadcast in broadcastResponse.Entries)
        {
            if (!broadcast.IsEnabled)
                continue;

            var satisfiesMaxVersion = broadcast.MaxVersion is null || Globals.Version <= broadcast.MaxVersion;
            var satisfiesMinVersion = broadcast.MinVersion is null || Globals.Version >= broadcast.MinVersion;

            if (satisfiesMaxVersion && satisfiesMinVersion)
                _info.Broadcast(broadcast);
        }

        await CheckForUpdate(isAutomatic: true);
    }

    [RelayCommand]
    public async Task Update()
    {
        var remoteHash = _api.GetHash(PORTLE_URL) ?? string.Empty;

        if (!File.Exists(_settings.Developer.PortlePath) || (!_settings.Developer.UsePortlePath && !remoteHash.Equals(_settings.Developer.PortlePath.GetHash(), StringComparison.OrdinalIgnoreCase)))
        {
            Log.Information($"Updating portle executable from {PORTLE_URL} at {_settings.Developer.PortlePath}");
            await _api.DownloadFileAsync(PORTLE_URL, _settings.Developer.PortlePath);
        }

        var args = new[]
        {
            "--silent",
            "--skip-setup",
            $"--add-repository {RepositoryAPI.REPOSITORY_URL}",
            $"--import-profile \"Rivals Porting\" \"{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppDomain.CurrentDomain.FriendlyName + ".exe")}\" \"RivalsPorting\"",
            "--update-profile \"Rivals Porting\" -force",
            "--launch-profile \"Rivals Porting\"",
        };
        
        Info.Message("Portle", $"Rivals Porting {UpdateVersion!.Version} is currently being downloaded.");

        await Task.Delay(2500);

        Process.Start(new ProcessStartInfo
        {
            FileName = _settings.Developer.PortlePath,
            Arguments = string.Join(' ', args),
            WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
            UseShellExecute = true
        });

        _app.Shutdown();
    }

    public async Task CheckForUpdate(bool isAutomatic = false)
    {
        if (Globals.IsDevBuild) return;

        // Version feed comes from GitHub Repository.json (same as v3).
        // Online services (broadcasts, auth, chat, etc.) continue to use Api.RivalsPorting.
        var repositoryInfo = await Api.Repository.GetRepositoryAsync();
        var newestVersion = repositoryInfo?.Versions.MaxBy(version => version.UploadTime);
        if (newestVersion is null || newestVersion.Version <= Globals.Version)
        {
            if (!isAutomatic)
            {
                Info.Dialog("No Update Available", "Rivals Porting is up to date.");
            }
            return;
        }
        
        UpdateVersion = newestVersion;

        if (!isAutomatic)
        {
            Info.Dialog($"Update {newestVersion.Version}",
                $"Rivals Porting {newestVersion.Version} is now available. Would you like to update?",
                buttons:
                [
                    new DialogButton
                    {
                        Text = "Update",
                        Action = () => TaskService.Run(Update)
                    }
                ]);
            return;
        }

        if (DateTime.Today > newestVersion.UploadTime.AddDays(6))
        {
            var outOfDateDays = DateTime.Today - newestVersion.UploadTime;
            Info.Dialog($"Update {newestVersion.Version}", $"Your Rivals Porting is {outOfDateDays.Days} days out of date, please consider updating.", buttons: [
                new DialogButton
                {
                    Text = "Update",
                    IsPrimary = true,
                    Action = () => TaskService.Run(Update)
                },
                new DialogButton
                {
                    Text = "Cancel"
                }
            ]);
        }
    }

    [GeneratedRegex(@"^[0-9a-f]{7}$")]
    private static partial Regex CommitShaMatch();
}
