namespace StrideEdExt.SharedData.ProceduralPlacement.RuntimeToEditorRequests;

[Obsolete]
public class UpdateObjectPlacementLayerTreeRequest : ObjectPlacementMapRequestBase
{
    public List<UpdateObjectPlacementLayerTreeNode> Layers { get; } = [];
}

public class UpdateObjectPlacementLayerTreeNode
{
    public required Guid LayerId { get; init; }
    public required Guid? ParentDensityMapLayerId { get; init; }
    public List<UpdateObjectPlacementLayerTreeNode> Children { get; } = [];
}
