using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using RivalsPorting.Controls.Navigation.Sidebar;
using RivalsPorting.Framework;
using RivalsPorting.Models.Installation;
using RivalsPorting.ViewModels.Settings;

namespace RivalsPorting.Views.Settings;

public partial class InstallationSettingsView : ViewBase<InstallationSettingsViewModel>
{
    public InstallationSettingsView() : base(AppSettings.Installation)
    {
        InitializeComponent();
    }

    // spaces aint working so easy fix ??
    private void OnTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        if (e.Key != Key.Space) return;

        textBox.Text = textBox.Text!.Insert(textBox.CaretIndex, " ");
        textBox.CaretIndex++;
    }
}