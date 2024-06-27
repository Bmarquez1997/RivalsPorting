using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using CUE4Parse_Conversion.Textures;
using CUE4Parse.UE4.Assets.Exports.Texture;
using FortnitePorting.Framework;
using FortnitePorting.Framework.Services;
using FortnitePorting.ViewModels;
using SkiaSharp;

namespace FortnitePorting.Windows;

public partial class JsonPreviewWindow : WindowBase<JsonPreviewViewModel>
{
    public static JsonPreviewWindow? Instance;
    
    public JsonPreviewWindow(string name, string content)
    {
        InitializeComponent();
        DataContext = ViewModel;

        ViewModel.FileName = name;
        ViewModel.FileContent = content;
        ViewModel.Update();
    }

    public static void Preview(string name, string content)
    {
        if (Instance is not null)
        {
            Instance.ViewModel.FileName = name;
            Instance.ViewModel.FileContent = content;
            Instance.ViewModel.Update();
            return;
        }

        TaskService.RunDispatcher(() =>
        {
            Instance = new JsonPreviewWindow(name, content);
            Instance.Show();
        });
    }
    
    public void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    public void OnMinimizeClicked(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    public void OnMaximizeClicked(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    public void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();

        Instance = null;
    }
}