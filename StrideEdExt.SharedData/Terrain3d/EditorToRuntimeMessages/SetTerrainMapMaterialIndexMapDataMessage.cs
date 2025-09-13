using Stride.Core.Mathematics;
using Half = System.Half;

namespace StrideEdExt.SharedData.Terrain3d.EditorToRuntimeMessages;

public class SetTerrainMapMaterialIndexMapDataMessage : TerrainMapMessageBase
{
    public required Size2 HeightmapTextureSize { get; init; }
    public required Array2d<byte> MaterialIndexMapData { get; init; }
    public required Array2d<Half> MaterialWeightMapData { get; init; }
}
