using System.Diagnostics;

namespace StrideEdExt.Painting;

[DebuggerDisplay("{DebugDisplayString,nq}")]
public readonly struct PaintSessionId : IComparable<PaintSessionId>, IEquatable<PaintSessionId>
{
    private readonly byte _id;

    public static readonly PaintSessionId Empty = new();

    public PaintSessionId(byte id)
    {
        _id = id;
    }

    public override string ToString() => _id.ToString();

    internal readonly string DebugDisplayString => ToString();

    public static bool operator ==(PaintSessionId left, PaintSessionId right) =>  left.Equals(right);

    public static bool operator !=(PaintSessionId left, PaintSessionId right) => !left.Equals(right);

    public bool Equals(PaintSessionId other) => _id == other._id;

    public override bool Equals(object? obj) => obj is PaintSessionId id && Equals(id);

    public override int GetHashCode() => _id.GetHashCode();

    public int CompareTo(PaintSessionId other) => _id.CompareTo(other._id);

    public static explicit operator PaintSessionId(byte id) => new PaintSessionId(id);

    public static explicit operator byte(PaintSessionId id) => id._id;
}
