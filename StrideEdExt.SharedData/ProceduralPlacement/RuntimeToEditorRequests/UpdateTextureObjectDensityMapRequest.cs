using Stride.Core.Mathematics;
using Half = System.Half;

namespace StrideEdExt.SharedData.ProceduralPlacement.RuntimeToEditorRequests;

public class UpdateTextureObjectDensityMapRequest : ObjectPlacementMapRequestBase
{
    public required Guid LayerId { get; init; }
    public required Int2 ObjectDensityMapTexturePixelStartPosition { get; init; }
    public required Vector2 ObjectDensityMapTextureScale { get; init; }
    public required Array2d<Half>? ObjectDensityMapData { get; init; }
}
