using RivalsPorting.Rendering.Components.Rendering;
using RivalsPorting.Rendering.Components;
using RivalsPorting.Rendering.Core;

namespace RivalsPorting.Rendering.Actors;

public class CameraActor : Actor
{
    public CameraComponent Camera { get; }

    public CameraActor(string name) : base(name)
    {
        Camera = new CameraComponent();
        Components.Add(Camera);
    }
}