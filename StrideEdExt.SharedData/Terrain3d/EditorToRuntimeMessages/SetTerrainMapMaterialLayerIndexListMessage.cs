namespace StrideEdExt.SharedData.Terrain3d.EditorToRuntimeMessages;

public class SetTerrainMapMaterialLayerIndexListMessage : TerrainMapMessageBase
{
    public required List<SetTerrainMapMaterialLayerData> MaterialLayers { get; set; }
}

public class SetTerrainMapMaterialLayerData
{
    public required string MaterialName { get; init; }
    public required byte MaterialIndex { get; init; }
}
