using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using RivalsPorting.Extensions;
using Newtonsoft.Json;
using RivalsPorting.Models.API.Responses;
using RivalsPorting.Models.Supabase.Tables;

namespace RivalsPorting.Models.Leaderboard;

public partial class LeaderboardUser : ObservableObject
{
    [ObservableProperty] [JsonProperty("rank")] private int _ranking;
    [ObservableProperty] [JsonProperty("user_id")] private string _userId;

    [ObservableProperty] [JsonProperty("total")] private int _exportCount;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(UserBrush))]
    private UserInfoResponse? _userInfo;

    public SolidColorBrush UserBrush => UserInfo?.Role.Brush() ?? ESupabaseRole.User.Brush();

    public async Task Load()
    {
        UserInfo = await SupaBase.GetUserAsync(UserId);
    }
}
