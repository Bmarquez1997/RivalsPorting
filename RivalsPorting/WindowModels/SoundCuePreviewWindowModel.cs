using RivalsPorting.Application;
using RivalsPorting.Models.Nodes.SoundCue;
using RivalsPorting.Services;

namespace RivalsPorting.WindowModels;

[Transient]
public partial class SoundCuePreviewWindowModel(SettingsService settings) : NodeGraphPreviewWindowModelBase<SoundCueNodeTree>(settings);
