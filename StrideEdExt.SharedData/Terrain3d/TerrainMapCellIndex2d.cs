using Stride.Core;
using Stride.Core.Mathematics;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace SceneEditorExtensionExample.SharedData.Terrain3d;

[DataContract]
[DebuggerDisplay("{DebugDisplayString,nq}")]
public struct TerrainMapCellIndex2d : IEquatable<TerrainMapCellIndex2d>
{
    public static readonly TerrainMapCellIndex2d Zero = new(0, 0);

    public int X;
    public int Z;

    public TerrainMapCellIndex2d(int x, int z)
    {
        X = x;
        Z = z;
    }

    public override readonly string ToString() => string.Format(CultureInfo.CurrentCulture, "X:{0} Z:{1}", X, Z);

    internal readonly string DebugDisplayString => ToString();

    public override readonly int GetHashCode() => HashCode.Combine(X.GetHashCode(), Z.GetHashCode());

    public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is TerrainMapCellIndex2d other && Equals(other);

    public readonly bool Equals(TerrainMapCellIndex2d other)
    {
        return X == other.X
            && Z == other.Z;
    }

    public static bool operator ==(TerrainMapCellIndex2d left, TerrainMapCellIndex2d right) => left.Equals(right);

    public static bool operator !=(TerrainMapCellIndex2d left, TerrainMapCellIndex2d right) => !left.Equals(right);

    public static TerrainMapCellIndex2d operator +(TerrainMapCellIndex2d left, Int2 right)
    {
        return new TerrainMapCellIndex2d(left.X + right.X, left.Z + right.Y);
    }

    public static TerrainMapCellIndex2d operator +(Int2 left, TerrainMapCellIndex2d right)
    {
        return new TerrainMapCellIndex2d(left.X + right.X, left.Y + right.Z);
    }

    public static TerrainMapCellIndex2d operator -(TerrainMapCellIndex2d left, Int2 right)
    {
        return new TerrainMapCellIndex2d(left.X - right.X, left.Z - right.Y);
    }

    public static Int2 operator -(TerrainMapCellIndex2d left, TerrainMapCellIndex2d right)
    {
        return new Int2(left.X - right.X, left.Z - right.Z);
    }

    public static TerrainMapCellIndex2d operator -(TerrainMapCellIndex2d value)
    {
        return new TerrainMapCellIndex2d(-value.X, -value.Z);
    }

    public static implicit operator TerrainMapCellIndex2d(Int2 value)
    {
        return new TerrainMapCellIndex2d(value.X, value.Y);
    }

    public static explicit operator Int2(TerrainMapCellIndex2d value)
    {
        return new Int2(value.X, value.Z);
    }
}
