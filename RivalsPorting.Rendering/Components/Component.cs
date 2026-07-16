using RivalsPorting.Rendering.Actors;
using RivalsPorting.Rendering.Core;

namespace RivalsPorting.Rendering.Components;

public class Component(string name)
{
    public string Name = name;
    
    public Actor? Actor;
}