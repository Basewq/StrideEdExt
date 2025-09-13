namespace StrideEdExt.SharedData.Terrain3d.RuntimeToEditorRequests;

public class TerrainMapEditorDestroyedRequest : TerrainMapRequestBase
{
    public required Guid SceneId { get; init; }
    public required Guid EditorEntityId { get; init; }
}
