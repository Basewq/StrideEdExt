using Stride.Core.Mathematics;

namespace StrideEdExt.SharedData.Terrain3d.RuntimeToEditorRequests;

public class UpdateLayerOrderingRequest : TerrainMapRequestBase
{
    public List<Guid> LayerId { get; } = [];
}
