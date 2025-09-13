using Stride.Core.Mathematics;
using Half = System.Half;

namespace StrideEdExt.SharedData.Terrain3d.RuntimeToEditorRequests;

public class UpdateModelHeightmapRequest : TerrainMapRequestBase
{
    public required Guid LayerId { get; init; }
    public required Int2 HeightmapTexturePixelStartPosition { get; init; }
    public required Array2d<Half?> HeightmapData { get; init; }
}
