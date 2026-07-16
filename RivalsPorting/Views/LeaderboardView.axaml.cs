using RivalsPorting.Controls.Navigation.Sidebar;
using RivalsPorting.Framework;
using RivalsPorting.ViewModels;

namespace RivalsPorting.Views;

public partial class LeaderboardView : ViewBase<LeaderboardViewModel>
{
    public LeaderboardView()
    {
        InitializeComponent();
        
        Navigation.Leaderboard.Initialize(Sidebar, ContentFrame);
    }
    
    private void OnItemSelected(object? sender, SidebarItemSelectedArgs e)
    {
        Navigation.Leaderboard.Open(e.Tag);
    }
}