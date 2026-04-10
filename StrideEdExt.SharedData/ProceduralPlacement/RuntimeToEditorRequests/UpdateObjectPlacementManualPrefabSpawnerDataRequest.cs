using Stride.Core.Mathematics;

namespace StrideEdExt.SharedData.ProceduralPlacement.RuntimeToEditorRequests;

public class UpdateObjectPlacementManualPrefabSpawnerDataRequest : ObjectPlacementMapRequestBase
{
    public required Guid LayerId { get; init; }
    public required List<UpsertObjectPlacementManualPrefabTransform>? UpsertPrefabTransformList { get; init; }
    public required List<DeleteObjectPlacementManualPrefabTransform>? DeletePrefabTransformList { get; init; }
}

public class UpsertObjectPlacementManualPrefabTransform
{
    public required Guid SpawnInstancingId { get; init; }
    public required bool IsEnabled { get; init; }
    public required string? PrefabUrl { get; init; }
    public required float CollisionRadius { get; init; }
    public required Vector3 Position { get; init; }
    public required Quaternion Orientation { get; init; }
    public required Vector3 Scale { get; init; }
}

public class DeleteObjectPlacementManualPrefabTransform
{
    public required Guid SpawnInstancingId { get; init; }
}
