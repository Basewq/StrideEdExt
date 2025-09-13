using Stride.Core.Assets;
using StrideEdExt.SharedData.StrideEditorExt.EditorRuntimeInterfacing;

namespace StrideEdExt.SharedData.Terrain3d.EditorToRuntimeMessages;

public abstract class TerrainMapMessageBase : IEditorToRuntimeMessage
{
    public required AssetId TerrainMapAssetId { get; set; }
}
