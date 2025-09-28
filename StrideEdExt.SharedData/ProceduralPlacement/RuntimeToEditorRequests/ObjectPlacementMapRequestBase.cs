using Stride.Core.Assets;
using StrideEdExt.SharedData.StrideEditorExt.EditorRuntimeInterfacing;

namespace StrideEdExt.SharedData.ProceduralPlacement.RuntimeToEditorRequests;

public abstract class ObjectPlacementMapRequestBase : IRuntimeToEditorRequest
{
    public required AssetId ObjectPlacementMapAssetId { get; init; }
}
