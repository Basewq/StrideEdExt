using System.Diagnostics;

namespace StrideEdExt.Painting;

[DebuggerDisplay("{DebugDisplayString,nq}")]
class PaintSessionKey : IDisposable, IEquatable<PaintSessionKey>
{
    private readonly IPainterService _paintBrushService;
    
    public readonly PaintSessionId PaintSessionId;

    public PaintSessionKey(IPainterService paintBrushService, PaintSessionId paintSessionId)
    {
        _paintBrushService = paintBrushService;
        PaintSessionId = paintSessionId;
    }

    /// <summary>
    /// Ends the paint session when this method is called.
    /// </summary>
    public void Dispose()
    {
        _paintBrushService.EndSession(PaintSessionId);
    }

    public override string ToString() => PaintSessionId.ToString();

    internal string DebugDisplayString => ToString();

    public static bool operator ==(PaintSessionKey left, PaintSessionKey right) => left.Equals(right);

    public static bool operator !=(PaintSessionKey left, PaintSessionKey right) => !left.Equals(right);

    public override bool Equals(object? obj) => obj is PaintSessionKey key && Equals(key);

    public bool Equals(PaintSessionKey? other) => PaintSessionId == other?.PaintSessionId;

    public override int GetHashCode() => PaintSessionId.GetHashCode();

    public static implicit operator PaintSessionId(PaintSessionKey value)
    {
        return value.PaintSessionId;
    }
}
