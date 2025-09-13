using Stride.Core.Mathematics;

namespace StrideEdExt.Painting;

public class PaintBrushstrokeHandle : IDisposable
{
    private readonly IPainterService _painterService;

    public PaintSessionId PaintSessionId { get; }


    public PaintBrushstrokeHandle(IPainterService painterService, PaintSessionId paintSessionId)
    {
        _painterService = painterService;
        PaintSessionId = paintSessionId;
    }

    public void Dispose()
    {
        _painterService.EndBrushstroke(this);
    }

    public override string ToString() => PaintSessionId.ToString();

    internal string DebugDisplayString => ToString();

    /// <summary>
    /// Add the brush to the current mouse location.
    /// </summary>
    public void AddBrushstrokePoint(Vector2 brushScreenPositionNormalized)
    {
        _painterService.AddBrushstrokePoint(this, brushScreenPositionNormalized);
    }

}
