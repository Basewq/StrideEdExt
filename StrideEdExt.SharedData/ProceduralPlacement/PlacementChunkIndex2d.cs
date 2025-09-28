using Stride.Core;
using Stride.Core.Mathematics;
using StrideEdExt.SharedData.Terrain3d;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace StrideEdExt.SharedData.ProceduralPlacement;

[Obsolete]      // TODO remove
[DataContract]
[DebuggerDisplay("{DebugDisplayString,nq}")]
public struct PlacementChunkIndex2d : IEquatable<PlacementChunkIndex2d>
{
    public static readonly PlacementChunkIndex2d Zero = new(0, 0);

    public int X;
    public int Z;

    public PlacementChunkIndex2d(int x, int z)
    {
        X = x;
        Z = z;
    }

    public override readonly string ToString() => string.Format(CultureInfo.CurrentCulture, "X:{0} Z:{1}", X, Z);

    internal readonly string DebugDisplayString => ToString();

    public override readonly int GetHashCode() => HashCode.Combine(X.GetHashCode(), Z.GetHashCode());

    public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is PlacementChunkIndex2d other && Equals(other);

    public readonly bool Equals(PlacementChunkIndex2d other)
    {
        return X == other.X
            && Z == other.Z;
    }

    public static bool operator ==(PlacementChunkIndex2d left, PlacementChunkIndex2d right) => left.Equals(right);

    public static bool operator !=(PlacementChunkIndex2d left, PlacementChunkIndex2d right) => !left.Equals(right);

    public static PlacementChunkIndex2d operator +(PlacementChunkIndex2d left, Int2 right)
    {
        return new PlacementChunkIndex2d(left.X + right.X, left.Z + right.Y);
    }

    public static PlacementChunkIndex2d operator +(Int2 left, PlacementChunkIndex2d right)
    {
        return new PlacementChunkIndex2d(left.X + right.X, left.Y + right.Z);
    }

    public static PlacementChunkIndex2d operator -(PlacementChunkIndex2d left, Int2 right)
    {
        return new PlacementChunkIndex2d(left.X - right.X, left.Z - right.Y);
    }

    public static Int2 operator -(PlacementChunkIndex2d left, PlacementChunkIndex2d right)
    {
        return new Int2(left.X - right.X, left.Z - right.Z);
    }

    public static PlacementChunkIndex2d operator -(PlacementChunkIndex2d value)
    {
        return new PlacementChunkIndex2d(-value.X, -value.Z);
    }

    public static implicit operator PlacementChunkIndex2d(Int2 value)
    {
        return new PlacementChunkIndex2d(value.X, value.Y);
    }

    public static explicit operator Int2(PlacementChunkIndex2d value)
    {
        return new Int2(value.X, value.Z);
    }

    public readonly TerrainChunkIndex2d AsTerrainChunkIndex2d() => new(X, Z);
}
