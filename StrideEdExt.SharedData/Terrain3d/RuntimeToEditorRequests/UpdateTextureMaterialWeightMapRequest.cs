using Stride.Core.Mathematics;
using Half = System.Half;

namespace StrideEdExt.SharedData.Terrain3d.RuntimeToEditorRequests;

public class UpdateTextureMaterialWeightMapRequest : TerrainMapRequestBase
{
    public required Guid LayerId { get; init; }
    public required Int2 MaterialWeightMapTexturePixelStartPosition { get; init; }
    public required Array2d<Half>? MaterialWeightMapData { get; init; }
}
