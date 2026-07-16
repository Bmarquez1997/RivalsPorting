using System.Threading.Tasks;
using RivalsPorting.Models.Leaderboard;

namespace RivalsPorting.ViewModels.Leaderboard;

public partial class LeaderboardStreaksViewModel : PagedLeaderboardViewModelBase<LeaderboardStreak>
{
    protected override string PageCountFunctionName => "leaderboard_streaks_page_count";
    protected override string PageDataFunctionName => "leaderboard_streaks";

    protected override async Task LoadItem(LeaderboardStreak item)
    {
        await item.Load();
    }
}