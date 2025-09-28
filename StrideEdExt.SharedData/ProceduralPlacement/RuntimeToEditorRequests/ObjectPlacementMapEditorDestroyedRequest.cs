namespace StrideEdExt.SharedData.ProceduralPlacement.RuntimeToEditorRequests;

public class ObjectPlacementMapEditorDestroyedRequest : ObjectPlacementMapRequestBase
{
    public required Guid EditorEntityId { get; init; }
    public required Guid? SceneId { get; init; }
}
