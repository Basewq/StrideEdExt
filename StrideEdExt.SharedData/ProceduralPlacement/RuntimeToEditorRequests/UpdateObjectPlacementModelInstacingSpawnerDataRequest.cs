using StrideEdExt.SharedData.ProceduralPlacement.Layers.Spawners;

namespace StrideEdExt.SharedData.ProceduralPlacement.RuntimeToEditorRequests;

public class UpdateObjectPlacementModelInstacingSpawnerDataRequest : ObjectPlacementMapRequestBase
{
    public required Guid LayerId { get; init; }
    public required float MinimumDensityValueThreshold { get; init; }
    public required ObjectPlacementModelType ModelType { get; init; }
    public required List<ObjectSpawnAssetDefinition> ObjectSpawnAssetDefinitionList { get; init; }
}
