namespace StrideEdExt.SharedData.ProceduralPlacement.EditorToRuntimeMessages;

public class SetObjectPlacementObjectDataMessage : ObjectPlacementMapMessageBase
{
    public required List<string> ModelAssetUrlList { get; init; }
    public required List<string> PrefabAssetUrlList { get; init; }

    public required Dictionary<Guid, List<ModelObjectPlacementsData>> LayerIdToModelPlacementDataList { get; init; }
    public required Dictionary<Guid, List<PrefabObjectPlacementsData>> LayerIdToPrefabPlacementDataList { get; init; }
}
