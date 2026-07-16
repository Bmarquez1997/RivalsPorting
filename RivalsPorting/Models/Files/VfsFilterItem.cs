using CommunityToolkit.Mvvm.ComponentModel;

namespace RivalsPorting.Models.Files;

public partial class VfsFilterItem(string vfsName) : ObservableObject
{
    [ObservableProperty] private bool _isChecked = false;

    public string Title
    {
        get
        {
            if (VfsName.Contains(".o"))
                return $"UEFN: {VfsName}";

            return $"FN: {VfsName}";
        }
    }
    
    public string VfsName { get; } = vfsName;
}