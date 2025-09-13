using Stride.Core.Assets;
using StrideEdExt.SharedData.StrideEditorExt.EditorRuntimeInterfacing;

namespace StrideEdExt.SharedData.Terrain3d.RuntimeToEditorRequests;

public abstract class TerrainMapRequestBase : IRuntimeToEditorRequest
{
    public required AssetId TerrainMapAssetId { get; init; }
}
