using RivalsPorting.Rendering.Renderers;
using RivalsPorting.Rendering.Components.Rendering;

namespace RivalsPorting.Rendering.Components.Mesh;

public class MeshComponent : SpatialComponent
{
    public readonly MeshRenderer Renderer;

    public MeshComponent(MeshRenderer renderer)
    {
        Renderer = renderer;
        
        Renderer.Component = this;
        Renderer.Initialize();
    }
}