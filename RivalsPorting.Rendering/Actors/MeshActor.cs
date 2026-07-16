using CUE4Parse.GameTypes.FN.Assets.Exports.DataAssets;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.UObject;
using RivalsPorting.Rendering.Components.Mesh;
using RivalsPorting.Rendering.Core;

namespace RivalsPorting.Rendering.Actors;

public class MeshActor : Actor
{
    public MeshComponent MeshComponent;
    
    public MeshActor(UStaticMesh mesh, Transform? transform = null, List<KeyValuePair<UBuildingTextureData, int>>? textureData = null) : base(mesh.Name)
    {
        MeshComponent = new StaticMeshComponent(mesh, textureData)
        {
            Transform = transform ?? Transform.Identity
        };
        
        Components.Add(MeshComponent);
    }
    
    public MeshActor(USkeletalMesh mesh, Transform? transform = null) : base(mesh.Name)
    {
        MeshComponent = new SkeletalMeshComponent(mesh)
        {
            Transform = transform ?? Transform.Identity
        };
        
        Components.Add(MeshComponent);
    }
}