using Stride.Core.Mathematics;

namespace StrideEdExt.SharedData.Terrain3d.RuntimeToEditorRequests;

public class UpdateTextureHeightmapRequest : TerrainMapRequestBase
{
    public required Guid LayerId { get; init; }
    public required Int2 HeightmapTexturePixelStartPosition { get; init; }
    public required Array2d<float>? HeightmapData { get; init; }
}
