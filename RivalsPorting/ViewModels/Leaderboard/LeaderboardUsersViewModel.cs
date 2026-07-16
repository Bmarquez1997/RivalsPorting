using System.Threading.Tasks;
using RivalsPorting.Models.Leaderboard;

namespace RivalsPorting.ViewModels.Leaderboard;

public partial class LeaderboardUsersViewModel : PagedLeaderboardViewModelBase<LeaderboardUser>
{
    protected override string PageCountFunctionName => "leaderboard_users_page_count";
    protected override string PageDataFunctionName => "leaderboard_users";

    protected override async Task LoadItem(LeaderboardUser item)
    {
        await item.Load();
    }
}