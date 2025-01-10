using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CUE4Parse.UE4.Versions;
using FluentAvalonia.UI.Controls;
using FortnitePorting.Application;
using FortnitePorting.Models.CUE4Parse;
using FortnitePorting.Services;
using FortnitePorting.Shared;
using FortnitePorting.Shared.Validators;
using Newtonsoft.Json;

namespace FortnitePorting.Models.Settings;

public partial class InstallationProfile : ObservableValidator
{
    [ObservableProperty] private string _profileName = "Unnammed";
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EncryptionKeyEnabled))]
    [NotifyPropertyChangedFor(nameof(MappingsFileEnabled))]
    [NotifyPropertyChangedFor(nameof(IsCustom))]
    private EFortniteVersion _fortniteVersion = EFortniteVersion.LatestInstalled;
    
    [NotifyDataErrorInfo]
    [ArchiveDirectory]
    [ObservableProperty] private string _archiveDirectory;
    
    [ObservableProperty] private EGame _unrealVersion = EGame.GAME_MarvelRivals;
    
    [NotifyDataErrorInfo]
    [EncryptionKey]
    [ObservableProperty] 
    private FileEncryptionKey _mainKey = new("0x0C263D8C22DCB085894899C3A3796383E9BF9DE0CBFB08C9BF2DEF2E84F29D74");
    
    [ObservableProperty] private int _selectedExtraKeyIndex;
    [ObservableProperty] private ObservableCollection<FileEncryptionKey> _extraKeys = [
        new("0xF959B39D10C93808116F4D0C5583E1D11CBCCD428E737A48B75D40EC87FBF9D8"),
        new("0xFCFC4D709BC395492703482C50DC423744B5931272587ACCD78B0E57D7215BDD"),
        new("0x9F3F11DA58B6DD43266CE124F60E955C4A6BE7D5E4B23B69E63EFB0718DA952B"),
        new("0xD7BA72F24C18357A2384399D98ACF9DB40DD03A55ED4128A396D3D7697930FB5"),
    ];
    
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(MappingsFileEnabled))]
    private bool _useMappingsFile = true;
    
    [ObservableProperty] private string _mappingsFile = DependencyService.MappingsFile.FullName;
    
    [ObservableProperty] private ELanguage _gameLanguage = ELanguage.English;

    [JsonIgnore] public bool IsCustom => FortniteVersion is EFortniteVersion.Custom;
    [JsonIgnore] public bool EncryptionKeyEnabled => IsCustom;
    [JsonIgnore] public bool MappingsFileEnabled => IsCustom;
    
    public async Task BrowseArchivePath()
    {
        if (await BrowseFolderDialog() is { } path)
        {
            ArchiveDirectory = path;
        }
    }
    
    public async Task BrowseMappingsFile()
    {
        if (await BrowseFileDialog(fileTypes: Globals.MappingsFileType, suggestedFileName: MappingsFile) is { } path)
        {
            MappingsFile = path;
        }
    }
    
    public async Task AddEncryptionKey()
    {
        ExtraKeys.Add(FileEncryptionKey.Empty);
    }
    
    public async Task RemoveEncryptionKey()
    {
        var selectedIndexToRemove = SelectedExtraKeyIndex;
        ExtraKeys.RemoveAt(selectedIndexToRemove);
        SelectedExtraKeyIndex = selectedIndexToRemove == 0 ? 0 : selectedIndexToRemove - 1;
    }

    public override string ToString()
    {
        return ProfileName;
    }
}