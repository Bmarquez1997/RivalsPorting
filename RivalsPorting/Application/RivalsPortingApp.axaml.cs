using System;
using System.IO;
using System.Linq;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using FluentAvalonia.Styling;
using RivalsPorting.Shared.Extensions;

namespace RivalsPorting.Application;

public partial class RivalsPortingApp : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
        BindingPlugins.DataValidators.RemoveAll(validator => validator is DataAnnotationsValidationPlugin);

        ApplyThemeAccentColor();
        
        AppServices.Initialize();
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            App.InitializeDesktop(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static void ApplyThemeAccentColor()
    {
        if (Current is not { } app) return;
        if (app.Styles.OfType<FluentAvaloniaTheme>().FirstOrDefault() is not { } fluentTheme) return;

        if (app.TryGetResource("FPAccentColor", app.ActualThemeVariant, out var resource) && resource is Color accentColor)
        {
            fluentTheme.CustomAccentColor = accentColor;
        }
    }
    
}