using RivalsPorting.Application;
using RivalsPorting.Models.Nodes.Material;
using RivalsPorting.Services;

namespace RivalsPorting.WindowModels;

[Transient]
public partial class MaterialPreviewWindowModel(SettingsService settings) : NodeGraphPreviewWindowModelBase<MaterialNodeTree>(settings);
