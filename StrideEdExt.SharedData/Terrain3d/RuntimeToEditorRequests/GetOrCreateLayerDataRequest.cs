namespace StrideEdExt.SharedData.Terrain3d.RuntimeToEditorRequests;

public class GetOrCreateLayerDataRequest : TerrainMapRequestBase
{
    public required Guid LayerId { get; init; }
    public required Type LayerDataType { get; init; }
}
