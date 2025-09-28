using StrideEdExt.SharedData.ProceduralPlacement.Layers.Spawners;

namespace StrideEdExt.SharedData.ProceduralPlacement.RuntimeToEditorRequests;

public class UpdateObjectPlacementPrefabSpawnerDataRequest : ObjectPlacementMapRequestBase
{
    public required Guid LayerId { get; init; }
    public required float MinimumDensityValueThreshold { get; init; }
    public required List<ObjectSpawnAssetDefinition> ObjectSpawnAssetDefinitionList { get; init; }
}
