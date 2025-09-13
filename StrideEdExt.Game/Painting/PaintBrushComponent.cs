using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Design;
using Stride.Graphics;
using System.Diagnostics.CodeAnalysis;

namespace StrideEdExt.Painting;

[DataContract]
[DefaultEntityComponentRenderer(typeof(PaintBrushProcessor))]
class PaintBrushComponent : EntityComponent
{
    internal required PaintSessionId PaintSessionId { get; init; }
    internal required IPainterService PainterService { get; init; }
    internal IPainterTool? SelectedPainterTool { get; set; }

    [DataMemberIgnore]
    public bool IsBrushActionInProgress { get; set; }

    [DataMemberIgnore]
    public Color4 Color { get; set; } = Color4.White;

    internal bool TryGetActiveToolBrushSettings([NotNullWhen(true)] out PaintBrushSettings? brushSettings)
    {
        if (SelectedPainterTool is not null)
        {
            brushSettings = SelectedPainterTool.BrushSettings;
            return true;
        }
        brushSettings = null;
        return false;
    }
}
