using System;
using RivalsPorting.Framework;
using RivalsPorting.WindowModels;

namespace RivalsPorting.Windows;

public partial class ChangelogWindow : WindowBase<ChangelogWindowModel>, IPreviewWindow
{
    public ChangelogWindow()
    {
        InitializeComponent();
        DataContext = WindowModel;
        Owner = App.Lifetime.MainWindow;
    }

    public static void Preview(string? text)
    {
        text ??= "No Description.";

        var window = WindowManager.GetOrShowPreview(() => new ChangelogWindow());
        window.Editor.Document.Text = text;
    }
}
