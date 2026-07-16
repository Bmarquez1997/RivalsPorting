using CommunityToolkit.Mvvm.ComponentModel;
using RivalsPorting.Models.Supabase.Tables;


namespace RivalsPorting.Models.Supabase.User;

public partial class UserPermissions : ObservableObject
{
    [ObservableProperty] private ESupabaseRole _role = ESupabaseRole.User;
    [ObservableProperty] private bool _canExportUEFN = false;
    [ObservableProperty] private bool _isMuted = false;
}