using Stride.Core.Mathematics;

namespace StrideEdExt.SharedData.ProceduralPlacement.RuntimeToEditorRequests;

public class RegenerateObjectPlacementSpawnerObjectsDataRequest : ObjectPlacementMapRequestBase
{
    public required Size2 TerrainMapTextureSize { get; init; }
}
