using Stride.Core.Mathematics;
using Half = System.Half;

namespace StrideEdExt.SharedData.Terrain3d.EditorToRuntimeMessages;

public class SetPainterMaterialWeightMapDataMessage : TerrainMapMessageBase
{
    public required Guid LayerId { get; init; }
    public required Array2d<Half> MaterialWeightMapData { get; set; }
    public required Int2? MaterialWeightMapTexturePixelStartPosition { get; set; }
}
