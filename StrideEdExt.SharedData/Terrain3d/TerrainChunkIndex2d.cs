using Stride.Core;
using Stride.Core.Mathematics;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace SceneEditorExtensionExample.SharedData.Terrain3d;

[DataContract]
[DebuggerDisplay("{DebugDisplayString,nq}")]
public struct TerrainChunkIndex2d : IEquatable<TerrainChunkIndex2d>
{
    public static readonly TerrainChunkIndex2d Zero = new(0, 0);

    public int X;
    public int Z;

    public TerrainChunkIndex2d(int x, int z)
    {
        X = x;
        Z = z;
    }

    public override readonly string ToString() => string.Format(CultureInfo.CurrentCulture, "X:{0} Z:{1}", X, Z);

    internal readonly string DebugDisplayString => ToString();

    public override readonly int GetHashCode() => HashCode.Combine(X.GetHashCode(), Z.GetHashCode());

    public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is TerrainChunkIndex2d other && Equals(other);

    public readonly bool Equals(TerrainChunkIndex2d other)
    {
        return X == other.X
            && Z == other.Z;
    }

    public static bool operator ==(TerrainChunkIndex2d left, TerrainChunkIndex2d right) => left.Equals(right);

    public static bool operator !=(TerrainChunkIndex2d left, TerrainChunkIndex2d right) => !left.Equals(right);

    public static TerrainChunkIndex2d operator +(TerrainChunkIndex2d left, Int2 right)
    {
        return new TerrainChunkIndex2d(left.X + right.X, left.Z + right.Y);
    }

    public static TerrainChunkIndex2d operator +(Int2 left, TerrainChunkIndex2d right)
    {
        return new TerrainChunkIndex2d(left.X + right.X, left.Y + right.Z);
    }

    public static TerrainChunkIndex2d operator -(TerrainChunkIndex2d left, Int2 right)
    {
        return new TerrainChunkIndex2d(left.X - right.X, left.Z - right.Y);
    }

    public static Int2 operator -(TerrainChunkIndex2d left, TerrainChunkIndex2d right)
    {
        return new Int2(left.X - right.X, left.Z - right.Z);
    }

    public static TerrainChunkIndex2d operator -(TerrainChunkIndex2d value)
    {
        return new TerrainChunkIndex2d(-value.X, -value.Z);
    }

    public static implicit operator TerrainChunkIndex2d(Int2 value)
    {
        return new TerrainChunkIndex2d(value.X, value.Y);
    }

    public static explicit operator Int2(TerrainChunkIndex2d value)
    {
        return new Int2(value.X, value.Z);
    }
}
