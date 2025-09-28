using Stride.Core.Mathematics;

namespace StrideEdExt.SharedData.ProceduralPlacement.RuntimeToEditorRequests;

public class UpdateObjectDensityMapTextureStartPositionRequest : ObjectPlacementMapRequestBase
{
    public required Guid LayerId { get; init; }
    public required Int2 ObjectDensityMapTexturePixelStartPosition { get; init; }
}
