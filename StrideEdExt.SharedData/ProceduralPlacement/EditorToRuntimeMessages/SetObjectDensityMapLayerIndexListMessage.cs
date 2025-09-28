namespace StrideEdExt.SharedData.ProceduralPlacement.EditorToRuntimeMessages;

public class SetObjectDensityMapLayerIndexListMessage : ObjectPlacementMapMessageBase
{
    public required List<SetObjectDensityMapLayerData> MaterialLayers { get; set; }
}

public class SetObjectDensityMapLayerData
{
    public required string MaterialName { get; init; }
    public required byte MaterialIndex { get; init; }
}
