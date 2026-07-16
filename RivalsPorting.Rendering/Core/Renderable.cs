using RivalsPorting.Rendering.Components.Rendering;
using RivalsPorting.Rendering.Components;

namespace RivalsPorting.Rendering.Core;

public class Renderable
{
    public virtual void Initialize() { }
    public virtual void Update(float deltaTime) { }
    public virtual void Render(CameraComponent camera) { }
    public virtual void Destroy() { }
}