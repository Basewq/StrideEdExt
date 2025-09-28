using Stride.Core.Mathematics;

namespace StrideEdExt.SharedData.ProceduralPlacement.RuntimeToEditorRequests;

public class AdjustPainterObjectDensityMapRequest : ObjectPlacementMapRequestBase
{
    public required Guid LayerId { get; init; }
    public required List<ObjectDensityMapAdjustmentRegionRequest> ObjectDensityMapAdjustmentRegions { get; init; }
}

public class ObjectDensityMapAdjustmentRegionRequest
{
    public required Array2d<float> AdjustmentObjectDensityMapData { get; init; }
    public required Int2 StartPosition { get; init; }
}
