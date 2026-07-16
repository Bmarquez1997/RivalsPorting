using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using RivalsPorting.Framework;
using RivalsPorting.Shared.Extensions;
using RivalsPorting.Models.Leaderboard;

namespace RivalsPorting.ViewModels.Leaderboard;

public partial class LeaderboardExportsViewModel : PagedLeaderboardViewModelBase<LeaderboardExport>
{
    protected override string PageCountFunctionName => "leaderboard_exports_page_count";
    protected override string PageDataFunctionName => "leaderboard_exports";

    protected override async Task LoadItem(LeaderboardExport item)
    {
        await item.Load();
    }
}