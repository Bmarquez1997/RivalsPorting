using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using FortnitePorting.Installer.Services;
using FortnitePorting.Installer.Views;
using FortnitePorting.Shared.Framework;
using FortnitePorting.Shared.Models;
using FortnitePorting.Shared.Services;
using FortnitePorting.Shared.Validators;

namespace FortnitePorting.Installer.ViewModels;

public partial class InstallViewModel : ViewModelBase
{
    [ObservableProperty] private string _mainTitle = "Installing";
    [ObservableProperty] private string _subTitle = "Beginning Installation Process";
    [ObservableProperty] private bool _isFinished;
    
    [ObservableProperty] private FileInfo _installedFile;
    
    private static readonly DirectoryInfo TempDirectory = new(Path.GetTempPath());

    public override async Task Initialize()
    {
        if (IntroVM.InstallDependencies)
        {
            foreach (var dependency in IntroVM.ReleaseInfo.Dependencies)
            {
                try
                {
                    MainTitle = $"Installing: {dependency.Name}";
                    SubTitle = $"Downloading {dependency.URL}";
                    var downloadedFile = await ApiVM.DownloadFileAsync(dependency.URL, TempDirectory);

                    SubTitle = $"Running Setup for {downloadedFile.Name}";
                    using var dependencyProcess = new Process();
                    dependencyProcess.StartInfo = new ProcessStartInfo
                    {
                        FileName = downloadedFile.FullName,
                        Arguments = "/install /quiet /norestart",
                        UseShellExecute = true
                    };

                    dependencyProcess.Start();
                    await dependencyProcess.WaitForExitAsync();

                    downloadedFile.Delete();
                }
                catch (Exception e)
                {
                    await TaskService.RunDispatcherAsync(async () =>
                    {
                        var dialog = new ContentDialog
                        {
                            Title = "An Error Occurred",
                            Content = $"Failed to install dependency {dependency.Name}:.\nThis may result in issues when using Fortnite Porting.\nError: {e.GetType().FullName} {e.Message}",
                            CloseButtonText = "Continue"
                        };
                        await dialog.ShowAsync();
                    });
                }
            }
        }
        
        MainTitle = $"Installing: RivalsPorting {IntroVM.ReleaseInfo.Version.GetDisplayString(EVersionStringType.IdentifierPrefix)}";
        SubTitle = $"Downloading {IntroVM.ReleaseInfo.Download}";

        var installationDirectory = new DirectoryInfo(IntroVM.InstallationPath);
        installationDirectory.Create();
        
        InstalledFile = await ApiVM.DownloadFileAsync(IntroVM.ReleaseInfo.Download, installationDirectory);

        MainTitle = "Installation Complete";
        SubTitle = "Please press continue to finalize the installation process.";

        IsFinished = true;
    }

    [RelayCommand]
    public async Task Continue()
    {
        AppWM.SetView<FinishedView>();
    }
    
}