using System;
using RivalsPorting.Controls.Navigation.Sidebar;
using RivalsPorting.Framework;
using RivalsPorting.ViewModels;

namespace RivalsPorting.Views;

public partial class PluginView : ViewBase<PluginViewModel>
{
    public PluginView() : base(AppSettings.Plugin)
    {
        InitializeComponent();
        Navigation.Plugin.Initialize(Sidebar, ContentFrame);
        Navigation.Plugin.AddTypeResolver<EExportLocation>(location =>
        {
            var name = location.ToString();
            var viewName = $"RivalsPorting.Views.Plugin.{name}PluginView";
        
            var type = Type.GetType(viewName);
            return type;
        });
    }
    
    private void OnItemSelected(object? sender, SidebarItemSelectedArgs e)
    {
        if (e.Tag is not EExportLocation exportLocation) return;

        Navigation.Plugin.Open(exportLocation);
    }
}