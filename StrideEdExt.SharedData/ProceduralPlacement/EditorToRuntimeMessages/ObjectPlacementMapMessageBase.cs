using Stride.Core.Assets;
using StrideEdExt.SharedData.StrideEditorExt.EditorRuntimeInterfacing;

namespace StrideEdExt.SharedData.ProceduralPlacement.EditorToRuntimeMessages;

public abstract class ObjectPlacementMapMessageBase : IEditorToRuntimeMessage
{
    public required AssetId ObjectPlacementMapAssetId { get; set; }
}
