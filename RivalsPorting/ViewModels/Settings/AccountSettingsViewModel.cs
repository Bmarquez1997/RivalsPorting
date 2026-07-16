using CommunityToolkit.Mvvm.ComponentModel;
using RivalsPorting.Framework;
using Newtonsoft.Json;
using RivalsPorting.Application;
using RivalsPorting.Models.Supabase.User;
using RivalsPorting.Services;

namespace RivalsPorting.ViewModels.Settings;

public partial class AccountSettingsViewModel : SettingsViewModelBase
{
   [JsonIgnore] public SupabaseService SupaBase => AppServices.SupaBase;

   [ObservableProperty] private string? _sessionInfoEncrypted = null;

   [ObservableProperty] private bool _useDiscordRichPresence = true;

   partial void OnUseDiscordRichPresenceChanged(bool value)
   {
      if (value)
         Discord.Initialize();
      else
         Discord.Deinitialize();
   }
}