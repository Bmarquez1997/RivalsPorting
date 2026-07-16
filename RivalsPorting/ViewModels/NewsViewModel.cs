using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using RivalsPorting.Framework;
using RivalsPorting.Models.API.Responses;
using RivalsPorting.Windows;

namespace RivalsPorting.ViewModels;

public partial class NewsViewModel : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<NewsEntry> _news = [];

    public override async Task OnViewOpened()
    {
        var newsResponse = await Api.RivalsPorting.News();
        News = [..newsResponse.Entries];
    }
    
    public void OpenNews(NewsEntry news)
    {
        ChangelogWindow.Preview(news.Description);
    }
}