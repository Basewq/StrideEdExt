using Stride.Core.Mathematics;
using Half = System.Half;

namespace StrideEdExt.SharedData.ProceduralPlacement.EditorToRuntimeMessages;

public class SetPainterObjectDensityMapDataMessage : ObjectPlacementMapMessageBase
{
    public required Guid LayerId { get; init; }
    public required Array2d<Half> ObjectDensityMapData { get; set; }
    public required Int2? ObjectDensityMapTexturePixelStartPosition { get; set; }
}
