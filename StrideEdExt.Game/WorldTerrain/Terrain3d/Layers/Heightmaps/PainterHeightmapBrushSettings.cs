using Stride.Core;
using Stride.Core.Annotations;
using StrideEdExt.Painting;
using StrideEdExt.Painting.Brushes;

namespace StrideEdExt.WorldTerrain.Terrain3d.Layers.Heightmaps;

[DataContract]
public enum HeightmapPaintModeType
{
    Raise,
    Lower,
    Smooth,
    Flatten,
}

[DataContract]
public class PainterHeightmapBrushSettings
{
    private HeightmapPaintModeType _paintModeType;
    public HeightmapPaintModeType PaintModeType { get => _paintModeType; set => SetValue(ref _paintModeType, value); }

    private float _brushDiameter = 5;
    /// <summary>
    /// Brush diameter in world units.
    /// </summary>
    [DataMemberRange(minimum: 0.1, maximum: 100, smallStep: 0.1, largeStep: 1, decimalPlaces: 2)]
    public float BrushDiameter { get => _brushDiameter; set => SetValue(ref _brushDiameter, value); }

    private float _terrainHeight = 1;
    [DataMemberRange(minimum: 0.01, maximum: 100, smallStep: 0.1, largeStep: 1, decimalPlaces: 2)]
    public float TerrainHeight { get => _terrainHeight; set => SetValue(ref _terrainHeight, value); }

    private float _opacity = 1;
    [DataMemberRange(minimum: 0, maximum: 1, smallStep: 0.01, largeStep: 0.1, decimalPlaces: 2)]
    public float Opacity { get => _opacity; set => SetValue(ref _opacity, value); }

    private float _stampSpacingPercentage = 10;
    /// <summary>
    /// Distance between each brush stamp draw, as a percentage of <see cref="BrushDiameter"/>.
    /// </summary>
    [Display("Stamp Spacing %")]
    [DataMemberRange(minimum: 0.1, maximum: 1000, smallStep: 1, largeStep: 10, decimalPlaces: 2)]
    public float StampSpacingPercentage { get => _stampSpacingPercentage; set => SetValue(ref _stampSpacingPercentage, value); }

    private PaintBrushShapeBase _brushShape = new PaintCircularBrushShape();
    [Display(Expand = ExpandRule.Once)]
    public PaintBrushShapeBase BrushShape { get => _brushShape; set => SetValue(ref _brushShape, value); }

    [DataMemberIgnore]
    public bool HasChanged { get; set; }

    private void SetValue<T>(ref T backingField, T newValue)
    {
        backingField = newValue;
        HasChanged = true;
    }

    public void CopyTo(PaintBrushSettings brushSetings)
    {
        brushSetings.BrushDiameter = BrushDiameter;
        brushSetings.BrushStrength = 1;
        brushSetings.Opacity = Opacity;
        brushSetings.StampSpacingPercentage = StampSpacingPercentage;
        brushSetings.BrushShape = BrushShape;
    }
}
