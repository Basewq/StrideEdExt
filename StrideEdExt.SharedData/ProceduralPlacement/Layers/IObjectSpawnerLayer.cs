namespace StrideEdExt.SharedData.ProceduralPlacement.Layers;

public interface IObjectSpawnerLayer : IObjectPlacementLayer
{
    float ObjectSpacing { get; }

    float MinimumDensityValueThreshold { get; }

    float SurfaceNormalMinimumAngleDegrees { get; }
    float SurfaceNormalMaximumAngleDegrees { get; }

    bool AlignWithSurfaceNormal { get; }

    float PositionOffsetMinimumRadius { get; }
    float PositionOffsetMaximumRadius { get; }

    float RotationYOffsetMinimumAngleDegrees { get; }
    float RotationYOffsetMaximumAngleDegrees { get; }

    float ScaleMinimum { get; }
    float ScaleMaximum { get; }
}
