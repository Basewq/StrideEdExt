namespace StrideEdExt.SharedData.ProceduralPlacement.RuntimeToEditorRequests;

public class GetOrCreateObjectPlacementLayerDataRequest : ObjectPlacementMapRequestBase
{
    public required Guid LayerId { get; init; }
    public required Type LayerDataType { get; init; }
}
