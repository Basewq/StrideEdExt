using Stride.Core;
using Stride.Engine;
using Stride.Rendering;
using StrideEdExt.Rendering;

namespace StrideEdExt.Painting.Brushes;

[DataContract(Inherited = true)]
public abstract class PaintBrushShapeBase
{
    public abstract BrushModeType BrushMode { get; }

    public abstract Model CreateCursorPreviewModel(IServiceRegistry serviceRegistry);

    public virtual void UpdateCursorPreviewModel(ModelComponent modelComponent) { }
}
