using Stride.Core.Mathematics;
using Half = System.Half;

namespace StrideEdExt.SharedData.Terrain3d.EditorToRuntimeMessages;

public class SetModelHeightmapDataMessage : TerrainMapMessageBase
{
    public required Guid LayerId { get; init; }
    public required Array2d<Half?> LayerHeightmapData { get; init; }
    public required Int2? HeightmapTexturePixelStartPosition { get; set; }
}
