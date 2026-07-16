using System;
using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Exports;
using RivalsPorting.Framework;
using RivalsPorting.Services;
using RivalsPorting.WindowModels;

namespace RivalsPorting.Windows;

public partial class ModelPreviewWindow : WindowBase<ModelPreviewWindowModel>, IPreviewWindow
{
    public static ModelPreviewWindow? Instance;
    
    public ModelPreviewWindow()
    {
        InitializeComponent();
        DataContext = WindowModel;
        Owner = App.Lifetime.MainWindow;
        
        WindowModel.InitializeContext();
    }

    public static void Preview(IEnumerable<UObject> objects)
    {
        if (Instance is null)
        {
            Instance = new ModelPreviewWindow();
            Instance.Show();
        }
        
        Instance.WindowModel.LoadScene(objects);
        Instance.BringToTop();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        Instance = null;
    }
}
