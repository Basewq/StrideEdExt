using Stride.Core.Mathematics;

namespace StrideEdExt.SharedData.Terrain3d.EditorToRuntimeMessages;

public class SetTerrainMapHeightmapDataMessage : TerrainMapMessageBase
{
    public required Size2 HeightmapTextureSize { get; init; }
    public required Array2d<float> HeightmapData { get; init; }
}
