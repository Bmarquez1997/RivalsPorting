using System.Threading.Tasks;
using RivalsPorting.Models.Leaderboard;

namespace RivalsPorting.ViewModels.Leaderboard;

public partial class LeaderboardLevelsViewModel : PagedLeaderboardViewModelBase<LeaderboardUserLevel>
{
    protected override string PageCountFunctionName => "leaderboard_levels_page_count";
    protected override string PageDataFunctionName => "leaderboard_levels";

    protected override async Task LoadItem(LeaderboardUserLevel item)
    {
        await item.Load();
    }
}