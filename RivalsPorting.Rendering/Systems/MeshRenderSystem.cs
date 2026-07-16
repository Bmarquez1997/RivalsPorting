using CUE4Parse.UE4.Objects.Core.Math;
using RivalsPorting.Rendering.Components;
using RivalsPorting.Rendering.Components.Mesh;
using RivalsPorting.Rendering.Components.Rendering;
using RivalsPorting.Rendering.Exceptions;
using RivalsPorting.Rendering.Extensions;
using RivalsPorting.Rendering.Renderers;

namespace RivalsPorting.Rendering.Systems;

public class MeshRenderSystem : ISystem
{
    public Type[] ComponentTypes { get; } =
    [
        typeof(MeshComponent)
    ];

    private HashSet<MeshComponent> _registeredComponents = [];

    public FBox GetBounds()
    {
        var bounds = new FBox();
        foreach (var component in _registeredComponents)
        {
            var localBounds = component.Renderer.BoundingBox * 0.01f;
        
            var worldMatrix = component.WorldMatrix.ToFMatrix();
            var worldBounds = localBounds.TransformBy(worldMatrix);
        
            bounds += worldBounds;
        }
        
        return bounds;
    }
    
    public void Update(float deltaTime)
    {
        foreach (var component in _registeredComponents)
        {
            component.Renderer.Update(deltaTime);
        }
    }

    public void Render(CameraComponent camera)
    {
        foreach (var component in _registeredComponents)
        {
            component.Renderer.Render(camera);
        }
    }

    public void RegisterComponent(Component component)
    {
        if (component is not MeshComponent meshComponent) return;

        if (!_registeredComponents.Add(meshComponent))
        {
            throw new RenderingXException("Mesh component has already been registered with this mesh render system.");
        }
    }

    public void UnregisterComponent(Component component)
    {
        if (component is not MeshComponent meshComponent) return;

        if (!_registeredComponents.Remove(meshComponent))
        {
            throw new RenderingXException("Mesh component is not registered with this mesh render system.");
        }
    }
}