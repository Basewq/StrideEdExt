using Stride.Core.Mathematics;

namespace StrideEdExt.SharedData.Terrain3d.RuntimeToEditorRequests;

public class AdjustPainterHeightmapRequest : TerrainMapRequestBase
{
    public required Guid LayerId { get; init; }
    public required List<HeightmapAdjustmentRegionRequest> HeightmapAdjustmentRegions { get; init; }
}

public class HeightmapAdjustmentRegionRequest
{
    /// <summary>
    /// Values relative to the normalized heightmap values.
    /// </summary>
    public required Array2d<float> AdjustmentHeightmapData { get; init; }
    public required Int2 StartPosition { get; init; }
}
