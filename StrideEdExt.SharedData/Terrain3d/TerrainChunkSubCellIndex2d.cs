using Stride.Core;
using Stride.Core.Mathematics;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace SceneEditorExtensionExample.SharedData.Terrain3d;

[DataContract]
[DebuggerDisplay("{DebugDisplayString,nq}")]
public struct TerrainChunkSubCellIndex2d : IEquatable<TerrainChunkSubCellIndex2d>
{
    public static readonly TerrainChunkSubCellIndex2d Zero = new(0, 0);

    public int X;
    public int Z;

    public TerrainChunkSubCellIndex2d(int x, int z)
    {
        X = x;
        Z = z;
    }

    public override readonly string ToString() => string.Format(CultureInfo.CurrentCulture, "X:{0} Z:{1}", X, Z);

    internal readonly string DebugDisplayString => ToString();

    public override readonly int GetHashCode() => HashCode.Combine(X.GetHashCode(), Z.GetHashCode());

    public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is TerrainChunkSubCellIndex2d other && Equals(other);

    public readonly bool Equals(TerrainChunkSubCellIndex2d other)
    {
        return X == other.X
            && Z == other.Z;
    }

    public static bool operator ==(TerrainChunkSubCellIndex2d left, TerrainChunkSubCellIndex2d right) => left.Equals(right);

    public static bool operator !=(TerrainChunkSubCellIndex2d left, TerrainChunkSubCellIndex2d right) => !left.Equals(right);

    public static TerrainChunkSubCellIndex2d operator +(TerrainChunkSubCellIndex2d left, Int2 right)
    {
        return new TerrainChunkSubCellIndex2d(left.X + right.X, left.Z + right.Y);
    }

    public static TerrainChunkSubCellIndex2d operator +(Int2 left, TerrainChunkSubCellIndex2d right)
    {
        return new TerrainChunkSubCellIndex2d(left.X + right.X, left.Y + right.Z);
    }

    public static TerrainChunkSubCellIndex2d operator -(TerrainChunkSubCellIndex2d left, Int2 right)
    {
        return new TerrainChunkSubCellIndex2d(left.X - right.X, left.Z - right.Y);
    }

    public static Int2 operator -(TerrainChunkSubCellIndex2d left, TerrainChunkSubCellIndex2d right)
    {
        return new Int2(left.X - right.X, left.Z - right.Z);
    }

    public static TerrainChunkSubCellIndex2d operator -(TerrainChunkSubCellIndex2d value)
    {
        return new TerrainChunkSubCellIndex2d(-value.X, -value.Z);
    }

    public static implicit operator TerrainChunkSubCellIndex2d(Int2 value)
    {
        return new TerrainChunkSubCellIndex2d(value.X, value.Y);
    }

    public static explicit operator Int2(TerrainChunkSubCellIndex2d value)
    {
        return new Int2(value.X, value.Z);
    }
}
