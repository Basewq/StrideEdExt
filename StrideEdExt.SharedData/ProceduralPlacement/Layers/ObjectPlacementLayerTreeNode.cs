namespace StrideEdExt.SharedData.ProceduralPlacement.Layers;

public record ObjectPlacementLayerTreeNode
{
    public required IObjectPlacementLayer Layer { get; init; }
    public required ObjectPlacementLayerTreeNode? ParentDensityMapLayerNode { get; init; }
    public List<ObjectPlacementLayerTreeNode> Children { get; } = [];
}
