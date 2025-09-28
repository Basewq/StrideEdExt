namespace StrideEdExt.SharedData.ProceduralPlacement.Layers;

public interface IObjectPlacementLayer
{
    Guid LayerId { get; }
    Type LayerDataType { get; }
}
