using RivalsPorting.Framework;
using RivalsPorting.ViewModels.Plugin;

namespace RivalsPorting.Views.Plugin;

public partial class BlenderPluginView : ViewBase<BlenderPluginViewModel>
{
    public BlenderPluginView() : base(AppSettings.Plugin.Blender)
    {
        InitializeComponent();
    }
}