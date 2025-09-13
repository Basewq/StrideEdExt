using Stride.Core.Mathematics;

namespace StrideEdExt.SharedData.Terrain3d.EditorToRuntimeMessages;

public class SetTextureHeightmapDataMessage : TerrainMapMessageBase
{
    public required Guid LayerId { get; init; }
    public required Array2d<float> LayerHeightmapData { get; init; }
    public required Int2? HeightmapTexturePixelStartPosition { get; set; }
}
