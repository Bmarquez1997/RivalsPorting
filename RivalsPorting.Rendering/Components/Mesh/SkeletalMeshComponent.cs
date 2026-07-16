using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using RivalsPorting.Rendering.Renderers;

namespace RivalsPorting.Rendering.Components.Mesh;

public class SkeletalMeshComponent(USkeletalMesh mesh) : MeshComponent(new SkeletalMeshRenderer(mesh));