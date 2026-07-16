using RivalsPorting.Framework;
using RivalsPorting.ViewModels.Plugin;

namespace RivalsPorting.Views.Plugin;

public partial class UnrealPluginView : ViewBase<UnrealPluginViewModel>
{
    public UnrealPluginView() : base(AppSettings.Plugin.Unreal)
    {
        InitializeComponent();
    }
}