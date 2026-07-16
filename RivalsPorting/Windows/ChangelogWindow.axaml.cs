using System;
using System.Linq;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Rendering;
using FluentAvalonia.UI.Controls;
using RivalsPorting.Extensions;
using RivalsPorting.Framework;
using RivalsPorting.Models.AvaloniaEdit;
using RivalsPorting.Services;
using RivalsPorting.WindowModels;
using PropertiesContainer = RivalsPorting.Models.Viewers.PropertiesContainer;

namespace RivalsPorting.Windows;

public partial class ChangelogWindow : WindowBase<ChangelogWindowModel>
{
    public static ChangelogWindow? Instance;
    
    public ChangelogWindow()
    {
        InitializeComponent();
        DataContext = WindowModel;
        Owner = App.Lifetime.MainWindow;
    }

    public static void Preview(string? text)
    {
        text ??= "No Description.";
        
        if (Instance == null)
        {
            Instance = new ChangelogWindow();
            Instance.Show();
        }
        
        Instance.BringToTop();

        Instance.Editor.Document.Text = text;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        
        Instance = null;
    }
}