using Stride.Core.Mathematics;

namespace StrideEdExt.SharedData.Terrain3d.RuntimeToEditorRequests;

public class UpdateTerrainMapLayerOrderingRequest : TerrainMapRequestBase
{
    public List<Guid> LayerId { get; } = [];
}
