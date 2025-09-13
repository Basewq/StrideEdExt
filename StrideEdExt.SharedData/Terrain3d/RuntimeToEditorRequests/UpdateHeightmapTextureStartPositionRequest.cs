using Stride.Core.Mathematics;

namespace StrideEdExt.SharedData.Terrain3d.RuntimeToEditorRequests;

public class UpdateHeightmapTextureStartPositionRequest : TerrainMapRequestBase
{
    public required Guid LayerId { get; init; }
    public required Int2 HeightmapTexturePixelStartPosition { get; init; }
}
