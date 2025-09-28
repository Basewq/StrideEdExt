namespace StrideEdExt.SharedData.ProceduralPlacement.RuntimeToEditorRequests;

public class ObjectPlacementMapEditorReadyRequest : ObjectPlacementMapRequestBase
{
    public required Guid SceneId { get; init; }
    public required Guid EditorEntityId { get; init; }
}
