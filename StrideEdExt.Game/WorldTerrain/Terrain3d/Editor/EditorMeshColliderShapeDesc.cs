using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Physics;

namespace StrideEdExt.WorldTerrain.Terrain3d.Editor;

[DataContract]
public class EditorMeshColliderShapeDesc : IInlineColliderShapeDesc
{
    public required Vector3[] VertexPositions;
    public required int[] VertexIndices;

    public bool Match(object obj)
    {
        bool isMatch = obj is EditorMeshColliderShapeDesc collShapeDesc
            && collShapeDesc.VertexPositions.AsSpan().SequenceEqual(VertexPositions.AsSpan())
            && collShapeDesc.VertexIndices.AsSpan().SequenceEqual(VertexIndices.AsSpan());
        return isMatch;
    }

    public ColliderShape CreateShape(IServiceRegistry services)
    {
        return new StaticMeshColliderShape(VertexPositions, VertexIndices);
    }
}
