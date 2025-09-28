using Stride.Core.Mathematics;

namespace StrideEdExt.SharedData.ProceduralPlacement.Layers.Spawners;

public class ObjectPlacementSpawnPlacementData
{
    /// <summary>
    /// Corresponds to the index on <see cref="ObjectPlacementMap.ModelAssetUrlRefList"/> or <see cref="ObjectPlacementMap.PrefabAssetUrlRefList"/>.
    /// </summary>
    public required int AssetUrlListIndex;
    // All data in world space (except SurfaceNormalModelSpace).
    public required Vector3 Position;
    public required Quaternion Orientation;
    public required Vector3 Scale;
    public required Vector3 SurfaceNormalModelSpace;

    public float GetMaxScale1d()
    {
        return Math.Max(Scale.X, Math.Max(Scale.Y, Scale.Z));
    }

    public Matrix GetWorldTransformMatrix()
    {
        Matrix.Transformation(in Scale, in Orientation, in Position, out var transformMatrix);
        return transformMatrix;
    }

    public ObjectPlacementData ToObjectPlacementData()
    {
        return new ObjectPlacementData
        {
            Position = Position,
            Orientation = Orientation,
            Scale = Scale,
            SurfaceNormalModelSpace = SurfaceNormalModelSpace,
        };
    }
}
