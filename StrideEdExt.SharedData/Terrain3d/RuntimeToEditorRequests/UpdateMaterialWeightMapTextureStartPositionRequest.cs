using Stride.Core.Mathematics;

namespace StrideEdExt.SharedData.Terrain3d.RuntimeToEditorRequests;

public class UpdateMaterialWeightMapTextureStartPositionRequest : TerrainMapRequestBase
{
    public required Guid LayerId { get; init; }
    public required Int2 MaterialWeightMapTexturePixelStartPosition { get; init; }
}
