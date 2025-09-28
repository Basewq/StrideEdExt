namespace StrideEdExt.SharedData.Terrain3d.RuntimeToEditorRequests;

public class TerrainMapEditorDestroyedRequest : TerrainMapRequestBase
{
    public required Guid EditorEntityId { get; init; }
    public required Guid? SceneId { get; init; }
}
