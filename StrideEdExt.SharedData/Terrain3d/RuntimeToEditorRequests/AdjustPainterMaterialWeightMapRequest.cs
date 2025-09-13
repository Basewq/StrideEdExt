using Stride.Core.Mathematics;

namespace StrideEdExt.SharedData.Terrain3d.RuntimeToEditorRequests;

public class AdjustPainterMaterialWeightMapRequest : TerrainMapRequestBase
{
    public required Guid LayerId { get; init; }
    public required List<MaterialWeightMapAdjustmentRegionRequest> WeightMapAdjustmentRegions { get; init; }
}

public class MaterialWeightMapAdjustmentRegionRequest
{
    public required Array2d<float> AdjustmentWeightMapData { get; init; }
    public required Int2 StartPosition { get; init; }
}
