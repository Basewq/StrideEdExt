namespace StrideEdExt.SharedData.ProceduralPlacement.Layers;

public interface IObjectDensityMapLayer : IObjectPlacementLayer
{
    ObjectDensityMapBlendType BlendType { get; }
    bool IsInverted { get; }
}
