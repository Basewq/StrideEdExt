using Stride.Core.Mathematics;
using Half = System.Half;

namespace StrideEdExt.SharedData.ProceduralPlacement.EditorToRuntimeMessages;

public class SetTextureObjectDensityMapDataMessage : ObjectPlacementMapMessageBase
{
    public required Guid LayerId { get; init; }
    public required Array2d<Half> ObjectDensityMapData { get; init; }
    public required Int2? ObjectDensityMapTexturePixelStartPosition { get; init; }
}
