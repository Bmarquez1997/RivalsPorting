using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.i18N;
using FortnitePorting.Application;
using FortnitePorting.Controls;
using FortnitePorting.Controls.Assets;
using FortnitePorting.Extensions;
using FortnitePorting.Framework;
using FortnitePorting.Services;
using FortnitePorting.ViewModels;
using AssetItem = FortnitePorting.Controls.Assets.AssetItem;

namespace FortnitePorting.Views;

public partial class AssetsView : ViewBase<AssetsViewModel>
{
    public AssetsView()
    {
        InitializeComponent();
    }
    
    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        await TaskService.RunAsync(async () => await ViewModel.Initialize());
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox listBox) return;
        if (listBox.SelectedItem is not AssetItem asset) return;
        
        ViewModel.CurrentAsset = asset;
        ViewModel.ExtraOptions.Clear();
        var styles = asset.Asset.GetOrDefault("ItemVariants", Array.Empty<UObject>());
        foreach (var style in styles)
        {
            var channel = style.GetOrDefault("VariantChannelName", new FText("Style")).Text.ToLower().TitleCase();
            var optionsName = style.ExportType switch
            {
                "FortCosmeticCharacterPartVariant" => "PartOptions",
                "FortCosmeticMaterialVariant" => "MaterialOptions",
                "FortCosmeticParticleVariant" => "ParticleOptions",
                "FortCosmeticMeshVariant" => "MeshOptions",
                "FortCosmeticGameplayTagVariant" => "GenericTagOptions",
                _ => null
            };

            if (optionsName is null) continue;

            var options = style.Get<FStructFallback[]>(optionsName);
            if (options.Length == 0) continue;

            var styleSelector = new StyleItem(channel, options, asset.IconBitmap);
            if (styleSelector.Styles.Count == 0) continue;
            ViewModel.ExtraOptions.Add(styleSelector);
        }
    }

    private async void OnAssetTypeClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.CurrentLoader is null) return;
        if (sender is not ToggleButton toggleButton) return;
        if (toggleButton.Tag is not EAssetType assetType) return;
        
        var buttons = AssetTypePanel.Children.OfType<ToggleButton>();
        foreach (var button in buttons)
        {
            if (button.Tag is not EAssetType buttonAssetType) continue;
            button.IsChecked = buttonAssetType == assetType;
        }
        
        if (ViewModel.CurrentLoader.Type == assetType) return;

        ViewModel.SetLoader(assetType);
        
        var loaders = ViewModel.Loaders;
        foreach (var loader in loaders)
        {
            if (loader.Type == assetType)
            {
                loader.Pause.Unpause();
            }
            else
            {
                loader.Pause.Pause();
            }
        }
        
        await ViewModel.CurrentLoader.Load();
    }

    private void OnScrollStyles(object? sender, PointerWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer) return;
        switch (e.Delta.Y)
        {
            case < 0:
                scrollViewer.LineLeft();
                break;
            case > 0:
                scrollViewer.LineRight();
                break;
        }
    }

    private void OnFilterChecked(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox) return;
        if (checkBox.Content is null) return;
        if (!checkBox.IsChecked.HasValue) return;

        ViewModel.ModifyFilters(checkBox.Content.ToString()!, checkBox.IsChecked.Value);
    }

    private void OnFilterClearClicked(object? sender, RoutedEventArgs e)
    {
        ViewModel.Filters.Clear();
        foreach (var child in FilterPopupPanel.Children)
        {
            if (child is not CheckBox checkBox) continue;
            checkBox.IsChecked = false;
        }
    }
}