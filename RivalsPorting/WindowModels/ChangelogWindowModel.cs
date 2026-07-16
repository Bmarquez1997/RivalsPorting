using System;
using System.Collections.ObjectModel;
using System.Xml;
using Avalonia.Platform;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using CommunityToolkit.Mvvm.ComponentModel;
using RivalsPorting.Application;
using RivalsPorting.Framework;
using RivalsPorting.Services;
using PropertiesContainer = RivalsPorting.Models.Viewers.PropertiesContainer;

namespace RivalsPorting.WindowModels;

[Transient]
public partial class ChangelogWindowModel(SettingsService settings) : WindowModelBase
{
    [ObservableProperty] private SettingsService _settings = settings;

    [ObservableProperty] private string _text = string.Empty;

    public static IHighlightingDefinition JsonHighlighter { get; set; }

    static ChangelogWindowModel()
    {
        using var stream = AssetLoader.Open(new Uri("avares://RivalsPorting/Assets/Highlighters/Changelog.xshd"));
        using var reader = new XmlTextReader(stream);
        JsonHighlighter = HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }
}