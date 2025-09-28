namespace StrideEdExt.SharedData.ProceduralPlacement.Layers.Spawners;

public interface IObjectSpawnerLayerData
{
    List<ObjectSpawnAssetDefinition> SpawnAssetDefinitionList { get; }
    List<ObjectPlacementSpawnPlacementData> SpawnPlacementDataList { get; }
}
