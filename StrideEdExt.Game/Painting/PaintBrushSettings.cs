using Stride.Core;
using Stride.Engine;
using Stride.Graphics;
using Stride.Rendering;
using StrideEdExt.Painting.Brushes;

namespace StrideEdExt.Painting;

public class PaintBrushSettings
{
    /// <summary>
    /// Brush diameter in world units.
    /// </summary>
    public float BrushDiameter { get; set; }
    public float BrushStrength { get; set; }
    public float Opacity { get; set; }
    /// <summary>
    /// Distance between each brush stamp draw, as a percentage of <see cref="BrushDiameter"/>.
    /// </summary>
    public float StampSpacingPercentage { get; set; }

    public PaintBrushShapeBase BrushShape  { get; set; } = new PaintCircularBrushShape();

    internal Model CreateCursorPreviewModel(IServiceRegistry serviceRegistry)
    {
        return BrushShape.CreateCursorPreviewModel(serviceRegistry);
    }

    internal void UpdateCursorPreviewModel(ModelComponent modelComponent)
    {
        BrushShape.UpdateCursorPreviewModel(modelComponent);
    }
}
