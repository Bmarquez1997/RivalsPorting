using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using RivalsPorting.Controls.WrapPanel;
using RivalsPorting.Framework;
using RivalsPorting.Models.Files;
using RivalsPorting.Services;
using RivalsPorting.ViewModels;

namespace RivalsPorting.Views;

public partial class FilesView : ViewBase<FilesViewModel>
{
    public FilesView() : base(FilesVM)
    {
        InitializeComponent();
    }
    
    private void OnFileItemTapped(TreeItem item)
    {
        if (item.Type != ENodeType.File) return;
        
        TaskService.RunDispatcher(async () => await ViewModel.Preview());
    }
}