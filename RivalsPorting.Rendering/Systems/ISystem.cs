using RivalsPorting.Rendering.Components;
using RivalsPorting.Rendering.Components.Rendering;

namespace RivalsPorting.Rendering.Systems;

public interface ISystem
{
    public Type[] ComponentTypes { get; }
    
    public void Update(float deltaTime);
    public void Render(CameraComponent camera);
    
    public void RegisterComponent(Component component);
    public void UnregisterComponent(Component component);
}

public static class SystemExtensions
{
    extension(ISystem system)
    {
        public bool Supports(Type targetType)
        {
            return system.ComponentTypes.Any(targetType.IsAssignableTo);
        }
    }
}